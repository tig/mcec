//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Windows.Forms;
using Gma.UserActivityMonitor;
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
        Assert.Equal(baseline.KeyPress + delta, HookManager.KeyPressSubscriberCount);
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
