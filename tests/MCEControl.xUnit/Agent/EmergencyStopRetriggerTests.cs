// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Verifies <see cref="EmergencyStop.Retriggered"/>: pressing the chord while the stop is already
/// latched must NOT re-engage (no <see cref="EmergencyStop.StateChanged"/>), but must surface the
/// re-press so a host can re-offer its re-arm affordance (headless: HeadlessOperatorUi reopens the
/// prompt). Toggles the process-global latch, so serialized via the AgentSerial collection.
/// </summary>
[Collection("AgentSerial")]
public class EmergencyStopRetriggerTests {
    [Fact]
    public void Trigger_WhileAlreadyLatched_RaisesRetriggered_NotStateChanged() {
        using ManualResetEventSlim retriggered = new(false);
        bool stateChangedRaised = false;
        Action onRetriggered = retriggered.Set;
        Action<bool> onStateChanged = _ => stateChangedRaised = true;
        EmergencyStop.Retriggered += onRetriggered;
        EmergencyStop.StateChanged += onStateChanged;
        try {
            // Latch directly (not via Trigger) so this test exercises ONLY the already-latched
            // early-return path, without CompleteTrigger's input-release side effects.
            AgentRuntime.SetEmergencyStopped(true);

            EmergencyStop.Trigger("re-press");

            // Dispatched on the thread pool (never on the hook callback path); wait for it.
            Assert.True(retriggered.Wait(TimeSpan.FromSeconds(10)), "Retriggered never fired for a re-press while latched");
            Assert.False(stateChangedRaised, "a re-press while latched must not re-raise StateChanged");
            Assert.True(EmergencyStop.IsStopped, "the latch must survive a re-press");
        }
        finally {
            EmergencyStop.Retriggered -= onRetriggered;
            EmergencyStop.StateChanged -= onStateChanged;
            AgentRuntime.SetEmergencyStopped(false);
        }
    }
}
