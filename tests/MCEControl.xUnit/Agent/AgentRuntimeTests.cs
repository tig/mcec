// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

[Collection("AgentSerial")]
public class AgentRuntimeTests {
    [Fact]
    public void AgentCommandsEnabled_FalseWhenSettingsNull() {
        AgentRuntime.Settings = null;

        Assert.False(AgentRuntime.AgentCommandsEnabled);
    }

    [Fact]
    public void AgentCommandsEnabled_TrueWhenSettingsOptIn() {
        try {
            AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };

            Assert.True(AgentRuntime.AgentCommandsEnabled);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void AgentCommandsEnabled_FalseWhenSettingsOptOut() {
        try {
            AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = false };

            Assert.False(AgentRuntime.AgentCommandsEnabled);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Audit_DoesNotThrow() {
        System.Exception? ex = Record.Exception(() => AgentRuntime.Audit("capture", "window 0x1234 \"Title\""));

        Assert.Null(ex);

        AgentRuntime.Settings = null;
    }
}
