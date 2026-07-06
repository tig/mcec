//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using Xunit;
using MCEControl;

namespace MCEControl.xUnit;

/// <summary>
/// The GUI host only shows the command overlay (and its "MCEC is controlling your PC" banner) when the
/// agent front door is open (#292); a normal, non-agent instance must not show it. Pure gate; no Form
/// is constructed (InternalsVisibleTo).
/// </summary>
public class MainWindowOverlayGateTests {
    [Theory]
    // overlayEnabled, mcpServerEnabled, agentCommandsEnabled, expected
    [InlineData(true, false, false, false)] // the bug: enabled by default, but no front door -> hidden
    [InlineData(true, true, false, true)]   // MCP/HTTP front door open -> shown
    [InlineData(true, false, true, true)]   // agent commands front door open -> shown
    [InlineData(true, true, true, true)]    // both open -> shown
    [InlineData(false, true, false, false)] // CommandOverlayEnabled=false force-disables it
    [InlineData(false, false, true, false)] // ditto, even with a front door open
    [InlineData(false, false, false, false)]
    public void ShouldShowCommandOverlay_RequiresOverlayEnabledAndAnOpenFrontDoor(
        bool overlayEnabled, bool mcpServerEnabled, bool agentCommandsEnabled, bool expected) {
        AppSettings settings = new() {
            CommandOverlayEnabled = overlayEnabled,
            McpServerEnabled = mcpServerEnabled,
            AgentCommandsEnabled = agentCommandsEnabled,
        };

        Assert.Equal(expected, MainWindow.ShouldShowCommandOverlay(settings));
    }
}
