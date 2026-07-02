// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Regression tests for #196: the Settings dialog hands MainWindow a deep clone on OK, and the
/// apply path must re-publish that new object to <see cref="AgentRuntime.Settings"/> — otherwise
/// the UI-agnostic engine (AgentServer security gates, CommandInvoker pacing, overlay, record)
/// keeps reading the stale pre-dialog object until restart. MainWindow is a Form and impractical
/// to construct headlessly, so these tests exercise the extracted republish helper
/// (<see cref="MainWindow.PublishAgentRuntimeSettings"/>) that both the load and dialog-OK paths
/// go through, and verify the outcome via <see cref="AgentRuntime"/> state.
/// </summary>
[Collection("AgentSerial")]
public class MainWindowApplySettingsTests {
    [Fact]
    public void PublishAgentRuntimeSettings_RepublishesTheExactNewObject() {
        AppSettings? saved = AgentRuntime.Settings;
        try {
            var initial = new AppSettings();
            MainWindow.PublishAgentRuntimeSettings(initial);
            Assert.Same(initial, AgentRuntime.Settings);

            // Simulate the Settings dialog OK path: it returns a clone, not the same instance.
            var clone = (AppSettings)initial.Clone();
            Assert.NotSame(initial, clone);

            MainWindow.PublishAgentRuntimeSettings(clone);

            // The engine seam must now be the new object — not the stale pre-dialog one.
            Assert.Same(clone, AgentRuntime.Settings);
        }
        finally {
            AgentRuntime.Settings = saved;
        }
    }

    [Fact]
    public void PublishAgentRuntimeSettings_EngineGatesSeeNewValues() {
        AppSettings? saved = AgentRuntime.Settings;
        try {
            var initial = new AppSettings { AgentCommandsEnabled = false };
            MainWindow.PublishAgentRuntimeSettings(initial);
            Assert.False(AgentRuntime.AgentCommandsEnabled);

            // Operator flips a security gate in the Settings dialog; OK produces a clone.
            var clone = (AppSettings)initial.Clone();
            clone.AgentCommandsEnabled = true;

            MainWindow.PublishAgentRuntimeSettings(clone);

            // Gate checks that read AgentRuntime.Settings must reflect the post-dialog value.
            Assert.True(AgentRuntime.AgentCommandsEnabled);

            // And flipping it back off in a later dialog round-trip must also take effect.
            var second = (AppSettings)clone.Clone();
            second.AgentCommandsEnabled = false;
            MainWindow.PublishAgentRuntimeSettings(second);
            Assert.False(AgentRuntime.AgentCommandsEnabled);
        }
        finally {
            AgentRuntime.Settings = saved;
        }
    }
}
