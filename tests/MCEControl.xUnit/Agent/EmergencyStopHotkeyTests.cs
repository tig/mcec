// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Windows.Forms;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Unit tests for parsing the emergency-stop (#135) hotkey spec.</summary>
public class EmergencyStopHotkeyTests {
    [Fact]
    public void Default_IsCtrlAltShiftS() {
        EmergencyStopHotkey hk = EmergencyStopHotkey.Default;

        Assert.Equal(Keys.S, hk.Key);
        Assert.Equal(3, hk.Modifiers.Count);
        Assert.Contains("ctrl", hk.Modifiers);
        Assert.Contains("alt", hk.Modifiers);
        Assert.Contains("shift", hk.Modifiers);
        Assert.Equal("Ctrl+Alt+Shift+S", hk.Display);
    }

    [Theory]
    [InlineData("ctrl+alt+shift+s")]
    [InlineData("Shift+Ctrl+Alt+S")]   // order-independent
    [InlineData("  Ctrl + Alt + Shift + S ")] // whitespace tolerant
    public void Parse_NormalizesToCanonicalDisplay(string spec) {
        EmergencyStopHotkey hk = EmergencyStopHotkey.Parse(spec)!;
        Assert.Equal("Ctrl+Alt+Shift+S", hk.Display);
    }

    [Fact]
    public void Parse_SingleDedicatedKey_NoModifiers() {
        EmergencyStopHotkey hk = EmergencyStopHotkey.Parse("Pause")!;
        Assert.Equal(Keys.Pause, hk.Key);
        Assert.Empty(hk.Modifiers);
    }

    [Fact]
    public void Parse_Digit_MapsToDKey() {
        EmergencyStopHotkey hk = EmergencyStopHotkey.Parse("Ctrl+1")!;
        Assert.Equal(Keys.D1, hk.Key);
        Assert.Single(hk.Modifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt+Shift")]     // modifier-only, no main key
    [InlineData("Ctrl+A+B")]           // two non-modifier keys
    [InlineData("Ctrl+NotAKey")]       // unknown key name
    [InlineData(null)]
    public void Parse_Invalid_ReturnsNull(string? spec) {
        Assert.Null(EmergencyStopHotkey.Parse(spec));
    }

    [Theory]
    [InlineData(Keys.LControlKey, "ctrl")]
    [InlineData(Keys.RControlKey, "ctrl")]
    [InlineData(Keys.LShiftKey, "shift")]
    [InlineData(Keys.RMenu, "alt")]
    [InlineData(Keys.LWin, "win")]
    [InlineData(Keys.S, null)]
    public void ModifierName_CollapsesLeftRightVariants(Keys key, string? expected) {
        Assert.Equal(expected, EmergencyStopHotkey.ModifierName(key));
    }
}
