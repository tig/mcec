// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

// The WM_*/PBT_*/DEVICE_NOTIFY_*/GUID_* identifiers mirror their winuser.h/winnt.h names.
// ReSharper disable InconsistentNaming

namespace MCEControl.Hooks;

/// <summary>
/// The user32 power-setting-notification P/Invoke surface and the WM_POWERBROADCAST constants used
/// by <see cref="UserActivityMonitorService"/>'s presence detection (and <see cref="MainWindow"/>'s
/// WndProc dispatch). First-party since #214; this descends from the vendored
/// Gma.UserActivityMonitor fork's <c>NativeMethods</c>; only the members MCEC uses were kept.
/// </summary>
/// <remarks>
/// https://docs.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast and
/// https://docs.microsoft.com/en-us/windows/win32/power/power-setting-guids
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "P/Invokes are grouped thematically per subsystem, matching the repo's existing Win32 grouping.")]
internal static class PowerNativeMethods {
    private const string User32 = "user32.dll";

    /// <summary>Notifies applications of a power-management event (winuser.h).</summary>
    public const int WM_POWERBROADCAST = 0x0218;

    /// <summary>WM_POWERBROADCAST wParam: a power setting changed (a POWERBROADCAST_SETTING follows in lParam).</summary>
    public const int PBT_POWERSETTINGCHANGE = 0x8013;

    /// <summary>RegisterPowerSettingNotification flag: hRecipient is a window handle.</summary>
    public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    // Session-specific notification of user activity/presence (Present, NotPresent, Inactive).
    // Sent only to interactive-session registrants. {3C0F4548-C03F-4c4d-B9F2-237EDE686376}
    public static Guid GUID_SESSION_USER_PRESENCE = new(0x3c0f4548, 0xc03f, 0x4c4d, 0xb9, 0xf2, 0x23, 0x7e, 0xde, 0x68, 0x63, 0x76);

    // Whether the system is entering or exiting 'away mode'. {98A7F580-01F7-48AA-9C0F-44352C29E5C0}
    public static Guid GUID_SYSTEM_AWAYMODE = new(0x98A7F580, 0x01F7, 0x48AA, 0x9C, 0x0F, 0x44, 0x35, 0x2C, 0x29, 0xE5, 0xC0);

    // Whether the monitor is on or off. {02731015-4510-4526-99E6-E5A17EBD1AEA}
    public static Guid GUID_MONITOR_POWER_ON = new(0x02731015, 0x4510, 0x4526, 0x99, 0xE6, 0xE5, 0xA1, 0x7E, 0xBD, 0x1A, 0xEA);

    /// <summary>
    /// Registers the recipient window to receive WM_POWERBROADCAST/PBT_POWERSETTINGCHANGE messages
    /// for the given power-setting GUID. Returns a notification handle for
    /// <see cref="UnregisterPowerSettingNotification"/>, or <see cref="IntPtr.Zero"/> on failure.
    /// </summary>
    [DllImport(User32, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    /// <summary>
    /// Unregisters a power-setting notification previously registered with
    /// <see cref="RegisterPowerSettingNotification"/>.
    /// </summary>
    [DllImport(User32, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
