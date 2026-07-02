//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Windows.Forms;
using MCEControl.Hooks;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #197: the activity monitor's teardown must detach every handler it
/// attached to <see cref="HookManager" />'s static events so the global WH_MOUSE_LL/WH_KEYBOARD_LL
/// hooks are uninstalled on Stop, and a Start/Stop/Start cycle must yield exactly ONE handler set
/// (the old GlobalEventProvider wrapper never detached, leaking the system-wide hooks and firing
/// Activity N times per input event after N restarts).
///
/// Uses <see cref="HookManager.SuppressRealHooksForTesting" /> so the subscribe/unsubscribe
/// bookkeeping runs without installing real global hooks — hosted CI has no interactive desktop.
/// All assertions are deltas from a captured baseline so the tests stay robust if anything else in
/// the process ever subscribes to HookManager.
/// </summary>
public class UserActivityMonitorServiceTests {
    private static void AssertServiceHandlerDelta(HookSubscriberBaseline baseline, int delta) {
        Assert.Equal(baseline.KeyDown + delta, HookManager.KeyDownSubscriberCount);
        Assert.Equal(baseline.KeyUp + delta, HookManager.KeyUpSubscriberCount);
        Assert.Equal(baseline.MouseMove + delta, HookManager.MouseMoveSubscriberCount);
        Assert.Equal(baseline.MouseClick + delta, HookManager.MouseClickSubscriberCount);
        Assert.Equal(baseline.MouseDown + delta, HookManager.MouseDownSubscriberCount);
        Assert.Equal(baseline.MouseDoubleClick + delta, HookManager.MouseDoubleClickSubscriberCount);
        // Subscribing MouseDoubleClick also attaches HookManager's internal double-click monitor to
        // MouseUp, so MouseUp moves by 2 per attached handler set.
        Assert.Equal(baseline.MouseUp + (delta * 2), HookManager.MouseUpSubscriberCount);
    }

    [Fact]
    public void StartStopStart_AttachesExactlyOneHandlerSet_AndUnhooksOnStop() {
        HookManager.SuppressRealHooksForTesting = true;
        UserActivityMonitorService monitor = UserActivityMonitorService.Instance;
        monitor.InputDetection = true;
        monitor.UnlockDetection = false;
        monitor.PowerBroadcastDetection = false;

        HookSubscriberBaseline baseline = HookSubscriberBaseline.Capture();

        try {
            // Two full cycles: after any number of restarts there must be exactly ONE handler set.
            for (int cycle = 1; cycle <= 2; cycle++) {
                monitor.Start();

                Assert.True(HookManager.IsKeyboardHookInstalled, $"cycle {cycle}: keyboard hook not installed after Start");
                Assert.True(HookManager.IsMouseHookInstalled, $"cycle {cycle}: mouse hook not installed after Start");
                AssertServiceHandlerDelta(baseline, 1);

                monitor.Stop();

                // The #197 invariant: after Stop, zero of our handlers remain and (when nothing else
                // is subscribed) the system-wide hooks are unhooked.
                AssertServiceHandlerDelta(baseline, 0);
                if (baseline.KeyboardTotal == 0) {
                    Assert.False(HookManager.IsKeyboardHookInstalled, $"cycle {cycle}: keyboard hook still installed after Stop");
                }
                if (baseline.MouseTotal == 0) {
                    Assert.False(HookManager.IsMouseHookInstalled, $"cycle {cycle}: mouse hook still installed after Stop");
                }
            }
        }
        finally {
            // Best-effort cleanup if an assertion fired between Start and Stop (Stop is idempotent).
            monitor.Stop();
        }
    }

    // NOTE (#198 → #214): there used to be a test here asserting the monitor never subscribes
    // HookManager.KeyPress (whose hook-proc ToAscii call consumed dead-key state and broke
    // accented-character composition system-wide). When the hook code became first-party (#214) the
    // KeyPress/ToAscii path was deleted outright, making that regression structurally impossible.

