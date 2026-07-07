// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Concurrent;
using FlaUI.UIA3;

namespace MCEControl;

/// <summary>
/// Owns one MTA worker thread and one cached <see cref="UIA3Automation"/> (#215). Queued work
/// receives that automation when the item runs on the worker — never a process-wide field — so a
/// shutdown/restart cannot hand stale-queue items a later generation's automation (#317 review).
/// </summary>
internal sealed class UiaWorkerHost : IUiaWorkerStopHandle {
    private readonly Lock _gate = new();
    private BlockingCollection<Action<UIA3Automation>>? _queue;
    private Thread? _thread;
    private ManualResetEventSlim? _ready;

    internal void Enqueue(Action<UIA3Automation> work) {
        lock (_gate) {
            EnsureStarted();
            _queue!.Add(work);
        }
    }

    public void Stop(int joinTimeoutMs) {
        BlockingCollection<Action<UIA3Automation>>? queue;
        Thread? thread;
        ManualResetEventSlim? ready;
        lock (_gate) {
            queue = _queue;
            thread = _thread;
            ready = _ready;
            _queue = null;
            _thread = null;
            _ready = null;
        }
        if (queue is null) {
            return;
        }
        queue.CompleteAdding();
        if (thread is not null && !thread.Join(joinTimeoutMs)) {
            Logger.Instance.Log4.Warn(
                $"UiaWorkerHost: the UIA worker did not exit within {joinTimeoutMs}ms (a stuck UIA call); abandoning it (background thread).");
        }
        ready?.Dispose();
    }

    private void EnsureStarted() {
        if (_queue is not null) {
            return;
        }
        BlockingCollection<Action<UIA3Automation>> queue = [];
        _queue = queue;
        _ready = new ManualResetEventSlim(initialState: false);
        _thread = new Thread(() => WorkerMain(queue)) { IsBackground = true, Name = "mcec-uia" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
        _ready.Wait();
    }

    private void WorkerMain(BlockingCollection<Action<UIA3Automation>> queue) {
        using UIA3Automation automation = new();
        _ready!.Set();
        foreach (Action<UIA3Automation> item in queue.GetConsumingEnumerable()) {
            item(automation);
        }
    }
}