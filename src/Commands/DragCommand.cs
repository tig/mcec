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
/// intermediate waypoints (<see cref="PathSpec"/>). The whole gesture is dispatched atomically by
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

    // A window is only needed to resolve element endpoints; a pure coordinate drag needs none.
    protected override bool RequiresWindowTarget => !string.IsNullOrEmpty(FromValue) || !string.IsNullOrEmpty(ToValue);

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr hwnd = target is null ? IntPtr.Zero : new IntPtr(target.Handle);

        // Endpoint misses are element lookups, so they carry the same element-not-found / no-target
        // taxonomy as click/invoke (#206); the recovery (wait-for/re-find the element) is identical.
        if (!TryResolvePoint(hwnd, FromBy, FromValue, FromX, FromY, out (int X, int Y) from, out string? fromError)) {
            return CommandResult.Fail(Cmd, $"Drag start: {fromError}", "element-not-found", "no-target");
        }
        if (!TryResolvePoint(hwnd, ToBy, ToValue, ToX, ToY, out (int X, int Y) to, out string? toError)) {
            return CommandResult.Fail(Cmd, $"Drag end: {toError}", "element-not-found", "no-target");
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
    /// <paramref name="y"/>). Returns false with <paramref name="error"/> when an element endpoint has
    /// no window or the element can't be found.
    /// </summary>
    private static bool TryResolvePoint(IntPtr hwnd, string by, string value, int x, int y, out (int X, int Y) point, out string? error) {
        error = null;
        if (string.IsNullOrEmpty(value)) {
            point = (x, y);
            return true;
        }
        if (hwnd == IntPtr.Zero) {
            point = default;
            error = "element endpoint requires a target window";
            return false;
        }
        UiaElementInfo? el = UiaService.Find(hwnd, string.IsNullOrEmpty(by) ? "name" : by, value, FindTimeoutMs);
        if (el is null) {
            point = default;
            error = $"element not found ({(string.IsNullOrEmpty(by) ? "name" : by)}='{value}')";
            return false;
        }
        point = (el.X + (el.Width / 2), el.Y + (el.Height / 2));
        return true;
    }

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
