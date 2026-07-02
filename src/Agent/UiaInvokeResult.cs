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

    /// <summary>UIA threw while attaching, finding, or dispatching (window closed mid-call, COM
    /// fault). Not agent-recoverable beyond re-observing the target.</summary>
    Faulted,
}
