// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: resolves a target window, finds a single UIA element, then runs a UIA
/// pattern action against it (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>/<c>select</c>). Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
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
        bool ok = UiaService.Invoke(h, By, Value, Action, Text);
        if (ok) {
            JsonObject data = new() {
                ["invoked"] = ok,
                ["action"] = Action,
                ["by"] = By,
                ["value"] = Value,
            };
            Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
        }
        else {
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Invoke failed (element not found or pattern unsupported)").ToJson());
        }
        return ok;
    }
}
