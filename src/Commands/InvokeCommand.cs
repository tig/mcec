// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: resolves a target window, finds a single UIA element, then runs a UIA
/// pattern action against it (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>/<c>select</c>).
/// Failures are categorical (#206): each <see cref="UiaInvokeResult"/> outcome maps to a distinct
/// error code/category (see <see cref="FailureFor"/>) so an agent can tell "wait for it" from "stop
/// re-finding and change the action". Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and
/// audited (structurally, via <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class InvokeCommand : WindowTargetingAgentCommand {
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("action")] public string Action { get; set; } = "invoke";
    [XmlAttribute("text")] public string Text { get; set; } = null!;

    public static List<Command> BuiltInCommands {
        get => [new InvokeCommand { Cmd = "invoke" }];
    }

    protected override string AuditDetails() =>
        $"invoke action={Action} by={By} value='{Value}' window handle={Handle} title='{Window}' process='{Process}'";

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr h = new IntPtr(target!.Handle);
        UiaInvokeResult outcome = UiaService.Invoke(h, By, Value, Action, Text, out int matchCount);
        if (outcome == UiaInvokeResult.Ok) {
            JsonObject data = new() {
                ["invoked"] = true,
                ["action"] = Action,
                ["by"] = By,
                ["value"] = Value,
            };
            return CommandResult.Ok(Cmd, data);
        }
        return FailureFor(outcome, matchCount);
    }

    /// <summary>
    /// Maps a failed <see cref="UiaInvokeResult"/> onto a distinct error code/category (#206, #261).
    /// The distinctions matter for recovery: <c>no-target</c> means wait/re-find; <c>invalid-argument</c>
    /// means fix the call (re-finding a pattern-less element loops forever); <c>ambiguous-selector</c>
    /// means narrow the selector; <c>stale-element</c> means re-observe for a fresh target;
    /// <c>elevation</c> and <c>internal</c> mean report it. Internal so tests can pin the mapping
    /// without a live UIA tree.
    /// </summary>
    internal CommandResult FailureFor(UiaInvokeResult outcome, int matchCount = 0) => outcome switch {
        UiaInvokeResult.ElementNotFound => CommandResult.Fail(Cmd,
            $"Element not found ({By}='{Value}') in the target window. invoke does not wait; find/wait-for the element first.",
            "element-not-found", "no-target"),
        UiaInvokeResult.PatternUnsupported => CommandResult.Fail(Cmd,
            $"The element ({By}='{Value}') exists but does not support the UIA pattern the '{Action}' action needs. " +
            "Re-finding it will not help; use a different action (or click it) instead.",
            "pattern-unsupported", "invalid-argument"),
        UiaInvokeResult.ActionUnknown => CommandResult.Fail(Cmd,
            $"Unknown action '{Action}'. Use invoke, toggle, setvalue, setfocus, expand, collapse, or select.",
            "action-unknown", "invalid-argument"),
        UiaInvokeResult.ElementAmbiguous => CommandResult.Fail(Cmd,
            $"The selector ({By}='{Value}') matched {matchCount} elements; refusing to guess which to act on. " +
            "Narrow it: prefer automationId, or add className or a more specific name.",
            $"selector-matched-{matchCount}", "ambiguous-selector"),
        UiaInvokeResult.ElementStale => CommandResult.Fail(Cmd,
            $"The element ({By}='{Value}') or its window went away mid-call (closed or re-rendered). " +
            "Re-query/find for a fresh target, then retry.",
            "element-stale", "stale-element"),
        UiaInvokeResult.TargetElevated => CommandResult.Fail(Cmd,
            "UI Automation was denied access to the target; it runs at a higher integrity level (UAC) than MCEC " +
            "and cannot be driven. Surface this to the operator; do not retry.",
            "target-elevated", "elevation"),
        _ => CommandResult.Fail(Cmd,
            "Invoke faulted: UI Automation threw while attaching to or driving the target (it may have closed mid-call). Re-observe the window.",
            "invoke-faulted", "internal"),
    };
}
