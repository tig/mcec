// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The outcome of a <see cref="UiaService.Invoke"/> pattern dispatch (#206). The old bare bool
/// conflated four distinct failures, so an agent could not tell "the element is not there yet"
/// (re-find/wait) from "the element exists but cannot do that action" (change the action; re-finding
/// loops forever) from "the action name is a typo" (fix the argument) from "UIA faulted" (report it).
/// <see cref="InvokeCommand"/> maps each member to a distinct error code/category.
/// </summary>
public enum UiaInvokeResult {
    /// <summary>The action was dispatched to the element's pattern successfully.</summary>
    Ok,

    /// <summary>No element matched the selector within the lookup timeout (or the window handle was
    /// invalid). Recovery: <c>wait-for</c>/<c>find</c> the element, then retry.</summary>
    ElementNotFound,

    /// <summary>The element WAS found but does not support the UIA pattern the action needs
    /// (e.g. <c>toggle</c> on a plain button). Recovery: choose a different action or click it;
    /// re-finding the same element cannot help.</summary>
    PatternUnsupported,

    /// <summary>The action string is not one of the supported actions (a typo like <c>click</c> or
    /// <c>set-value</c>). Nothing was looked up or dispatched. Recovery: fix the argument.</summary>
    ActionUnknown,

    /// <summary>The selector matched MORE than one element and the lookup refused to guess (#261).
    /// Nothing was dispatched. Recovery: narrow the selector (automationId/className or a more
    /// specific name); retrying the same selector cannot help.</summary>
    ElementAmbiguous,

    /// <summary>The element or its window went away between lookup and dispatch, or the window handle
    /// no longer resolves (UIA_E_ELEMENTNOTAVAILABLE, #261). Recovery: re-<c>query</c>/<c>find</c> for
    /// a fresh target, then retry.</summary>
    ElementStale,

    /// <summary>UIA was denied access to the target (E_ACCESSDENIED, #261): for a valid window this
    /// means the target runs at a higher integrity level (UAC) than MCEC and cannot be driven.
    /// Not agent-recoverable; surface it to the operator.</summary>
    TargetElevated,

    /// <summary>UIA threw while attaching, finding, or dispatching (COM fault not classified as stale
    /// or elevation). Not agent-recoverable beyond re-observing the target.</summary>
    Faulted,

    /// <summary>A <c>setfocus</c> dispatched but the element did NOT end up with keyboard focus
    /// (verified via <c>HasKeyboardFocus</c>, #91/#270): e.g. a container that redirects focus, or a
    /// surface a bare UIA SetFocus cannot focus. Maps to the <c>focus</c> category. Recovery: use the
    /// <c>focus</c> tool (it clicks to place focus first), or <c>click</c> the control directly.</summary>
    FocusNotSet,
}
