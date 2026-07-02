// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// An <see cref="AgentCommand"/> that targets a window with the shared selector set
/// (<c>window</c> title substring / <c>handle</c> / <c>process</c> / <c>classname</c> /
/// <c>foreground</c>). Hosts the five selector properties once (they were copy-pasted — and had
/// drifted, #200 — across seven commands), performs the single
/// <see cref="WindowResolver.Resolve(long?, string?, string?, string?, bool)"/> incantation, and
/// hands <see cref="ExecuteCore(WindowInfo?)"/> the resolved window.
///
/// Not every command resolves unconditionally: <c>click</c>/<c>drag</c> only need a window for
/// element endpoints, <c>capture</c> supports a window-less region grab, and <c>record</c> resolves
/// inside its (test-overridable) grabber factory — those override <see cref="RequiresWindowTarget"/>
/// and receive <c>null</c>. When resolution fails, <see cref="OnWindowNotFound"/> emits each
/// command's historical failure envelope (they differ — result unification is #206).
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
        WindowResolver.Resolve(Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);

    /// <summary>
    /// Emits the command's window-not-found failure and returns false. The base emission is the
    /// historical majority shape; commands that emit a coded envelope (or audit the miss) override.
    /// </summary>
    protected virtual bool OnWindowNotFound() {
        Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window").ToJson());
        return false;
    }

    /// <summary>Resolve-once template: resolve (when required), fail via <see cref="OnWindowNotFound"/>, dispatch.</summary>
    protected sealed override bool ExecuteCore() {
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
    /// <see cref="RequiresWindowTarget"/> returned false for this invocation.
    /// </summary>
    protected abstract bool ExecuteCore(WindowInfo? target);

    // No Clone override: the shared selectors are value/string-typed, so the base
    // MemberwiseClone-based Command.Clone (#207) copies them by construction.
}
