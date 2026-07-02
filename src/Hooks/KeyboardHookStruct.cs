// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl.Hooks;

/// <summary>
/// The KBDLLHOOKSTRUCT structure: information about a low-level keyboard input event, marshaled
/// from the WH_KEYBOARD_LL callback's lParam.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardHookStruct {
    /// <summary>
    /// A virtual-key code in the range 1 to 254.
    /// </summary>
    public int VirtualKeyCode;
    /// <summary>
    /// A hardware scan code for the key.
    /// </summary>
    public int ScanCode;
    /// <summary>
    /// The extended-key flag, event-injected flag (LLKHF_INJECTED), context code, and
    /// transition-state flag.
    /// </summary>
    public int Flags;
    /// <summary>
    /// The time stamp for this message.
    /// </summary>
    public int Time;
    /// <summary>
    /// Extra information associated with the message.
    /// </summary>
    public int ExtraInfo;
}
