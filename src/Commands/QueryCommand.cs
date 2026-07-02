// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command: resolves a target window and returns a depth-bounded UIA tree dump so a
/// model can see the window's controls. Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and
/// audited. Disabled by default (security).
/// </summary>
public class QueryCommand : Command {
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("handle")] public long Handle { get; set; }
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("className")] public string ClassName { get; set; } = null!;
    [XmlAttribute("foreground")] public bool Foreground { get; set; }
    [XmlAttribute("maxDepth")] public int MaxDepth { get; set; } = 6;

    /// <summary>Upper bound on UIA nodes returned; keeps the result bounded for huge/virtualized trees.
    /// A clipped walk is reported via a <c>tree-truncated</c> warning. 0 means unbounded.</summary>
    [XmlAttribute("maxNodes")] public int MaxNodes { get; set; } = 1000;

    public static new List<Command> BuiltInCommands {
        get => [new QueryCommand { Cmd = "query" }];
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new QueryCommand {
        Window = this.Window,
        Handle = this.Handle,
        Process = this.Process,
        ClassName = this.ClassName,
        Foreground = this.Foreground,
        MaxDepth = this.MaxDepth,
        MaxNodes = this.MaxNodes,
    });

    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED; agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }
        AgentRuntime.Audit(Cmd, $"query window handle={Handle} title='{Window}' process='{Process}' class='{ClassName}' fg={Foreground} maxDepth={MaxDepth} maxNodes={MaxNodes}");

        WindowInfo? win = WindowResolver.Resolve(Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
        if (win is null) {
            Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window", "window-not-found", "no-target").ToJson());
            return false;
        }

        IntPtr h = new IntPtr(win.Handle);
        UiaTreeResult tree = UiaService.DumpTree(h, MaxDepth, MaxNodes);
        JsonObject data = new() {
            ["window"] = win.ToJsonObject(),
            ["nodeCount"] = tree.NodeCount,
            ["truncated"] = tree.Truncated,
            ["tree"] = tree.Root?.ToJsonObject(),
        };
        CommandResult res = CommandResult.Ok(Cmd, data);
        if (tree.Truncated) {
            res.Warn("tree-truncated", $"UIA tree exceeded the {MaxNodes}-node cap and was clipped; raise maxNodes or narrow the target for a complete tree.");
        }
        Reply?.WriteLine(res.ToJson());
        return true;
    }
}
