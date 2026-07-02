// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The pure state machine behind the emergency-stop (#135) hotkey: it tracks which modifier keys are
/// <b>physically</b> held and reports when the configured chord's main key is pressed while all required
/// modifiers are down. Kept free of any hook/UI/actuation dependency so the trigger logic is fully
/// unit-testable; <see cref="EmergencyStop"/> owns the global-hook wiring and the actual stop.
///
/// <para>SECURITY: injected events (<see cref="Hooks.GlobalKeyEventArgs.Injected"/>) are ignored entirely; they
/// never arm a modifier and never trigger; so MCEC's own agent keystrokes can neither trip nor defeat the
/// stop. Only real hardware input drives the machine, which is what makes the hotkey a true human override.</para>
/// </summary>
public sealed class EmergencyStopDetector(EmergencyStopHotkey hotkey) {
    private readonly HashSet<string> _heldModifiers = [];

    /// <summary>
    /// Feeds a key-down event. Returns true when it completes the chord (the main key pressed with every
    /// required modifier physically held) and the stop should fire. Injected events return false and are
    /// otherwise ignored.
    /// </summary>
    public bool OnKeyDown(Keys key, bool injected) {
        if (injected) {
            return false;
        }

        string? modifier = EmergencyStopHotkey.ModifierName(key);
        if (modifier is not null) {
            _heldModifiers.Add(modifier);
            return false;
        }

        return NormalizeKey(key) == NormalizeKey(hotkey.Key) && HasAllRequiredModifiers();
    }

    /// <summary>Feeds a key-up event so a released physical modifier no longer counts toward the chord.</summary>
    public void OnKeyUp(Keys key, bool injected) {
        if (injected) {
            return;
        }
        string? modifier = EmergencyStopHotkey.ModifierName(key);
        if (modifier is not null) {
            _heldModifiers.Remove(modifier);
        }
    }

    /// <summary>Clears held-modifier state (e.g. when re-arming, so a stale held key can't immediately re-fire).</summary>
    public void Reset() => _heldModifiers.Clear();

    private bool HasAllRequiredModifiers() {
        foreach (string required in hotkey.Modifiers) {
            if (!_heldModifiers.Contains(required)) {
                return false;
            }
        }
        return true;
    }

    // Collapse left/right modifier variants so a chord authored with a generic modifier as its main key
    // (unusual, but valid) still matches; non-modifier keys pass through unchanged.
    private static Keys NormalizeKey(Keys key) => EmergencyStopHotkey.ModifierName(key) switch {
        "shift" => Keys.ShiftKey,
        "ctrl" => Keys.ControlKey,
        "alt" => Keys.Menu,
        "win" => Keys.LWin,
        _ => key,
    };
}
