// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

// "LL" mirrors the Win32 MSLLHOOKSTRUCT/WH_MOUSE_LL naming this struct is the managed projection of.
// ReSharper disable InconsistentNaming

namespace MCEControl.Hooks;

/// <summary>
/// The MSLLHOOKSTRUCT structure: information about a low-level mouse input event, marshaled from
/// the WH_MOUSE_LL callback's lParam.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MouseLLHookStruct {
    /// <summary>
    /// The X- and Y-coordinates of the cursor, in screen coordinates.
    /// </summary>
    public Point Point;
    /// <summary>
    /// For WM_MOUSEWHEEL, the high-order word is the wheel delta (one click is WHEEL_DELTA, 120;
    /// positive is away from the user). For WM_XBUTTON* messages, the high-order word identifies
    /// the X button. Otherwise unused.
    /// </summary>
    public int MouseData;
    /// <summary>
    /// The event-injected flag: bit 0 (LLMHF_INJECTED) is set when the event was injected by
    /// software. Bits 1-15 are reserved.
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