    /// <summary>
    /// Issue #198: the hook-path handler must do only the cheap debounce check and dispatch the heavy
    /// work (log I/O, telemetry, SendLine's synchronous socket/serial writes) — never run it inline in
    /// the hook callback. Windows silently evicts an LL hook whose callback exceeds
    /// LowLevelHooksTimeout, and the #135 emergency-stop hotkey rides the same WH_KEYBOARD_LL hook.
    /// The dispatch seam captures the work without running it; the dispatch count proves the handler
    /// hands the work off exactly once (post-#209 the work itself is harmless in-proc — SendLine goes
    /// through AgentRuntime, a logged no-op with no host registered — but it must still never run
    /// inline on the hook path).
    /// </summary>
    [Fact]
    public void Activity_DispatchesHeavyWorkOffHookPath_AndDebouncesInHandler() {
        HookManager.SuppressRealHooksForTesting = true;
        UserActivityMonitorService monitor = UserActivityMonitorService.Instance;
        monitor.InputDetection = true;
        monitor.UnlockDetection = false;
        monitor.PowerBroadcastDetection = false;
        monitor.DebounceTime = 60;

        int dispatched = 0;
        monitor.DispatchForTesting = work => {
            Assert.NotNull(work);
            dispatched++; // capture, do NOT run — asserts the handler doesn't need the work executed
        };

        monitor.Start();
        try {
            // Start() primes the debounce clock to "now", so step it back past the window.
            monitor.LastActivityTimeForTesting = DateTime.Now.AddSeconds(-(monitor.DebounceTime + 1));

            monitor.Activity("KeyDown");
            Assert.Equal(1, dispatched); // dispatched exactly once, handler returned without running it

            monitor.Activity("MouseMove");
            monitor.Activity("KeyDown");
            Assert.Equal(1, dispatched); // inside the debounce window: no further dispatches
        }
        finally {
            monitor.Stop();
            monitor.DispatchForTesting = null;
            monitor.DebounceTime = 5;
        }
    }

    /// <summary>
    /// The debounce lives in the handler (not in the dispatched work) so the dispatch mechanism is not
    /// flooded on every mouse move — after the window elapses, the next activity dispatches again.
    /// </summary>
    [Fact]
    public void Activity_DispatchesAgain_AfterDebounceWindowElapses() {
        HookManager.SuppressRealHooksForTesting = true;
        UserActivityMonitorService monitor = UserActivityMonitorService.Instance;
        monitor.InputDetection = true;
        monitor.UnlockDetection = false;
        monitor.PowerBroadcastDetection = false;
        monitor.DebounceTime = 60;

        int dispatched = 0;
        monitor.DispatchForTesting = _ => dispatched++;

        monitor.Start();
        try {
            monitor.LastActivityTimeForTesting = DateTime.Now.AddSeconds(-(monitor.DebounceTime + 1));
            monitor.Activity("KeyDown");
            Assert.Equal(1, dispatched);

            // Simulate the debounce window elapsing.
            monitor.LastActivityTimeForTesting = DateTime.Now.AddSeconds(-(monitor.DebounceTime + 1));
            monitor.Activity("MouseMove");
            Assert.Equal(2, dispatched);
        }
        finally {
            monitor.Stop();
            monitor.DispatchForTesting = null;
            monitor.DebounceTime = 5;
        }
    }

    [Fact]
    public void HookManager_UninstallsHook_WhenLastSubscriberRemoved() {
        HookManager.SuppressRealHooksForTesting = true;
        HookSubscriberBaseline baseline = HookSubscriberBaseline.Capture();
        KeyEventHandler handler = (sender, e) => { };

        HookManager.KeyDown += handler;
        try {
            Assert.Equal(baseline.KeyDown + 1, HookManager.KeyDownSubscriberCount);
            Assert.True(HookManager.IsKeyboardHookInstalled);
        }
        finally {
            HookManager.KeyDown -= handler;
        }

        Assert.Equal(baseline.KeyDown, HookManager.KeyDownSubscriberCount);
        if (baseline.KeyboardTotal == 0) {
            Assert.False(HookManager.IsKeyboardHookInstalled);
        }
    }
}
