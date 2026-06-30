// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command handling both <c>find</c> (one-shot) and <c>wait-for</c> (polls until a
/// timeout). Resolves a target window, then locates a single UIA element by name/automationid/
/// classname. Always replies success with a <c>found</c> flag (a miss is not an error). Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited. Disabled by default (security).
/// </summary>
public class FindCommand : Command {
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("handle")] public long Handle { get; set; }
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("className")] public string ClassName { get; set; } = null!;
    [XmlAttribute("foreground")] public bool Foreground { get; set; }
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static new List<Command> BuiltInCommands {
        get => [
            new FindCommand { Cmd = "find" },
            new FindCommand { Cmd = "wait-for" },
        ];
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new FindCommand {
        Window = this.Window,
        Handle = this.Handle,
        Process = this.Process,
        ClassName = this.ClassName,
        Foreground = this.Foreground,
        By = this.By,
        Value = this.Value,
        Timeout = this.Timeout,
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

        int timeout = Timeout;
        if (string.Equals(Cmd, "wait-for", StringComparison.OrdinalIgnoreCase) && timeout <= 0) {
            timeout = 5000;
        }
        AgentRuntime.Audit(Cmd, $"find by={By} value='{Value}' timeout={timeout} window handle={Handle} title='{Window}' process='{Process}'");

        WindowInfo? win = WindowResolver.Resolve(Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
        if (win is null) {
            Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window").ToJson());
            return false;
        }

        IntPtr h = new IntPtr(win.Handle);
        UiaElementInfo? info = UiaService.Find(h, By, Value, timeout);
        bool found = info is not null;
        JsonObject data = new() {
            ["found"] = found,
            ["element"] = info?.ToJsonObject(),
            // Echo the resolved window so the session can record it as the active target (a find/wait-for
            // that establishes the current control feeds error.lastObservation for a later failing action).
            ["window"] = win.ToJsonObject(),
        };
        Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
        return true;
    }
}
