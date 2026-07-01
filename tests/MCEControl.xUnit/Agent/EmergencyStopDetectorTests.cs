// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Windows.Forms;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Unit tests for the emergency-stop (#135) chord detector — most importantly that it fires only on the
/// full chord and NEVER on injected (agent-synthesized) input.
/// </summary>
public class EmergencyStopDetectorTests {
    private static EmergencyStopDetector Default() => new(EmergencyStopHotkey.Default);

    private static void HoldChordModifiers(EmergencyStopDetector d, bool injected = false) {
        d.OnKeyDown(Keys.LControlKey, injected);
        d.OnKeyDown(Keys.LMenu, injected);   // alt
        d.OnKeyDown(Keys.LShiftKey, injected);
    }

    [Fact]
    public void PhysicalChord_Fires() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);

        Assert.True(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void InjectedTriggerKey_NeverFires_EvenWithPhysicalModifiers() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d, injected: false);

        // The agent injecting the S keypress must not trip the stop.
        Assert.False(d.OnKeyDown(Keys.S, injected: true));
    }

    [Fact]
    public void InjectedModifiers_DoNotArmChord() {
        EmergencyStopDetector d = Default();
        // All modifiers arrive injected (agent holding them); a physical S alone must NOT complete the chord.
        HoldChordModifiers(d, injected: true);

        Assert.False(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void MissingModifier_DoesNotFire() {
        EmergencyStopDetector d = Default();
        d.OnKeyDown(Keys.LControlKey, false);
        d.OnKeyDown(Keys.LShiftKey, false);
        // No Alt held.
        Assert.False(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void ReleasedModifier_DisarmsChord() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);
        d.OnKeyUp(Keys.LMenu, injected: false); // release Alt physically

        Assert.False(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void InjectedModifierRelease_IsIgnored_ChordStillArmed() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);
        // An injected Alt-up (agent releasing) must not disarm a physically-held chord.
        d.OnKeyUp(Keys.LMenu, injected: true);

        Assert.True(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void WrongMainKey_DoesNotFire() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);
        Assert.False(d.OnKeyDown(Keys.Q, injected: false));
    }

    [Fact]
    public void Reset_ClearsHeldModifiers() {
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);
        d.Reset();
        Assert.False(d.OnKeyDown(Keys.S, injected: false));
    }

    [Fact]
    public void ExtraModifierHeld_StillFires() {
        // Win also held alongside the required Ctrl+Alt+Shift — the required subset is present, so it fires.
        EmergencyStopDetector d = Default();
        HoldChordModifiers(d);
        d.OnKeyDown(Keys.LWin, injected: false);
        Assert.True(d.OnKeyDown(Keys.S, injected: false));
    }
}
