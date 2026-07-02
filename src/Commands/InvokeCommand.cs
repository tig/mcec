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

    public static new List<Command> BuiltInCommands {
        get => [new InvokeCommand { Cmd = "invoke" }];
    }

    protected override string? AuditDetails() =>
        $"invoke action={Action} by={By} value='{Value}' window handle={Handle} title='{Window}' process='{Process}'";

    protected override bool ExecuteCore(WindowInfo? target) {
        IntPtr h = new IntPtr(target!.Handle);
        UiaInvokeResult outcome = UiaService.Invoke(h, By, Value, Action, Text);
        if (outcome == UiaInvokeResult.Ok) {
            JsonObject data = new() {
                ["invoked"] = true,
                ["action"] = Action,
                ["by"] = By,
                ["value"] = Value,
            };
            Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
            return true;
        }
        Reply?.WriteLine(FailureFor(outcome).ToJson());
        return false;
    }

    /// <summary>
    /// Maps a failed <see cref="UiaInvokeResult"/> onto a distinct error code/category (#206). The
    /// distinctions matter for recovery: <c>no-target</c> means wait/re-find; <c>invalid-argument</c>
    /// means fix the call (re-finding a pattern-less element loops forever); <c>internal</c> means
    /// report it. Internal so tests can pin the mapping without a live UIA tree.
    /// </summary>
    internal CommandResult FailureFor(UiaInvokeResult outcome) => outcome switch {
        UiaInvokeResult.ElementNotFound => CommandResult.Fail(Cmd,
            $"Element not found ({By}='{Value}') in the target window. invoke does not wait — find/wait-for the element first.",
            "element-not-found", "no-target"),
        UiaInvokeResult.PatternUnsupported => CommandResult.Fail(Cmd,
            $"The element ({By}='{Value}') exists but does not support the UIA pattern the '{Action}' action needs. " +
            "Re-finding it will not help — use a different action (or click it) instead.",
            "pattern-unsupported", "invalid-argument"),
        UiaInvokeResult.ActionUnknown => CommandResult.Fail(Cmd,
            $"Unknown action '{Action}'. Use invoke, toggle, setvalue, setfocus, expand, collapse, or select.",
            "action-unknown", "invalid-argument"),
        _ => CommandResult.Fail(Cmd,
            "Invoke faulted: UI Automation threw while attaching to or driving the target (it may have closed mid-call). Re-observe the window.",
            "invoke-faulted", "internal"),
    };
}
