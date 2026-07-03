// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command: first-class top-level window DISCOVERY (issue #77). Where
/// <see cref="QueryCommand"/>/<see cref="FindCommand"/> resolve ONE window and then look inside it, this
/// enumerates the visible titled top-level windows themselves, so an agent that starts with only a
/// partial title, a process name, or the expectation that a window will appear can list the available
/// targets instead of guessing one. Filters (all optional) narrow the set with the SAME rules
/// <see cref="WindowResolver.Resolve"/> uses: <c>window</c> title substring (case-insensitive),
/// <c>process</c> name (exact, without .exe), <c>classname</c> (exact). With a <c>timeout</c> it WAITS,
/// polling until a matching top-level window appears or the timeout elapses.
///
/// Returns <c>{ count, windows: [ { handle, title, className, processName, processId, x, y, width, height }, … ] }</c>;
/// each array element is a full window OBJECT (the <see cref="WindowInfo"/> shape the other tools echo,
/// via <see cref="WindowInfo.ToJsonObject"/>), so a listed handle can be reused directly on a follow-up
/// <c>query</c>/<c>capture</c>/<c>invoke</c>.
///
/// SAFETY: a bare list (no filter, no timeout) enumerates ALL windows; that is discovery, not the
/// silent-arbitrary-target hazard <see cref="WindowResolver.Resolve"/> guards against (it never picks one
/// for you). But WAITING for "any window" IS that hazard (it would return whatever exists), so a
/// <c>timeout</c> with no filter is refused. Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and
/// audited (structurally, via <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class WindowsCommand : AgentCommand {
    // NOTE: attribute names MUST be all-lowercase (the .commands load XSLT lower-cases every name; a
    // camelCase attribute would never bind on load). Enforced by XmlNameCasingTests (#200).
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("classname")] public string ClassName { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new WindowsCommand { Cmd = "windows" }];
    }

    /// <summary>True when any discovery filter (title/process/class) is given.</summary>
    private bool HasFilter =>
        !string.IsNullOrEmpty(Window) || !string.IsNullOrEmpty(Process) || !string.IsNullOrEmpty(ClassName);

    protected override string AuditDetails() =>
        $"windows title='{Window}' process='{Process}' class='{ClassName}' timeout={Timeout}";

    protected override CommandResult ExecuteCore() {
        // Waiting for "any window" would return whatever happens to exist; the same silent-arbitrary-target
        // hazard the no-criteria Resolve refusal exists to prevent. Require a filter to WAIT. (A bare list
        // with no timeout is fine: it enumerates the whole set for the agent to choose from.)
        if (Timeout > 0 && !HasFilter) {
            return CommandResult.Fail(Cmd,
                "windows with a timeout needs at least one of window/process/className to wait for; " +
                "refusing to wait for an arbitrary window. Drop timeout to list all windows now, or add a filter.",
                "windows-no-criteria", "invalid-argument");
        }

        List<WindowInfo> windows = Timeout > 0
            ? WindowResolver.WaitForTopLevel(Window, Process, ClassName, Timeout)
            : WindowResolver.ListTopLevel(Window, Process, ClassName);

        JsonArray arr = [];
        foreach (WindowInfo w in windows) {
            arr.Add(w.ToJsonObject());
        }
        JsonObject data = new() {
            ["count"] = windows.Count,
            ["windows"] = arr,
        };
        // A wait that found nothing is an honest empty result (count:0), like a one-shot find miss; the
        // agent branches on count. It is NOT a failure, so it is not reclassified as a timeout error.
        return CommandResult.Ok(Cmd, data);
    }
}
