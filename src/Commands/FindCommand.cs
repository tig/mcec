// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command handling both <c>find</c> (one-shot) and <c>wait-for</c> (polls until a
/// timeout). Resolves a target window, then locates a single UIA element by name/automationid/
/// classname. Always replies success with a <c>found</c> flag (a miss is not an error). Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class FindCommand : WindowTargetingAgentCommand {
    [XmlAttribute("by")] public string By { get; set; } = "name";
    [XmlAttribute("value")] public string Value { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static List<Command> BuiltInCommands {
        get => [
            new FindCommand { Cmd = "find" },
            new FindCommand { Cmd = "wait-for" },
        ];
    }

    /// <summary>The effective poll timeout: <c>wait-for</c> defaults to 5000ms when none is given.</summary>
    private int EffectiveTimeout =>
        string.Equals(Cmd, "wait-for", StringComparison.OrdinalIgnoreCase) && Timeout <= 0 ? 5000 : Timeout;

    protected override string AuditDetails() =>
        $"find by={By} value='{Value}' timeout={EffectiveTimeout} window handle={Handle} title='{Window}' process='{Process}'";

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        IntPtr h = new IntPtr(target!.Handle);
        UiaElementInfo? info = UiaService.Find(h, By, Value, EffectiveTimeout);
        bool found = info is not null;
        JsonObject data = new() {
            ["found"] = found,
            ["element"] = info?.ToJsonObject(),
            // Echo the resolved window so the session can record it as the active target (a find/wait-for
            // that establishes the current control feeds error.lastObservation for a later failing action).
            ["window"] = target.ToJsonObject(),
        };
        return CommandResult.Ok(Cmd, data);
    }
}
