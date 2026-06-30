// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace MCEControl;

/// <summary>
/// UI Automation (UIA) observation and targeting for the MCEC 3.0 agent commands, backed by FlaUI.
/// Provides a depth-bounded tree dump (<see cref="DumpTree"/>), element lookup (<see cref="Find"/>),
/// and pattern dispatch (<see cref="Invoke"/>). Every UIA read is wrapped defensively: a single stale
/// or unsupported node must never abort the whole walk.
/// </summary>
public static class UiaService {
    /// <summary>
    /// Timeout for the element lookup an <c>invoke</c> performs before dispatching its action. It is kept
    /// below <see cref="AgentServer.InvokeModalGraceMs"/> on purpose: the grace assumes that an invoke
    /// still running afterward is blocked on a modal dialog, so the lookup itself must finish first — a
    /// missing element must fail fast (no-target) rather than be misreported as a pending modal (#107).
    /// Agents are instructed to <c>wait-for</c>/<c>find</c> a control before acting on it, so invoke does
    /// not need a long implicit wait of its own.
    /// </summary>
    public const int InvokeFindTimeoutMs = 500;

    /// <summary>
    /// Snapshots the UIA tree rooted at <paramref name="hwnd"/> down to <paramref name="maxDepth"/>
    /// levels (depth 0 = the root node only) and at most <paramref name="maxNodes"/> nodes. The node
    /// cap keeps the result size bounded and stable for agent reasoning on pathological trees (e.g. a
    /// virtualized list with thousands of items); when it bites, <see cref="UiaTreeResult.Truncated"/>
    /// is set so the caller can warn rather than silently return a clipped tree. <paramref name="maxNodes"/>
    /// &lt;= 0 means unbounded. Returns a result with a null root if the window can't be attached to.
    /// </summary>
    public static UiaTreeResult DumpTree(IntPtr hwnd, int maxDepth, int maxNodes) {
        if (hwnd == IntPtr.Zero) {
            return new UiaTreeResult(null, 0, false);
        }
        try {
            using UIA3Automation automation = new();
            AutomationElement root = automation.FromHandle(hwnd);
            UiaTreeBudget budget = new() { MaxNodes = maxNodes <= 0 ? int.MaxValue : maxNodes };
            // Walk children one at a time with the control-view walker rather than materializing every
            // child up front: a container with thousands of immediate children must not be fully
            // enumerated just to emit a handful before the node cap bites (#109).
            ITreeWalker walker = automation.TreeWalkerFactory.GetControlViewWalker();
            UiaElementInfo node = BuildNode(root, 0, maxDepth, budget, walker);
            return new UiaTreeResult(node, budget.Count, budget.Truncated);
        }
        catch (Exception e) {
            // FromHandle/UIA can throw COMException or FlaUI-specific exceptions on a window that
            // closes mid-call; never let it escape the command's Execute().
            Logger.Instance.Log4.Error($"UiaService.DumpTree failed: {e.Message}");
            return new UiaTreeResult(null, 0, false);
        }
    }

