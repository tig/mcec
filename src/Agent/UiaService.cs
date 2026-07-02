// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace MCEControl;

/// <summary>
/// UI Automation (UIA) observation and targeting for the MCEC 3.0 agent commands, backed by FlaUI.
/// Provides a depth-bounded tree dump (<see cref="DumpTree"/>), element lookup (<see cref="Find"/>),
/// and pattern dispatch (<see cref="Invoke"/>). Every UIA read is wrapped defensively: a single stale
/// or unsupported node must never abort the whole walk.
///
/// <para>THREADING (#215): all UIA tree access runs on ONE dedicated MTA worker thread owned by this
/// service, with a single cached <see cref="UIA3Automation"/> instance (created on the worker,
/// disposed by <see cref="Shutdown"/>). Before, each call constructed and disposed its own
/// <see cref="UIA3Automation"/> on whatever thread it arrived on — HTTP pool workers, stdio workers,
/// the invoke grace thread, and (pre-#195, via legacy TCP) even the STA UI thread, a classic
/// self-deadlock when MCEC drives its own window. The worker centralizes the apartment; a debug
/// assertion (<see cref="AssertNotOnMessageLoopThread"/>) documents and enforces that UIA work never
/// arrives from a thread running a WinForms message loop. ONE deliberate exception: an
/// <c>invoke</c>'s pattern dispatch runs on its calling thread (the executor's per-invoke
/// modal-grace worker, #105), NOT on the shared worker — a modal-opening Invoke blocks until the
/// dialog closes, and parking the shared worker there would block the very <c>query</c>/<c>capture</c>
/// the agent needs to read or dismiss that dialog. UIA3's client objects are free-threaded, so an
/// element resolved on the worker is safely driven from another MTA thread.</para>
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
    /// Interval between lookup attempts while a timed <see cref="Find"/> (or invoke's bounded lookup)
    /// polls for an element. Each ATTEMPT runs as one short item on the shared UIA worker; the sleep
    /// between attempts happens on the calling thread, so a long <c>wait-for</c> never monopolizes the
    /// worker — concurrent <c>query</c>/<c>capture</c> items interleave between polls (#215).
    /// </summary>
    public const int FindPollIntervalMs = 100;

    /// <summary>Bound on how long <see cref="Shutdown"/> waits for the worker to finish its last item.</summary>
    public const int ShutdownJoinTimeoutMs = 5_000;

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
            return RunOnWorker(automation => {
                AutomationElement root = automation.FromHandle(hwnd);
                UiaTreeBudget budget = new() { MaxNodes = maxNodes <= 0 ? int.MaxValue : maxNodes };
                // Walk children one at a time with the control-view walker rather than materializing every
                // child up front: a container with thousands of immediate children must not be fully
                // enumerated just to emit a handful before the node cap bites (#109).
                ITreeWalker walker = automation.TreeWalkerFactory.GetControlViewWalker();
                UiaElementInfo node = BuildNode(root, 0, maxDepth, budget, walker);
                return new UiaTreeResult(node, budget.Count, budget.Truncated);
            });
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
            AutomationElement? el = FindWithPolling(hwnd, by, value, timeoutMs);
            return el is null ? null : RunOnWorker(_ => Describe(el));
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"UiaService.Find failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds an element (a short <see cref="InvokeFindTimeoutMs"/> lookup — invoke fast-fails rather
    /// than waiting; see that constant) and dispatches <paramref name="action"/>
    /// (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>/<c>select</c>).
    /// Returns a <see cref="UiaInvokeResult"/> that keeps the four failure modes distinct (#206) — the
    /// old bare bool made an agent re-find elements that existed but lacked the pattern, i.e. loop.
    /// <c>expand</c> opens a collapsed menu/treeitem so its children become reachable —
    /// a closed WinForms menu's sub-items are not in the UIA tree until the parent is opened. It uses
    /// the ExpandCollapse pattern, falling back to Invoke for WinForms menu items that lack it.
    /// </summary>
    public static UiaInvokeResult Invoke(IntPtr hwnd, string by, string value, string action, string? text) {
        if (hwnd == IntPtr.Zero) {
            return UiaInvokeResult.ElementNotFound;
        }
        // Reject an unknown/typo action (e.g. "click", "set-value") rather than silently activating the
        // element via the default Invoke pattern. The caller defaults a missing action to "invoke".
        if (!IsSupportedAction(action)) {
            Logger.Instance.Log4.Warn($"UiaService.Invoke: unsupported action '{action}' — rejected.");
            return UiaInvokeResult.ActionUnknown;
        }
        try {
            // The lookup runs (poll-by-poll) on the shared UIA worker like every other tree access...
            AutomationElement? el = FindWithPolling(hwnd, by, value, InvokeFindTimeoutMs);
            if (el is null) {
                return UiaInvokeResult.ElementNotFound;
            }
            // ...but the PATTERN DISPATCH runs here, on the calling thread — the executor's per-invoke
            // modal-grace worker (#105) — deliberately NOT on the shared worker: an Invoke that opens a
            // modal dialog blocks synchronously until the dialog closes, and a wedged shared worker
            // would block the query/capture the agent needs to read or dismiss that very dialog. The
            // caller is a message-loop-free MTA thread (asserted), and UIA3 client objects are
            // free-threaded, so driving the worker-resolved element from here is safe.
            AssertNotOnMessageLoopThread();
            bool dispatched = action.ToLowerInvariant() switch {
                "invoke" => InvokeDefault(el),
                "toggle" => InvokeToggle(el),
                "setvalue" => InvokeSetValue(el, text),
                "setfocus" => InvokeSetFocus(el),
                "expand" => InvokeExpand(el),
                "collapse" => InvokeCollapse(el),
                "select" => InvokeSelect(el),
                _ => false, // unreachable: IsSupportedAction gated above
            };
            // The element exists; the only way a dispatcher returns false is a missing UIA pattern.
            return dispatched ? UiaInvokeResult.Ok : UiaInvokeResult.PatternUnsupported;
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"UiaService.Invoke failed: {e.Message}");
            return UiaInvokeResult.Faulted;
        }
    }

    /// <summary>True if <paramref name="action"/> is one of the supported invoke actions
    /// (<c>invoke</c>/<c>toggle</c>/<c>setvalue</c>/<c>setfocus</c>/<c>expand</c>/<c>collapse</c>/<c>select</c>, case-insensitive).</summary>
    public static bool IsSupportedAction(string action) =>
        action?.ToLowerInvariant() switch {
            "invoke" or "toggle" or "setvalue" or "setfocus" or "expand" or "collapse" or "select" => true,
            _ => false,
        };

    // -------------------------------------------------------------------------------------------
    // The dedicated UIA worker (#215)
    // -------------------------------------------------------------------------------------------

    private static readonly object WorkerGate = new();
    private static BlockingCollection<Action<UIA3Automation>>? _workQueue;
    private static Thread? _workerThread;
    private static int _workerGeneration;

    /// <summary>
    /// Runs <paramref name="work"/> on the dedicated UIA worker thread (started lazily) against the
    /// cached <see cref="UIA3Automation"/>, blocking the caller until it completes. Exceptions
    /// propagate to the caller, where each public entry point's defensive catch handles them.
    /// </summary>
    private static T RunOnWorker<T>(Func<UIA3Automation, T> work) {
        AssertNotOnMessageLoopThread();
        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EnqueueWork(automation => {
            try {
                tcs.SetResult(work(automation));
            }
            catch (Exception e) {
                tcs.SetException(e);
            }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    private static void EnqueueWork(Action<UIA3Automation> item) {
        lock (WorkerGate) {
            if (_workQueue is null) {
                BlockingCollection<Action<UIA3Automation>> queue = [];
                _workQueue = queue;
                _workerGeneration++;
                _workerThread = new Thread(() => WorkerMain(queue)) { IsBackground = true, Name = "mcec-uia" };
                // Explicitly MTA: UIA *clients* are recommended to run MTA (an STA client can deadlock
                // against providers that marshal back), and MTA lets the invoke path drive a
                // worker-resolved element from another MTA thread without marshaling.
                _workerThread.SetApartmentState(ApartmentState.MTA);
                _workerThread.Start();
            }
            _workQueue.Add(item);
        }
    }

    /// <summary>The worker main: one cached automation for the thread's lifetime, disposed at the end.</summary>
    private static void WorkerMain(BlockingCollection<Action<UIA3Automation>> queue) {
        using UIA3Automation automation = new();
        foreach (Action<UIA3Automation> item in queue.GetConsumingEnumerable()) {
            item(automation);
        }
    }

    /// <summary>
    /// Stops the worker thread and disposes the cached <see cref="UIA3Automation"/> (bounded join).
    /// Called on application shutdown (GUI: <c>MainWindow.PerformShutdown</c>; headless: after the
    /// stdio loop ends). Idempotent; a later call lazily restarts the worker (tests rely on this).
    /// </summary>
    public static void Shutdown() {
        BlockingCollection<Action<UIA3Automation>>? queue;
        Thread? worker;
        lock (WorkerGate) {
            queue = _workQueue;
            worker = _workerThread;
            _workQueue = null;
            _workerThread = null;
        }
        if (queue is null) {
            return;
        }
        queue.CompleteAdding(); // the worker drains what's queued, disposes the automation, and exits
        if (worker is not null && !worker.Join(ShutdownJoinTimeoutMs)) {
            Logger.Instance.Log4.Warn(
                $"UiaService: the UIA worker did not exit within {ShutdownJoinTimeoutMs}ms (a stuck UIA call); abandoning it (background thread).");
        }
    }

    /// <summary>
    /// Asserts (debug builds) that UIA work is not entering from a thread that runs a WinForms
    /// message loop. A UIA client call against MCEC's OWN window from the UI thread self-deadlocks:
    /// the provider side needs the very message loop the call is blocking (the historical
    /// legacy-TCP-path hazard, gone since #195 moved queue execution to the dispatcher thread — this
    /// assertion documents and enforces the contract so it cannot regress).
    /// </summary>
    private static void AssertNotOnMessageLoopThread() =>
        System.Diagnostics.Debug.Assert(!System.Windows.Forms.Application.MessageLoop,
            "UiaService: UIA work must never run on a thread with a WinForms message loop — a UIA call " +
            "against MCEC's own window would self-deadlock. Dispatch tool work on MCP workers, the " +
            "command dispatcher thread, or the invoke grace thread instead.");

    /// <summary>
    /// Test probe (InternalsVisibleTo): runs one item on the worker and reports the worker's managed
    /// thread id, the identity hash of the cached automation, and the worker generation (bumped on
    /// each lazy start), so tests can pin "one thread, one cached automation" across calls and prove
    /// a post-<see cref="Shutdown"/> restart deterministically.
    /// </summary>
    internal static (int ThreadId, int AutomationHash, int Generation) ProbeWorker() {
        (int threadId, int automationHash) = RunOnWorker(automation => (Environment.CurrentManagedThreadId,
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(automation)));
        lock (WorkerGate) {
            return (threadId, automationHash, _workerGeneration);
        }
    }

    /// <summary>
    /// Polls for an element on the shared UIA worker: one bounded lookup per
    /// <see cref="FindPollIntervalMs"/>, sleeping on the CALLING thread between attempts so a long
    /// wait never monopolizes the worker. <paramref name="timeoutMs"/> &lt;= 0 means a single attempt.
    /// The root is re-resolved from <paramref name="hwnd"/> each attempt, so a window that re-renders
    /// mid-wait cannot leave the poll holding a stale subtree.
    /// </summary>
    private static AutomationElement? FindWithPolling(IntPtr hwnd, string by, string value, int timeoutMs) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            AutomationElement? el = RunOnWorker(automation => {
                AutomationElement root = automation.FromHandle(hwnd);
                return root.FindFirstDescendant(BuildCondition(by, value));
            });
            if (el is not null || timeoutMs <= 0) {
                return el;
            }
            long remaining = timeoutMs - sw.ElapsedMilliseconds;
            if (remaining <= 0) {
                return null;
            }
            Thread.Sleep((int)Math.Min(FindPollIntervalMs, remaining));
        }
    }

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

    /// <summary>
    /// Selects the element using the SelectionItem pattern (for TabItem, ListItem, RadioButton, etc.).
    /// Returns false if the pattern is not supported.
    /// </summary>
    private static bool InvokeSelect(AutomationElement el) {
        var pattern = el.Patterns.SelectionItem.PatternOrDefault;
        if (pattern is null) {
            return false;
        }
        pattern.Select();
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

        try {
            var sel = el.Patterns.SelectionItem.PatternOrDefault;
            if (sel is not null) {
                info.IsSelected = sel.IsSelected.ValueOrDefault;
            }
        }
        catch (COMException) {
            // Leave null.
        }

        return info;
    }

    private static string? CleanString(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
