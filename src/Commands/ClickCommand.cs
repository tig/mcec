// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: a single mouse click at a target point (issue #122). The point is either an
/// absolute screen pixel (<c>x</c>/<c>y</c>) or a UI Automation element in the target window (<c>value</c>,
/// resolved to the centre of its bounds); the same pixel space <c>query</c>/<c>find</c> report, so an
/// agent can click a control it just observed without converting to normalized coordinates itself. The
/// move-then-click is dispatched atomically by <see cref="MouseCommand.PerformClick"/> so it cannot
/// interleave with another command's mouse input (the hazard #113 warns about when a caller hand-rolls
/// <c>mt</c>/<c>lbc</c>). Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited
/// (structurally, via <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class ClickCommand : WindowTargetingAgentCommand {
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("x")] public int X { get; set; }
    [XmlAttribute("y")] public int Y { get; set; }

    /// <summary>Which button to click: left | right | middle (default left).</summary>
    [XmlAttribute("button")] public string Button { get; set; } = "left";

    /// <summary>Click count: 1 = single, 2 = double (default 1).</summary>
    [XmlAttribute("count")] public int Count { get; set; } = 1;

    // How long to wait for an element endpoint to appear before failing; matches DragCommand's
    // "wait a beat, then fail cleanly" rather than blocking indefinitely.
    private const int FindTimeoutMs = 3000;

    public static List<Command> BuiltInCommands {
        get => [new ClickCommand { Cmd = "click" }];
    }

    protected override string AuditDetails() =>
        $"click at (by={By} value='{Value}' {X},{Y}) button={Button} count={Count} window handle={Handle} title='{Window}' process='{Process}'";

    // A window is only needed to resolve an element endpoint; a pure pixel click needs none.
    protected override bool RequiresWindowTarget => !string.IsNullOrEmpty(Value);

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr hwnd = target is null ? IntPtr.Zero : new IntPtr(target.Handle);

        if (!TryResolvePoint(hwnd, out (int X, int Y) point, out CommandResult? failure)) {
            return failure!;
        }

        // Clamp to the {1,2} the contract exposes and normalize the button, then dispatch and echo the
        // SAME values; so the result faithfully reports the gesture performed (e.g. count 3 is not run as
        // a double-click while claiming "3", and button "R" is reported as "right", not the raw input).
        int count = Count >= 2 ? 2 : 1;
        string button = MouseCommand.NormalizeButton(Button);
        MouseCommand.PerformClick(point, button, count);

        JsonObject data = new() {
            ["clicked"] = true,
            ["at"] = new JsonArray { point.X, point.Y },
            ["button"] = button,
            ["count"] = count,
        };
        return CommandResult.Ok(Cmd, data);
    }

    /// <summary>
    /// Resolves the click point to an absolute screen pixel: the centre of an element (<see cref="By"/>/
    /// <see cref="Value"/>) when <see cref="Value"/> is set, else the literal (<see cref="X"/>,
    /// <see cref="Y"/>). Returns false with a structured <paramref name="failure"/> when the element
    /// can't be resolved (not found, ambiguous, stale window, elevated target; #261).
    /// </summary>
    private bool TryResolvePoint(IntPtr hwnd, out (int X, int Y) point, out CommandResult? failure) {
        failure = null;
        if (string.IsNullOrEmpty(Value)) {
            point = (X, Y);
            return true;
        }
        string by = string.IsNullOrEmpty(By) ? "name" : By;
        // hwnd is guaranteed non-zero here: the base resolves the window whenever Value is set.
        UiaFindOutcome outcome = UiaService.Find(hwnd, by, Value, FindTimeoutMs);
        failure = UiaFindFailureFor(Cmd, by, Value, outcome);
        if (failure is null && outcome.Element is null) {
            failure = CommandResult.Fail(Cmd, $"element not found ({by}='{Value}')", "element-not-found", "no-target");
        }
        if (failure is not null) {
            point = default;
            return false;
        }
        UiaElementInfo el = outcome.Element!;
        point = (el.X + (el.Width / 2), el.Y + (el.Height / 2));
        return true;
    }
}