    /// <summary>
    /// Finds the first descendant of <paramref name="hwnd"/> matching <paramref name="value"/> by the
    /// given strategy (<c>name</c>/<c>automationid</c>/<c>classname</c>, case-insensitive). When
    /// <paramref name="timeoutMs"/> &gt; 0 it retries until found or the timeout elapses. Returns a
    /// single node (no children) or null.
    /// </summary>
    public static UiaElementInfo? Find(IntPtr hwnd, string by, string value, int timeoutMs) {
        if (hwnd == IntPtr.Zero) {
            return null;
        }
        try {
            using UIA3Automation automation = new();
            AutomationElement root = automation.FromHandle(hwnd);
            AutomationElement? el = FindElement(root, by, value, timeoutMs);
            return el is null ? null : Describe(el);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"UiaService.Find failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds an element (5s timeout) and dispatches <paramref name="action"/>
    /// (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>).
    /// Returns true on success, false if the element wasn't found or the required pattern is
    /// unsupported. <c>expand</c> opens a collapsed menu/treeitem so its children become reachable —
    /// a closed WinForms menu's sub-items are not in the UIA tree until the parent is opened. It uses
    /// the ExpandCollapse pattern, falling back to Invoke for WinForms menu items that lack it.
    /// </summary>
    public static bool Invoke(IntPtr hwnd, string by, string value, string action, string? text) {
        if (hwnd == IntPtr.Zero) {
            return false;
        }
        // Reject an unknown/typo action (e.g. "click", "set-value") rather than silently activating the
        // element via the default Invoke pattern. The caller defaults a missing action to "invoke".
        if (!IsSupportedAction(action)) {
            Logger.Instance.Log4.Warn($"UiaService.Invoke: unsupported action '{action}' — rejected.");
            return false;
        }
        try {
            using UIA3Automation automation = new();
            AutomationElement root = automation.FromHandle(hwnd);
            AutomationElement? el = FindElement(root, by, value, InvokeFindTimeoutMs);
            if (el is null) {
                return false;
            }
            return action.ToLowerInvariant() switch {
                "invoke" => InvokeDefault(el),
                "toggle" => InvokeToggle(el),
                "setvalue" => InvokeSetValue(el, text),
                "setfocus" => InvokeSetFocus(el),
                "expand" => InvokeExpand(el),
                "collapse" => InvokeCollapse(el),
                _ => false,
            };
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"UiaService.Invoke failed: {e.Message}");
            return false;
        }
    }

    /// <summary>True if <paramref name="action"/> is one of the supported invoke actions
    /// (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>, case-insensitive).</summary>
    public static bool IsSupportedAction(string action) =>
        action?.ToLowerInvariant() switch {
            "invoke" or "toggle" or "setvalue" or "setfocus" or "expand" or "collapse" => true,
            _ => false,
        };

    private static UiaElementInfo BuildNode(AutomationElement el, int depth, int maxDepth, UiaTreeBudget budget, ITreeWalker walker) {
        budget.Count++;
        UiaElementInfo info = Describe(el);
        if (depth >= maxDepth) {
            return info;
        }

        AutomationElement? child;
        try {
            child = walker.GetFirstChild(el);
        }
        catch (COMException) {
            // Element went stale; return what we have so far.
            return info;
        }

        while (child is not null) {
            if (budget.Count >= budget.MaxNodes) {
                // Hit the node cap: stop expanding and flag it so the caller surfaces a warning. Because
                // we never materialized the remaining siblings, the cap also bounds the UIA enumeration.
                budget.Truncated = true;
                break;
            }
            try {
                info.Children.Add(BuildNode(child, depth + 1, maxDepth, budget, walker));
            }
            catch (COMException) {
                // Skip a node that went stale mid-walk; keep the rest of the tree.
            }
            try {
                child = walker.GetNextSibling(child);
            }
            catch (COMException) {
                // Can't advance past a stale node; stop this level with what we have.
                break;
            }
        }
        return info;
    }

    private static AutomationElement? FindElement(AutomationElement root, string by, string value, int timeoutMs) {
        Func<ConditionFactory, ConditionBase> condition = BuildCondition(by, value);
        if (timeoutMs > 0) {
            return Retry.WhileNull(
                () => root.FindFirstDescendant(condition),
                TimeSpan.FromMilliseconds(timeoutMs)).Result;
        }
        return root.FindFirstDescendant(condition);
    }

    private static Func<ConditionFactory, ConditionBase> BuildCondition(string by, string value) =>
        by.ToLowerInvariant() switch {
            "automationid" => cf => cf.ByAutomationId(value),
            "classname" => cf => cf.ByClassName(value),
            _ => cf => cf.ByName(value),
        };

    private static bool InvokeDefault(AutomationElement el) {
        var pattern = el.Patterns.Invoke.PatternOrDefault;
        if (pattern is null) {
            return false;
        }
        pattern.Invoke();
        return true;
    }

    private static bool InvokeToggle(AutomationElement el) {
        var pattern = el.Patterns.Toggle.PatternOrDefault;
        if (pattern is null) {
            return false;
        }
        pattern.Toggle();
        return true;
    }

    private static bool InvokeSetValue(AutomationElement el, string? text) {
        var pattern = el.Patterns.Value.PatternOrDefault;
        if (pattern is null) {
            return false;
        }
        pattern.SetValue(text ?? string.Empty);
        return true;
    }

    private static bool InvokeSetFocus(AutomationElement el) {
        el.Focus();
        return true;
    }

    private static bool InvokeExpand(AutomationElement el) {
        var pattern = el.Patterns.ExpandCollapse.PatternOrDefault;
        if (pattern is not null) {
            pattern.Expand();
            return true;
        }
        // WinForms menu items (ToolStripMenuItem) open their dropdown via the Invoke pattern and do
        // not expose ExpandCollapse. Fall back to Invoke so `expand` opens menus uniformly across
        // WinForms and WPF/WinUI — letting an agent reach sub-items that aren't yet in the tree.
        var invoke = el.Patterns.Invoke.PatternOrDefault;
        if (invoke is not null) {
            invoke.Invoke();
            return true;
        }
        return false;
    }

    private static bool InvokeCollapse(AutomationElement el) {
        var pattern = el.Patterns.ExpandCollapse.PatternOrDefault;
        if (pattern is null) {
            return false;
        }
        pattern.Collapse();
        return true;
    }

    private static UiaElementInfo Describe(AutomationElement el) {
        UiaElementInfo info = new();

        try {
            info.ControlType = el.Properties.ControlType.ValueOrDefault.ToString();
        }
        catch (COMException) {
            // Leave the default control type.
        }

        try {
            info.Name = CleanString(el.Properties.Name.ValueOrDefault);
        }
        catch (COMException) {
            // Property unavailable on a stale node; leave null.
        }

        try {
            info.AutomationId = CleanString(el.Properties.AutomationId.ValueOrDefault);
        }
        catch (COMException) {
            // Leave null.
        }

        try {
            info.ClassName = CleanString(el.Properties.ClassName.ValueOrDefault);
        }
        catch (COMException) {
            // Leave null.
        }

        try {
            info.IsEnabled = el.Properties.IsEnabled.ValueOrDefault;
        }
        catch (COMException) {
            // Leave false.
        }

        try {
            info.IsOffscreen = el.Properties.IsOffscreen.ValueOrDefault;
        }
        catch (COMException) {
            // Leave false.
        }

        try {
            Rectangle r = el.Properties.BoundingRectangle.ValueOrDefault;
            info.X = r.X;
            info.Y = r.Y;
            info.Width = r.Width;
            info.Height = r.Height;
        }
        catch (COMException) {
            // Leave zeroed bounds.
        }

        try {
            info.Value = CleanString(el.Patterns.Value.PatternOrDefault?.Value?.ValueOrDefault);
        }
        catch (COMException) {
            // Leave null.
        }

        return info;
    }

    private static string? CleanString(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
