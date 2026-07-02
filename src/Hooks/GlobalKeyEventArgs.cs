// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Windows.Forms;

namespace MCEControl.Hooks;

/// <summary>
/// Extended global key event surfaced by <see cref="HookManager"/>'s low-level keyboard hook. Unlike the
/// plain <see cref="KeyEventArgs"/> events, this one carries whether the key was <see cref="Injected"/>
/// (synthesized by software via <c>SendInput</c>, flagged <c>LLKHF_INJECTED</c> by Windows) rather than
/// pressed on real hardware.
///
/// <para>The emergency-stop (#135) needs this distinction: MCEC's own agent actuation injects keystrokes,
/// so an e-stop that reacted to injected keys could be tripped — or defeated — by the very agent it is
/// meant to override. Reacting to <see cref="Injected"/> == <c>false</c> only makes the hotkey a true
/// human override.</para>
/// </summary>
public sealed class GlobalKeyEventArgs : EventArgs {
    public GlobalKeyEventArgs(Keys keyCode, bool injected) {
        KeyCode = keyCode;
        Injected = injected;
    }

    /// <summary>The virtual key code of the event (as a WinForms <see cref="Keys"/> value).</summary>
    public Keys KeyCode { get; }

    /// <summary>True when the event was software-injected (<c>LLKHF_INJECTED</c>), false for real hardware input.</summary>
    public bool Injected { get; }

    /// <summary>Set to true to suppress the key from further processing by other applications.</summary>
    public bool Handled { get; set; }
}
