// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// An <see cref="AgentCommand"/> that targets a window with the shared selector set
/// (<c>window</c> title substring / <c>handle</c> / <c>process</c> / <c>classname</c> /
/// <c>foreground</c>). Hosts the five selector properties once (they were copy-pasted; and had
/// drifted, #200; across seven commands), performs the single
/// <see cref="WindowResolver.Resolve(long?, string?, string?, string?, bool)"/> incantation, and
/// hands <see cref="ExecuteCore(WindowInfo?)"/> the resolved window.
///
/// Not every command resolves unconditionally: <c>click</c>/<c>drag</c> only need a window for
/// element endpoints, <c>capture</c> supports a window-less region grab, and <c>record</c> resolves
/// inside its (test-overridable) grabber factory; those override <see cref="RequiresWindowTarget"/>
/// and receive <c>null</c>. When resolution fails, <see cref="OnWindowNotFound"/> returns the ONE
/// structured window-not-found failure (#206 unified the per-command shapes); a command overrides it
/// only to add behavior (e.g. <c>capture</c> audits the miss).
/// </summary>
public abstract class WindowTargetingAgentCommand : AgentCommand {
    // NOTE: attribute names MUST be all-lowercase. SerializedCommands.Deserialize runs an XSLT that
    // lower-cases every element/attribute name before deserializing, so a camelCase name would never
    // bind on load and the value would be silently lost. Enforced for every Command by
    // XmlNameCasingTests (#200).
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("handle")] public long Handle { get; set; }
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("classname")] public string ClassName { get; set; } = null!;
    [XmlAttribute("foreground")] public bool Foreground { get; set; }

    /// <summary>True when any window selector (title/handle/process/class/foreground) is given.</summary>
    protected bool HasWindowTarget => !string.IsNullOrEmpty(Window)
        || Handle > 0
        || !string.IsNullOrEmpty(Process)
        || !string.IsNullOrEmpty(ClassName)
        || Foreground;

    /// <summary>
    /// Whether this invocation needs a resolved window before <see cref="ExecuteCore(WindowInfo?)"/>
    /// runs. Defaults to true (query/find/invoke always target a window); commands with window-less
    /// modes (pixel click/drag, region capture, record's grabber-owned resolution) override this.
    /// </summary>
    protected virtual bool RequiresWindowTarget => true;

    /// <summary>Resolves the target window from the shared selectors. Null when nothing matches.</summary>
    protected WindowInfo? ResolveTargetWindow() =>
        WindowResolver.Resolve(Handle > 0 ? Handle : null, Window, Process, ClassName, Foreground);

    /// <summary>
    /// Builds the command's window-not-found failure: one structured shape for every window-targeting
    /// command (#206); code <c>window-not-found</c>, category <c>no-target</c>. Commands override
    /// only to ADD behavior (e.g. <c>capture</c> audits the miss), not to change the envelope.
    /// </summary>
    protected virtual CommandResult OnWindowNotFound() =>
        CommandResult.Fail(Cmd, "No matching window", "window-not-found", "no-target");

    /// <summary>Resolve-once template: resolve (when required), fail via <see cref="OnWindowNotFound"/>, dispatch.</summary>
    protected sealed override CommandResult ExecuteCore() {
        if (!RequiresWindowTarget) {
            return ExecuteCore(null);
        }

        WindowInfo? win = ResolveTargetWindow();
        if (win is null) {
            return OnWindowNotFound();
        }

        return ExecuteCore(win);
    }

    /// <summary>
    /// The command body. <paramref name="target"/> is the resolved window, or <c>null</c> only when
    /// <see cref="RequiresWindowTarget"/> returned false for this invocation. Returns the structured
    /// result; the <see cref="AgentCommand"/> template owns emitting it.
    /// </summary>
    protected abstract CommandResult ExecuteCore(WindowInfo? target);

    /// <summary>
    /// Maps a non-miss <see cref="UiaFindOutcome"/> failure onto the ONE structured shape every
    /// element-resolving command shares (#261): ambiguous selector, then the classified UIA faults
    /// (<see cref="UiaFailureFor"/>). Returns null when the lookup did not fail this way (found, or a
    /// clean miss); a miss stays per-command because the commands disagree about it (<c>find</c>
    /// reports <c>found:false</c> success, <c>click</c>/<c>drag</c>/<c>invoke</c> fail with
    /// <c>no-target</c>).
    /// </summary>
    protected static CommandResult? UiaFindFailureFor(string cmd, string by, string value, UiaFindOutcome outcome) =>
        outcome.Ambiguous
            ? CommandResult.Fail(cmd,
                $"The selector ({by}='{value}') matched {outcome.MatchCount} elements; refusing to guess which one. " +
                "Narrow it: prefer automationId, or add className or a more specific name.",
                $"selector-matched-{outcome.MatchCount}", "ambiguous-selector")
            : UiaFailureFor(cmd, outcome.Failure);

    /// <summary>
    /// Maps a classified UIA fault (#261) onto the shared structured failure: window gone maps to
    /// <c>window-closed</c>/<c>stale-element</c>, access denied to <c>target-elevated</c>/<c>elevation</c>
    /// (for a valid window, UIA's E_ACCESSDENIED means UIPI blocked a lower-integrity client), and
    /// anything else to <c>uia-faulted</c>/<c>internal</c>. Null when there was no fault.
    /// </summary>
    protected static CommandResult? UiaFailureFor(string cmd, UiaFailureKind failure) => failure switch {
        UiaFailureKind.WindowGone => CommandResult.Fail(cmd,
            "The target window or element went away mid-call (closed or re-rendered). " +
            "Re-query for a fresh window handle, then retry.",
            "window-closed", "stale-element"),
        UiaFailureKind.AccessDenied => CommandResult.Fail(cmd,
            "UI Automation was denied access to the target; it runs at a higher integrity level (UAC) than MCEC " +
            "and cannot be observed or driven. Surface this to the operator; do not retry.",
            "target-elevated", "elevation"),
        UiaFailureKind.Faulted => CommandResult.Fail(cmd,
            "UI Automation faulted while attaching to or reading the target. Re-observe the window.",
            "uia-faulted", "internal"),
        _ => null,
    };

    // No Clone override: the shared selectors are value/string-typed, so the base
    // MemberwiseClone-based Command.Clone (#207) copies them by construction.
}
