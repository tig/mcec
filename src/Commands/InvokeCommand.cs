// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent actuation command: resolves a target window, finds a single UIA element, then runs a UIA
/// pattern action against it (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>/<c>select</c>). Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited. Disabled by default (security).
/// </summary>
public class InvokeCommand : Command {
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("handle")] public long Handle { get; set; }
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("classname")] public string ClassName { get; set; } = null!;
    [XmlAttribute("foreground")] public bool Foreground { get; set; }
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("action")] public string Action { get; set; } = "invoke";
    [XmlAttribute("text")] public string Text { get; set; } = null!;

    public static new List<Command> BuiltInCommands {
        get => [new InvokeCommand { Cmd = "invoke" }];
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new InvokeCommand {
        Window = this.Window,
        Handle = this.Handle,
        Process = this.Process,
        ClassName = this.ClassName,
        Foreground = this.Foreground,
        By = this.By,
        Value = this.Value,
        Action = this.Action,
        Text = this.Text,
    });

    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }
        AgentRuntime.Audit(Cmd, $"invoke action={Action} by={By} value='{Value}' window handle={Handle} title='{Window}' process='{Process}'");

        WindowInfo? win = WindowResolver.Resolve(Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
        if (win is null) {
            Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window").ToJson());
            return false;
        }

        IntPtr h = new IntPtr(win.Handle);
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
