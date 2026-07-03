// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// How a UIA attach/read/dispatch failed, classified from the exception it threw (#261). Produced by
/// <see cref="UiaService.ClassifyUiaFailure"/> and carried on <see cref="UiaFindOutcome"/> and
/// <see cref="UiaTreeResult"/> so commands can map real UIA failure modes onto the closed
/// <see cref="AgentErrorCategory"/> taxonomy (stale-element, elevation) instead of collapsing
/// everything into a null result that reads as "not found".
/// </summary>
public enum UiaFailureKind {
    /// <summary>No failure.</summary>
    None = 0,

    /// <summary>The window or element is no longer available (closed, torn down, re-rendered);
    /// UIA_E_ELEMENTNOTAVAILABLE or FlaUI's ElementNotAvailableException. Maps to <c>stale-element</c>.</summary>
    WindowGone,

    /// <summary>UIA was denied access to the target (E_ACCESSDENIED), which for a valid window almost
    /// always means the target process runs at a higher integrity level (UAC) than MCEC (UIPI blocks
    /// the client). Maps to <c>elevation</c>.</summary>
    AccessDenied,

    /// <summary>Any other UIA fault. Maps to <c>internal</c>.</summary>
    Faulted,
}
