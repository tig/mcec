// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

// The struct name mirrors the Win32 POWERBROADCAST_SETTING declaration in winuser.h.
// ReSharper disable InconsistentNaming

namespace MCEControl.Hooks;

/// <summary>
/// The POWERBROADCAST_SETTING structure sent with PBT_POWERSETTINGCHANGE: identifies the power
/// setting that changed and carries data about the change (marshaled from WndProc's lParam).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct POWERBROADCAST_SETTING {
    public Guid PowerSetting;
    public uint DataLength;
    public byte Data;
}
