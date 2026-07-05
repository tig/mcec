// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The <b>closed</b> failure taxonomy for every MCEC 3.0 agent tool result, owned by the contract in
/// <c>docs/design/agent-tool-result-contract.md</c> (#101). An agent may branch exhaustively on this
/// set; new failure modes are mapped onto an existing member rather than added ad hoc. The wire form
/// (the kebab-case string that goes into <c>error.category</c>) is produced by
/// <see cref="AgentError.CategoryWire"/>.
/// </summary>
public enum AgentErrorCategory {
    /// <summary>A wait/poll (e.g. <c>wait-for</c>) expired before its condition held.</summary>
    Timeout,

    /// <summary>A selector matched more than one candidate and the tool refused to guess.</summary>
    AmbiguousSelector,

    /// <summary>A previously resolved element/handle is no longer valid (window closed, tree re-rendered).</summary>
    StaleElement,

    /// <summary>A selector matched nothing (no window/element).</summary>
    NoTarget,

    /// <summary>The request itself was malformed or inapplicable; a client-supplied argument was
    /// invalid (unknown action, oversized region, ill-formed endpoint) or cannot apply to the target.
    /// Recovery is to FIX the arguments, not to retry the same call or broaden a selector (#191).</summary>
    InvalidArgument,

    /// <summary>A screenshot was produced but detected as black/blank.</summary>
    CaptureBlank,

    /// <summary>An action required keyboard focus and it could not be confirmed on the target. Produced
    /// (#91/#270) by the <c>focus</c> tool when a window is foreground but no control took focus
    /// (<see cref="FocusService.IsFocusInWindow"/>), and by <c>invoke setfocus</c> when the element does
    /// not end up with <c>HasKeyboardFocus</c> (<see cref="UiaInvokeResult.FocusNotSet"/>).</summary>
    Focus,

    /// <summary>The target runs at a higher integrity level (UAC) than MCEC and cannot be driven.
    /// Produced (#261) when a UIA attach/read/dispatch on a valid window fails with E_ACCESSDENIED
    /// (UIPI); see <see cref="UiaService.ClassifyUiaFailure"/>.</summary>
    Elevation,

    /// <summary>An action required the target to be foreground and it could not be brought forward.
    /// Produced (#91/#270) by the <c>focus</c> tool when <see cref="FocusService.BringToForeground"/>
    /// asks Windows to activate the target and <c>GetForegroundWindow</c> confirms it did not land
    /// (foreground lock, a modal on another app, a full-screen exclusive window).</summary>
    Foreground,

    /// <summary>An unexpected MCEC-side fault (bug, unhandled exception) or a policy refusal the agent
    /// cannot recover from on its own.</summary>
    Internal,
}
