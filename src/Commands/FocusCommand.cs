// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: give a target window (and, optionally, a specific control in it) real
/// KEYBOARD FOCUS, so subsequent keystrokes (<c>send_command</c> VK codes, <c>chars</c>) actually reach
/// it (#270). Keyboard input only ever goes to the foreground window's focused control, and neither a
/// bare click nor a bare UIA <c>SetFocus</c> reliably lands focus on a custom-drawn surface (a MAUI
/// GraphicsView, a game canvas): a UIA <c>SetFocus</c> can misroute to a sibling ComboBox, and a click
/// only focuses if the window is foreground at that pixel. This command does the whole gesture the way a
/// human does and VERIFIES each step, which is also what finally makes the closed taxonomy's
/// <c>foreground</c> and <c>focus</c> categories real producers (#91):
/// <list type="number">
///   <item>bring the window to the foreground and confirm it (<see cref="FocusService.BringToForeground"/>) — else <c>foreground</c>;</item>
///   <item>when an endpoint is given, click it (a real WM_LBUTTONDOWN/UP) to place focus on the control under the point;</item>
///   <item>confirm focus landed WHERE ASKED — for an element endpoint the resolved element must hold
///   <c>HasKeyboardFocus</c> (reinforced with a UIA <c>SetFocus</c> only if the click did not focus it,
///   so a good click-focus is not misrouted to a sibling); for a pixel/window focus, that focus is
///   somewhere in the window (<see cref="FocusService.IsFocusInWindow"/>) — else <c>focus</c>.</item>
/// </list>
/// Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class FocusCommand : WindowTargetingAgentCommand {
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("x")] public int X { get; set; }
    [XmlAttribute("y")] public int Y { get; set; }

    /// <summary>True when the caller gave an absolute-pixel endpoint (<see cref="X"/>/<see cref="Y"/>) rather
    /// than an element or a bare window; lets a literal <c>(0,0)</c> pixel be distinguished from "no point".</summary>
    [XmlAttribute("point")] public bool PointSpecified { get; set; }

    // Wait a beat for an element endpoint to appear before failing, matching click/drag's
    // "wait a beat, then fail cleanly" rather than blocking indefinitely.
    private const int FindTimeoutMs = 3000;

    public static List<Command> BuiltInCommands {
        get => [new FocusCommand { Cmd = "focus" }];
    }

    protected override string AuditDetails() =>
        $"focus (by={By} value='{Value}' point={(PointSpecified ? $"{X},{Y}" : "-")}) window handle={Handle} title='{Window}' process='{Process}'";

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr hwnd = new IntPtr(target!.Handle);

        // 1. Foreground first (verified): keyboard input only reaches the foreground window, and a click
        // lands on the target only if it is on top at that pixel.
        if (!FocusService.BringToForeground(hwnd)) {
            return ForegroundFailure();
        }

        // 2. Resolve the endpoint to click (element centre or an explicit pixel), if any was given.
        if (!TryResolveEndpoint(hwnd, out UiaElementInfo? element, out (int X, int Y)? point, out CommandResult? failure)) {
            return failure!;
        }

        // 3. Click to place focus on the control under the point (a real click focuses a surface a bare
        // UIA SetFocus can miss or misroute; #270).
        if (point is { } p) {
            MouseCommand.PerformClick(p, "left", 1);
        }

        // 4. Confirm focus landed WHERE IT WAS ASKED TO. For an element endpoint that means the RESOLVED
        // element must hold keyboard focus (#272 CR): confirming only that focus is somewhere in the
        // top-level window would pass even if it stayed on a sibling. For a pixel/window focus no specific
        // control was named, so a window-level confirmation is all we can honestly check.
        bool confirmed = element is not null
            ? ConfirmElementFocus(hwnd)
            : FocusService.ConfirmFocusInWindow(hwnd);
        if (!confirmed) {
            return FocusFailure(target);
        }

        return Success(target, element, point);
    }

    private string EffectiveBy => string.IsNullOrEmpty(By) ? "name" : By;

    /// <summary>
    /// Confirms the resolved element holds keyboard focus, reinforcing with a UIA <c>SetFocus</c> only if
    /// the click did not already focus it; so a good click-focus is never disturbed by a SetFocus that
    /// could misroute to a sibling (#270/#272). When UIA cannot read focus at all (windowless/unreadable,
    /// <see langword="null"/>), falls back to the window-level native check rather than manufacturing a
    /// failure; a definitive "not focused" (<see langword="false"/>) is a real <c>focus</c> failure.
    /// </summary>
    private bool ConfirmElementFocus(IntPtr hwnd) {
        if (UiaService.ElementHasKeyboardFocus(hwnd, EffectiveBy, Value) == true) {
            return true;
        }
        // The click did not focus it (or focus was unreadable); reinforce with UIA SetFocus, then re-check.
        UiaService.Invoke(hwnd, EffectiveBy, Value, "setfocus", null);
        bool? has = UiaService.ElementHasKeyboardFocus(hwnd, EffectiveBy, Value);
        return has == true || (has is null && FocusService.IsFocusInWindow(hwnd));
    }

    /// <summary>
    /// Resolves the optional endpoint: an element (<see cref="Value"/>, clicked at its centre), an
    /// explicit pixel (<see cref="PointSpecified"/>), or nothing (a bare window focus). Returns false with
    /// a structured <paramref name="failure"/> when an element endpoint can't be resolved (not found,
    /// ambiguous, stale window, elevated target; #261).
    /// </summary>
    private bool TryResolveEndpoint(IntPtr hwnd, out UiaElementInfo? element, out (int X, int Y)? point, out CommandResult? failure) {
        element = null;
        point = null;
        failure = null;
        if (!string.IsNullOrEmpty(Value)) {
            UiaFindOutcome outcome = UiaService.Find(hwnd, EffectiveBy, Value, FindTimeoutMs);
            failure = UiaFindFailureFor(Cmd, EffectiveBy, Value, outcome);
            if (failure is null && outcome.Element is null) {
                failure = CommandResult.Fail(Cmd, $"element not found ({EffectiveBy}='{Value}')", "element-not-found", "no-target");
            }
            if (failure is not null) {
                return false;
            }
            element = outcome.Element!;
            point = (element.X + (element.Width / 2), element.Y + (element.Height / 2));
            return true;
        }
        if (PointSpecified) {
            point = (X, Y);
        }
        return true;
    }

    private CommandResult Success(WindowInfo target, UiaElementInfo? element, (int X, int Y)? point) {
        JsonObject data = new() {
            ["focused"] = true,
            ["window"] = target.ToJsonObject(),
            ["focusHandle"] = FocusService.FocusedWindow(new IntPtr(target.Handle)).ToInt64(),
        };
        if (element is not null) {
            data["element"] = element.ToJsonObject();
        }
        if (point is { } p) {
            data["at"] = new JsonArray { p.X, p.Y };
        }
        return CommandResult.Ok(Cmd, data);
    }

    /// <summary>The <c>foreground</c> failure: Windows refused to activate the target (#91). Internal so
    /// tests can pin the code/category without a live desktop.</summary>
    internal CommandResult ForegroundFailure() => CommandResult.Fail(Cmd,
        "Could not bring the target window to the foreground; Windows refused the activation (foreground lock, " +
        "a modal on another app, or a full-screen exclusive window is holding it). Keyboard input will not reach " +
        "the target. Retry after whatever holds the foreground is dismissed, or ask the operator to click the target.",
        "foreground-not-set", "foreground");

    /// <summary>The <c>focus</c> failure: the window is foreground but no control in it took keyboard focus
    /// (#91, #270). Carries the resolved window as lastObservation. Internal so tests can pin the mapping.</summary>
    internal CommandResult FocusFailure(WindowInfo target) => CommandResult.Fail(Cmd,
        "The target window is foreground but keyboard focus did not land in it (no focusable control took focus, " +
        "or focus went to a sibling control). App keyboard shortcuts may not reach the intended control. Try a " +
        "`click` on the exact control first, or drive it with `invoke` instead of keystrokes.",
        "focus-not-confirmed", "focus", target.ToJsonObject());
}
