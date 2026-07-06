// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent observation command: first-class top-level window DISCOVERY and window-WAIT predicates
/// (issues #77, #112). Where <see cref="QueryCommand"/>/<see cref="FindCommand"/> resolve ONE window and
/// then look inside it, this enumerates the visible titled top-level windows themselves, and can WAIT for
/// one to change state, so an agent that starts with only a partial title, a process name, or the
/// expectation that a window will appear/close/take focus can act with a single call instead of polling.
/// Filters (all optional for discovery) narrow the set with the SAME rules <see cref="WindowResolver.Resolve"/>
/// uses: <c>window</c> title substring (case-insensitive), <c>process</c> name (exact, without .exe),
/// <c>classname</c> (exact).
///
/// With a <c>timeout</c> it WAITS; a <c>timeout</c> of 0 is a one-shot check. <c>condition</c> selects the
/// predicate (default <c>appears</c>):
/// <list type="bullet">
///   <item><c>appears</c>: until a matching window exists; returns the matches (a wait also reports
///     <c>satisfied</c>, and <c>count:0</c> on timeout).</item>
///   <item><c>disappears</c>: until NO window matches (a dialog/app closed); <c>satisfied</c> true when
///     gone, and <c>windows</c> lists whatever is still present.</item>
///   <item><c>foreground</c>: until the foreground window matches the filter; a satisfied result carries
///     the matched <c>window</c>, an unsatisfied one carries <c>foreground</c> (what IS foreground now).</item>
/// </list>
/// A WAIT that times out unsatisfied carries actionable diagnostics (#112): <c>waitedFor</c> (the criteria)
/// and <c>lastObservedWindows</c> (the full top-level set), so a timeout is triageable without re-querying.
/// A one-shot check (<c>timeout:0</c>) that misses returns <c>satisfied:false</c> alone, no wait diagnostics.
///
/// Returns <c>{ condition, count, windows: [ { handle, title, className, processName, processId, x, y, width, height }, … ] }</c>
/// for <c>appears</c>/<c>disappears</c> (a wait adds <c>satisfied</c>; each element the <see cref="WindowInfo"/>
/// shape via <see cref="WindowInfo.ToJsonObject"/>, so a listed handle is reusable directly), and
/// <c>{ condition, satisfied, window|foreground }</c> for <c>foreground</c>.
///
/// SAFETY: a bare list (no filter, no timeout) enumerates ALL windows; that is discovery, not the
/// silent-arbitrary-target hazard <see cref="WindowResolver.Resolve"/> guards against (it never picks one
/// for you). But WAITING for "any window" IS that hazard, and <c>disappears</c>/<c>foreground</c> are
/// predicates ABOUT a specific window, so a wait (or either of those conditions) with no filter is refused.
/// Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class WindowsCommand : AgentCommand {
    // NOTE: attribute names MUST be all-lowercase (the .commands load XSLT lower-cases every name; a
    // camelCase attribute would never bind on load). Enforced by XmlNameCasingTests (#200).
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("classname")] public string ClassName { get; set; } = null!;
    [XmlAttribute("condition")] public string Condition { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new WindowsCommand { Cmd = "windows" }];
    }

    /// <summary>True when any discovery filter (title/process/class) is given.</summary>
    private bool HasFilter =>
        !string.IsNullOrEmpty(Window) || !string.IsNullOrEmpty(Process) || !string.IsNullOrEmpty(ClassName);

    /// <summary>The requested predicate, normalized; <c>appears</c> when omitted.</summary>
    private string ConditionOrDefault =>
        string.IsNullOrWhiteSpace(Condition) ? "appears" : Condition.Trim().ToLowerInvariant();

    protected override string AuditDetails() =>
        $"windows title='{Window}' process='{Process}' class='{ClassName}' condition='{ConditionOrDefault}' timeout={Timeout}";

    protected override CommandResult ExecuteCore() {
        string condition = ConditionOrDefault;
        if (condition is not ("appears" or "disappears" or "foreground")) {
            return CommandResult.Fail(Cmd,
                $"windows condition '{Condition}' is not recognized; use appears (default), disappears, or foreground.",
                "windows-unknown-condition", "invalid-argument");
        }

        bool waiting = Timeout > 0;

        // disappears/foreground are predicates ABOUT a specific window, so they always need a filter.
        // appears needs a filter only when WAITING (a bare list enumerates the whole set to choose from);
        // waiting for "any window" is the silent-arbitrary-target hazard the no-criteria Resolve refuses.
        bool needsFilter = condition != "appears" || waiting;
        if (needsFilter && !HasFilter) {
            return CommandResult.Fail(Cmd,
                $"windows condition '{condition}' needs at least one of window/process/className to identify the " +
                "window; refusing to act on an arbitrary window. For 'appears', drop timeout to list all windows now.",
                "windows-no-criteria", "invalid-argument");
        }

        return condition switch {
            "disappears" => Disappears(),
            "foreground" => Foreground(),
            _ => Appears(waiting),
        };
    }

    private CommandResult Appears(bool waiting) {
        List<WindowInfo> matches = waiting
            ? WindowResolver.WaitForTopLevel(Window, Process, ClassName, Timeout)
            : WindowResolver.ListTopLevel(Window, Process, ClassName);

        JsonObject data = new() {
            ["condition"] = "appears",
            ["count"] = matches.Count,
            ["windows"] = ToArray(matches),
        };
        // A wait that found nothing is an honest miss (count:0), like a one-shot find miss; not a failure.
        if (waiting) {
            bool satisfied = matches.Count > 0;
            data["satisfied"] = satisfied;
            if (!satisfied) {
                AddTimeoutDiagnostics(data);
            }
        }
        return CommandResult.Ok(Cmd, data);
    }

    private CommandResult Disappears() {
        List<WindowInfo> remaining = Timeout > 0
            ? WindowResolver.WaitUntilGone(Window, Process, ClassName, Timeout)
            : WindowResolver.ListTopLevel(Window, Process, ClassName);
        bool gone = remaining.Count == 0;

        JsonObject data = new() {
            ["condition"] = "disappears",
            ["satisfied"] = gone,
            ["count"] = remaining.Count,
            ["windows"] = ToArray(remaining), // still-present matches; empty on success
        };
        // Wait diagnostics only when a WAIT actually timed out; a one-shot check (timeout:0) stays lean.
        if (!gone && Timeout > 0) {
            AddTimeoutDiagnostics(data);
        }
        return CommandResult.Ok(Cmd, data);
    }

    private CommandResult Foreground() {
        WindowInfo? match = Timeout > 0
            ? WindowResolver.WaitForForeground(Window, Process, ClassName, Timeout)
            : ForegroundIfMatches();
        bool satisfied = match is not null;

        JsonObject data = new() {
            ["condition"] = "foreground",
            ["satisfied"] = satisfied,
        };
        if (satisfied) {
            data["window"] = match!.ToJsonObject();
        }
        else {
            WindowInfo? current = WindowResolver.Resolve(null, null, null, null, foreground: true);
            data["foreground"] = current?.ToJsonObject(); // what IS foreground now (may be null)
            // Wait diagnostics only when a WAIT actually timed out; a one-shot check (timeout:0) stays lean.
            if (Timeout > 0) {
                AddTimeoutDiagnostics(data);
            }
        }
        return CommandResult.Ok(Cmd, data);
    }

    private WindowInfo? ForegroundIfMatches() {
        WindowInfo? fg = WindowResolver.Resolve(null, null, null, null, foreground: true);
        return fg is not null && WindowResolver.Matches(fg, Window, Process, ClassName) ? fg : null;
    }

    private static JsonArray ToArray(List<WindowInfo> windows) {
        JsonArray arr = [];
        foreach (WindowInfo w in windows) {
            arr.Add(w.ToJsonObject());
        }
        return arr;
    }

    // Actionable timeout diagnostics (#112): echo what was waited for and the full last-observed top-level
    // window set, so an agent can triage a timeout without issuing a second observation.
    private void AddTimeoutDiagnostics(JsonObject data) {
        // Echo all three criteria (null for the ones not applied) so a triaging agent sees exactly what
        // was waited for, plus the current full top-level set.
        JsonObject waitedFor = new() {
            ["window"] = NullIfEmpty(Window),
            ["process"] = NullIfEmpty(Process),
            ["className"] = NullIfEmpty(ClassName),
        };
        data["waitedFor"] = waitedFor;
        data["lastObservedWindows"] = ToArray(WindowResolver.EnumerateTopLevel());
    }

    private static JsonValue? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : JsonValue.Create(value);
}
