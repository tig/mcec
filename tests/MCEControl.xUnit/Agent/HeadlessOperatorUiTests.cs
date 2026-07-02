// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Threading;
using Xunit;
using MCEControl;
using MCEControl.Hooks;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Tests for <see cref="HeadlessOperatorUi"/>, the operator safety surface in headless <c>--mcp</c>
/// mode. Uses <see cref="HookManager.SuppressRealHooksForTesting"/> (no real WH_KEYBOARD_LL on hosted
/// CI) and the <see cref="HeadlessOperatorUi.RearmPromptForTests"/> seam (no visible modal; nothing on
/// CI could dismiss it). Mutates process-global state (the latch, the hook bookkeeping), so serialized
/// via the AgentSerial collection with save/restore in finally.
/// </summary>
[Collection("AgentSerial")]
public class HeadlessOperatorUiTests {
    [Fact]
    public void Start_NothingEnabled_DoesNotStartPumpThread() {
        AppSettings settings = new() { EmergencyStopEnabled = false, CommandOverlayEnabled = false };
        try {
            HeadlessOperatorUi.Start(settings);

            Assert.False(HeadlessOperatorUi.IsRunning, "with e-stop and overlay both disabled there is nothing to host");
        }
        finally {
            HeadlessOperatorUi.Stop();
        }
    }

    [Fact]
    public void Trigger_ShowsRearmPrompt_AcceptingReArms() {
        HookManager.SuppressRealHooksForTesting = true;
        string savedArtifactRoot = AgentRuntime.ArtifactRoot;
        using ManualResetEventSlim prompted = new(false);
        string? promptedReason = null;
        HeadlessOperatorUi.RearmPromptForTests = reason => {
            promptedReason = reason;
            prompted.Set();
            return true; // operator clicks Re-arm
        };
        // Overlay off: this test exercises the e-stop path; no window needed.
        AppSettings settings = new() { EmergencyStopEnabled = true, CommandOverlayEnabled = false };
        try {
            AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-tests", Guid.NewGuid().ToString("N"));

            HeadlessOperatorUi.Start(settings);
            Assert.True(HeadlessOperatorUi.IsRunning, "the pump thread should be hosting");

            // The full headless flow: trigger → latch → StateChanged → marshal to the pump thread →
            // prompt → (seam says re-arm) → Rearm clears the latch.
            EmergencyStop.Trigger("test panic");

            Assert.True(prompted.Wait(TimeSpan.FromSeconds(10)), "the re-arm prompt was never offered");
            Assert.Equal("test panic", promptedReason);

            // Rearm runs after the prompt returns; poll for the latch to clear.
            for (int i = 0; i < 200 && EmergencyStop.IsStopped; i++) {
                Thread.Sleep(50);
            }
            Assert.False(EmergencyStop.IsStopped, "accepting the prompt must re-arm (clear the latch)");
        }
        finally {
            HeadlessOperatorUi.Stop();
            HeadlessOperatorUi.RearmPromptForTests = null;
            AgentRuntime.SetEmergencyStopped(false);
            AgentRuntime.ArtifactRoot = savedArtifactRoot;
            HookManager.SuppressRealHooksForTesting = false;
        }
    }

    [Fact]
    public void Stop_EndsPumpThread() {
        HookManager.SuppressRealHooksForTesting = true;
        AppSettings settings = new() { EmergencyStopEnabled = true, CommandOverlayEnabled = false };
        try {
            HeadlessOperatorUi.Start(settings);
            Assert.True(HeadlessOperatorUi.IsRunning);

            HeadlessOperatorUi.Stop();

            Assert.False(HeadlessOperatorUi.IsRunning, "Stop must end the hosting state");
        }
        finally {
            HeadlessOperatorUi.Stop();
            HookManager.SuppressRealHooksForTesting = false;
        }
    }
}
