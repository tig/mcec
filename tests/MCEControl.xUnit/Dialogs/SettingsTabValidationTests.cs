// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Tests for the pure per-tab validation cores (#213). These are the exact rules the pre-#213
/// SettingsDialog.SettingsChanged() applied, now factored into internal static helpers on each
/// tab UserControl so they are testable without constructing any WinForms controls.
/// </summary>
public class SettingsTabValidationTests {
    // --- Client ---

    [Theory]
    [InlineData(true, "host", "5150", true)]
    [InlineData(true, "", "5150", false)] // empty host
    [InlineData(true, "host", "0", false)] // port 0
    [InlineData(true, "host", "", false)] // empty port
    [InlineData(true, "host", "abc", false)] // unparsable port
    [InlineData(true, "", "", false)]
    [InlineData(false, "", "", true)] // disabled section is always valid
    [InlineData(false, "host", "abc", true)]
    public void ClientSection_ValidatesHostAndPort(bool enabled, string host, string portText, bool expected) {
        Assert.Equal(expected, ClientSettingsTab.IsSectionValid(enabled, host, portText));
    }

    // --- Server / Wakeup ---

    [Theory]
    [InlineData(true, true, "host", "wake", "close", "5150", true)]
    [InlineData(true, true, "", "wake", "close", "5150", false)] // empty wakeup host
    [InlineData(true, true, "host", "", "close", "5150", false)] // empty wakeup command
    [InlineData(true, true, "host", "wake", "", "5150", false)] // empty closing command
    [InlineData(true, true, "host", "wake", "close", "0", false)] // port 0
    [InlineData(true, true, "host", "wake", "close", "abc", false)] // unparsable port
    [InlineData(true, false, "", "", "", "", true)] // wakeup disabled -> valid
    [InlineData(false, true, "", "", "", "", true)] // server disabled -> valid
    [InlineData(false, false, "", "", "", "", true)]
    public void ServerSection_ValidatesWakeupOnlyWhenBothEnabled(bool serverEnabled, bool wakeupEnabled,
        string wakeupHost, string wakeupCommand, string closingCommand, string wakeupPortText, bool expected) {
        Assert.Equal(expected, ServerSettingsTab.IsSectionValid(serverEnabled, wakeupEnabled,
            wakeupHost, wakeupCommand, closingCommand, wakeupPortText));
    }

    // --- Activity Monitor ---

    [Theory]
    [InlineData(true, "activity", "10", true)]
    [InlineData(true, "", "10", false)] // empty command
    [InlineData(true, "activity", "0", false)] // debounce 0
    [InlineData(true, "activity", "", false)] // empty debounce
    [InlineData(true, "activity", "abc", false)] // unparsable debounce
    [InlineData(false, "", "", true)] // disabled section is always valid
    public void ActivityMonitorSection_ValidatesCommandAndDebounce(bool enabled, string command,
        string debounceText, bool expected) {
        Assert.Equal(expected, ActivityMonitorSettingsTab.IsSectionValid(enabled, command, debounceText));
    }
}
