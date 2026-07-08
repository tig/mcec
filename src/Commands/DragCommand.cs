// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: a first-class press → move-path → release drag (issue #123). Each endpoint
/// is either an absolute screen pixel (<c>fromX/fromY</c>, <c>toX/toY</c>) or a UI Automation element in
/// the target window (<c>fromValue</c>/<c>toValue</c>, resolved to its bounds' centre), with optional
/// intermediate waypoints (<see cref="PathSpec"/>). When a window target is supplied and an endpoint is
/// pixel coordinates, that endpoint is interpreted as window-client-relative and translated to absolute
/// screen pixels via the target window's client origin. The whole gesture is dispatched atomically by
/// <see cref="MouseCommand.PerformDrag"/> so it cannot interleave with another command's mouse input
/// (the hazard #113 warns about when a caller hand-rolls <c>lbd</c>/<c>mt</c>/<c>lbu</c>). Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class DragCommand : WindowTargetingAgentCommand {
    // NOTE: attribute names MUST be all-lowercase. SerializedCommands.Deserialize runs an XSLT that
    // lower-cases every element/attribute name before deserializing, so a camelCase name (e.g. "fromValue")
    // would never bind on load and the value would be silently lost.
    [XmlAttribute("fromby")] public string FromBy { get; set; } = "name";
    [XmlAttribute("fromvalue")] public string FromValue { get; set; } = null!;
    [XmlAttribute("fromx")] public int FromX { get; set; }
    [XmlAttribute("fromy")] public int FromY { get; set; }

    [XmlAttribute("toby")] public string ToBy { get; set; } = "name";
    [XmlAttribute("tovalue")] public string ToValue { get; set; } = null!;
    [XmlAttribute("tox")] public int ToX { get; set; }
    [XmlAttribute("toy")] public int ToY { get; set; }

    /// <summary>Optional intermediate waypoints as <c>x,y;x,y;...</c> in absolute screen pixels.</summary>
    [XmlAttribute("path")] public string PathSpec { get; set; } = null!;

    // How long to wait for an element endpoint to appear before failing. Matches the "wait a beat, then
    // fail cleanly" behaviour of invoke rather than blocking indefinitely.
    private const int FindTimeoutMs = 3000;

    public static List<Command> BuiltInCommands {
        get => [new DragCommand { Cmd = "drag" }];
    }

    protected override string AuditDetails() =>
        $"drag from (by={FromBy} value='{FromValue}' {FromX},{FromY}) to (by={ToBy} value='{ToValue}' {ToX},{ToY}) window handle={Handle} title='{Window}' process='{Process}'";

    // A window is needed for element endpoints, and also for pixel endpoints when a target selector was
    // provided (pixel endpoints become window-client-relative in that case).
    protected override bool RequiresWindowTarget => HasWindowTarget || !string.IsNullOrEmpty(FromValue) || !string.IsNullOrEmpty(ToValue);

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr hwnd = target is null ? IntPtr.Zero : new IntPtr(target.Handle);

        // Endpoint misses are element lookups, so they carry the same taxonomy as click/invoke
        // (#206, #261: no-target, ambiguous-selector, stale-element, elevation); the recoveries match.
        if (!TryResolvePoint(hwnd, FromBy, FromValue, FromX, FromY, "Drag start", Cmd, out (int X, int Y) from, out CommandResult? fromFailure)) {
            return fromFailure!;
        }
        if (!TryResolvePoint(hwnd, ToBy, ToValue, ToX, ToY, "Drag end", Cmd, out (int X, int Y) to, out CommandResult? toFailure)) {
            return toFailure!;
        }

        List<(int X, int Y)> waypoints = [from];
        waypoints.AddRange(ParsePath(PathSpec));
        waypoints.Add(to);

        MouseCommand.PerformDrag(waypoints);

        JsonObject data = new() {
            ["dragged"] = true,
            ["from"] = new JsonArray { from.X, from.Y },
            ["to"] = new JsonArray { to.X, to.Y },
            ["points"] = waypoints.Count,
        };
        return CommandResult.Ok(Cmd, data);
    }

    /// <summary>
    /// Resolves one drag endpoint to an absolute screen pixel: an element (by/value, centre of its
    /// bounds) when <paramref name="value"/> is set, else the literal (<paramref name="x"/>,
    /// <paramref name="y"/>) when no window target is set, else the target window's client-origin
    /// offset plus (<paramref name="x"/>, <paramref name="y"/>) when a window target is set. Returns
    /// false with a structured <paramref name="failure"/> when an element endpoint has no window or
    /// can't be resolved (not found, ambiguous, stale window, elevated target; #261), or when a targeted
    /// window disappears before its client origin can be read. <paramref name="endpoint"/> ("Drag
    /// start"/"Drag end") is prefixed onto the detail of EVERY failure category (via
    /// <see cref="LabelEndpoint"/>) so a two-endpoint drag always says which end failed (#262 review).
    /// </summary>
    private static bool TryResolvePoint(IntPtr hwnd, string by, string value, int x, int y, string endpoint, string cmd, out (int X, int Y) point, out CommandResult? failure) {
        failure = null;
        if (string.IsNullOrEmpty(value)) {
            if (hwnd == IntPtr.Zero) {
                point = (x, y);
                return true;
            }
            if (!TryGetWindowClientOrigin(hwnd, out (int X, int Y) origin)) {
                point = default;
                failure = LabelEndpoint(endpoint, CommandResult.Fail(cmd,
                    "The target window disappeared before drag coordinates could be resolved. Re-query and retry.",
                    "window-closed", "stale-element"));
                return false;
            }
            point = OffsetByClientOrigin((x, y), origin);
            return true;
        }
        if (hwnd == IntPtr.Zero) {
            point = default;
            failure = LabelEndpoint(endpoint, CommandResult.Fail(cmd, "element endpoint requires a target window", "element-not-found", "no-target"));
            return false;
        }
        string effectiveBy = string.IsNullOrEmpty(by) ? "name" : by;
        UiaFindOutcome outcome = UiaService.Find(hwnd, effectiveBy, value, FindTimeoutMs);
        CommandResult? raw = UiaFindFailureFor(cmd, effectiveBy, value, outcome)
            ?? (outcome.Element is null
                ? CommandResult.Fail(cmd, $"element not found ({effectiveBy}='{value}')", "element-not-found", "no-target")
                : null);
        if (raw is not null) {
            point = default;
            failure = LabelEndpoint(endpoint, raw);
            return false;
        }
        UiaElementInfo el = outcome.Element!;
        point = (el.X + (el.Width / 2), el.Y + (el.Height / 2));
        return true;
    }

    /// <summary>
    /// Prefixes an endpoint label ("Drag start"/"Drag end") onto a resolution failure's detail so a
    /// two-endpoint drag reports WHICH end failed, uniformly across every failure category (#262
    /// review). Rebuilds the result because <see cref="CommandResult.Error"/> is init-only; code,
    /// category, and any carried data are preserved. Internal so the mapping is unit-testable without
    /// a live UIA tree.
    /// </summary>
    internal static CommandResult LabelEndpoint(string endpoint, CommandResult failure) =>
        CommandResult.Fail(failure.Command!, $"{endpoint}: {failure.Error}", failure.ErrorCode!, failure.ErrorCategory!, failure.Data);

    /// <summary>Parses <c>x,y;x,y;...</c> intermediate waypoints; skips malformed pairs.</summary>
    private static List<(int X, int Y)> ParsePath(string? spec) {
        List<(int X, int Y)> points = [];
        if (string.IsNullOrWhiteSpace(spec)) {
            return points;
        }
        foreach (string pair in spec.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            string[] xy = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length == 2 && int.TryParse(xy[0], out int px) && int.TryParse(xy[1], out int py)) {
                points.Add((px, py));
            }
        }
        return points;
    }
}
