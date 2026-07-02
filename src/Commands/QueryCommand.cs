// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command: resolves a target window and returns a depth-bounded UIA tree dump so a
/// model can see the window's controls. Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and
/// audited (structurally, via <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class QueryCommand : WindowTargetingAgentCommand {
    [XmlAttribute("maxdepth")] public int MaxDepth { get; set; } = 6;

    /// <summary>Upper bound on UIA nodes returned; keeps the result bounded for huge/virtualized trees.
    /// A clipped walk is reported via a <c>tree-truncated</c> warning. 0 means unbounded.</summary>
    [XmlAttribute("maxnodes")] public int MaxNodes { get; set; } = 1000;

    public static new List<Command> BuiltInCommands {
        get => [new QueryCommand { Cmd = "query" }];
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new QueryCommand {
        MaxDepth = this.MaxDepth,
        MaxNodes = this.MaxNodes,
    });

    protected override string? AuditDetails() =>
        $"query window handle={Handle} title='{Window}' process='{Process}' class='{ClassName}' fg={Foreground} maxDepth={MaxDepth} maxNodes={MaxNodes}";

    protected override bool OnWindowNotFound() {
        Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window", "window-not-found", "no-target").ToJson());
        return false;
    }

    protected override bool ExecuteCore(WindowInfo? target) {
        IntPtr h = new IntPtr(target!.Handle);
        UiaTreeResult tree = UiaService.DumpTree(h, MaxDepth, MaxNodes);
        JsonObject data = new() {
            ["window"] = target.ToJsonObject(),
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
