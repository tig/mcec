// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// A parsed emergency-stop (#135) hotkey: a set of required modifier keys plus one main key. The
/// operator configures it as a <c>+</c>-separated string (e.g. <c>Ctrl+Alt+Shift+S</c>) in
/// <see cref="AppSettings.EmergencyStopHotkey"/>; the default is a four-key chord no application uses and
/// the agent never synthesizes, so accidental triggering is near-zero.
///
/// <para>Modifier names are normalized to <c>ctrl</c>/<c>alt</c>/<c>shift</c>/<c>win</c> (left/right
/// variants collapse together) so matching against the low-level hook's specific L/R virtual-key codes
/// works regardless of which physical modifier the operator pressed.</para>
/// </summary>
public sealed class EmergencyStopHotkey {
    /// <summary>The operator-recommended default: <c>Ctrl+Alt+Shift+S</c> (mnemonic: <b>S</b>top).</summary>
    public const string DefaultSpec = "Ctrl+Alt+Shift+S";

    private EmergencyStopHotkey(IReadOnlySet<string> modifiers, Keys key, string display) {
        Modifiers = modifiers;
        Key = key;
        Display = display;
    }

    /// <summary>The set of required modifier names (a subset of <c>ctrl</c>/<c>alt</c>/<c>shift</c>/<c>win</c>).</summary>
    public IReadOnlySet<string> Modifiers { get; }

    /// <summary>The main (non-modifier) key that, pressed with all <see cref="Modifiers"/> held, fires the stop.</summary>
    public Keys Key { get; }

    /// <summary>A normalized human-readable rendering of the chord (e.g. <c>Ctrl+Alt+Shift+S</c>).</summary>
    public string Display { get; }

    /// <summary>The parsed default chord.</summary>
    public static EmergencyStopHotkey Default => Parse(DefaultSpec)!;

    /// <summary>
    /// Parses <paramref name="spec"/>, falling back to <see cref="Default"/> (with a logged warning) when
    /// it is invalid. The arm-the-hotkey paths (GUI <c>MainWindow.Start</c> and headless
    /// <see cref="HeadlessOperatorUi"/>) share this so a typo in settings degrades to the default chord
    /// identically in both hosts instead of leaving the operator with no panic hotkey.
    /// </summary>
    public static EmergencyStopHotkey ParseOrDefault(string? spec) {
        EmergencyStopHotkey? parsed = Parse(spec);
        if (parsed is null) {
            Logger.Instance.Log4.Warn($"EmergencyStop: could not parse hotkey '{spec}'; using default {DefaultSpec}.");
            return Default;
        }
        return parsed;
    }

    /// <summary>
    /// Parses a <c>+</c>-separated chord spec (e.g. <c>Ctrl+Alt+Shift+S</c>, <c>Pause</c>). Returns null
    /// when the spec is empty, names no main key, or names an unrecognized key. Modifier-only specs are
    /// rejected (a chord must have exactly one non-modifier key).
    /// </summary>
    public static EmergencyStopHotkey? Parse(string? spec) {
        if (string.IsNullOrWhiteSpace(spec)) {
            return null;
        }

        HashSet<string> modifiers = [];
        Keys? mainKey = null;
        foreach (string rawToken in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            string token = rawToken.ToLowerInvariant();
            string? modifier = NormalizeModifier(token);
            if (modifier is not null) {
                modifiers.Add(modifier);
                continue;
            }
            if (mainKey is not null) {
                // More than one non-modifier key; not a supported single-key chord.
                return null;
            }
            if (ParseKey(token) is not { } k) {
                return null;
            }
            mainKey = k;
        }

        if (mainKey is null) {
            return null;
        }

        string display = string.Join("+",
            OrderModifiers(modifiers).Select(Capitalize).Append(KeyDisplay(mainKey.Value)));
        return new EmergencyStopHotkey(modifiers, mainKey.Value, display);
    }

    /// <summary>
    /// The canonical modifier name for a <see cref="Keys"/> value, or null when it is not a modifier. Left
    /// and right variants (and the generic form) collapse to one name so the low-level hook's specific
    /// L/R codes match a chord authored with the generic modifier name.
    /// </summary>
    public static string? ModifierName(Keys key) => key switch {
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "shift",
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "ctrl",
        Keys.Menu or Keys.LMenu or Keys.RMenu => "alt",
        Keys.LWin or Keys.RWin => "win",
        _ => null,
    };

    private static string? NormalizeModifier(string token) => token switch {
        "ctrl" or "control" or "ctl" => "ctrl",
        "alt" or "menu" => "alt",
        "shift" or "shft" => "shift",
        "win" or "windows" or "lwin" or "rwin" or "super" or "meta" => "win",
        _ => null,
    };

    private static Keys? ParseKey(string token) {
        // A single letter or digit.
        if (token.Length == 1) {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z') {
                return Enum.Parse<Keys>(c.ToString());
            }
            if (c is >= '0' and <= '9') {
                return Enum.Parse<Keys>("D" + c);
            }
        }

        // Common aliases, then a general Keys parse (covers F1..F24, named keys, etc.).
        return token switch {
            "esc" or "escape" => Keys.Escape,
            "pause" or "break" => Keys.Pause,
            "space" or "spacebar" => Keys.Space,
            "enter" or "return" => Keys.Enter,
            "del" or "delete" => Keys.Delete,
            "ins" or "insert" => Keys.Insert,
            "home" => Keys.Home,
            "end" => Keys.End,
            "tab" => Keys.Tab,
            _ => Enum.TryParse(token, ignoreCase: true, out Keys parsed) && parsed != Keys.None ? parsed : null,
        };
    }

    private static IEnumerable<string> OrderModifiers(IEnumerable<string> modifiers) {
        // Stable, conventional order regardless of how the operator typed the spec.
        string[] order = ["ctrl", "alt", "shift", "win"];
        return order.Where(modifiers.Contains);
    }

    private static string Capitalize(string s) => s switch {
        "ctrl" => "Ctrl",
        "alt" => "Alt",
        "shift" => "Shift",
        "win" => "Win",
        _ => s,
    };

    private static string KeyDisplay(Keys key) => key switch {
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        _ => key.ToString(),
    };
}
