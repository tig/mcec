// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

namespace MCEControl.Hooks;

/// <summary>
/// Signature of a low-level hook callback passed to <see cref="HookNativeMethods.SetWindowsHookEx"/>.
/// For WH_KEYBOARD_LL/WH_MOUSE_LL the callback runs in the installing process on the thread that
/// installed the hook (which must pump messages).
/// </summary>
/// <param name="nCode">Hook code; when negative the callback must chain to CallNextHookEx untouched.</param>
/// <param name="wParam">The window-message identifier (WM_KEYDOWN, WM_LBUTTONDOWN, ...).</param>
/// <param name="lParam">Pointer to a KBDLLHOOKSTRUCT / MSLLHOOKSTRUCT with the event details.</param>
internal delegate int HookProc(int nCode, int wParam, IntPtr lParam);

/// <summary>
/// The user32 P/Invoke surface for <see cref="HookManager"/>'s global low-level keyboard/mouse hooks,
/// plus the winuser.h constants its hook callbacks interpret. First-party since #214 (this code
/// descends from the vendored Gma.UserActivityMonitor fork — see <see cref="HookManager"/>).
/// </summary>
/// <remarks>
/// The <c>hMod</c> argument to <see cref="SetWindowsHookEx"/> is deliberately passed as
/// <see cref="IntPtr.Zero"/> by callers: for low-level hooks the callback runs in the installing
/// process, so the system never uses hMod and NULL is valid (see the SetWindowsHookExW docs; #210).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "P/Invokes are grouped thematically per subsystem, matching the repo's existing Win32 grouping.")]
internal static class HookNativeMethods {
    private const string User32 = "user32.dll";

    /// <summary>Installs a hook that monitors low-level keyboard input events (winuser.h).</summary>
    internal const int WH_KEYBOARD_LL = 13;

    /// <summary>Installs a hook that monitors low-level mouse input events (winuser.h).</summary>
    internal const int WH_MOUSE_LL = 14;

    /// <summary>Posted when the user presses the left mouse button (winuser.h).</summary>
    internal const int WM_LBUTTONDOWN = 0x201;

    /// <summary>Posted when the user releases the left mouse button (winuser.h).</summary>
    internal const int WM_LBUTTONUP = 0x202;

    /// <summary>Posted when the user double-clicks the left mouse button (winuser.h).</summary>
    internal const int WM_LBUTTONDBLCLK = 0x203;

    /// <summary>Posted when the user presses the right mouse button (winuser.h).</summary>
    internal const int WM_RBUTTONDOWN = 0x204;

    /// <summary>Posted when the user releases the right mouse button (winuser.h).</summary>
    internal const int WM_RBUTTONUP = 0x205;

    /// <summary>Posted when the user double-clicks the right mouse button (winuser.h).</summary>
    internal const int WM_RBUTTONDBLCLK = 0x206;

    /// <summary>Posted when a nonsystem key is pressed (winuser.h).</summary>
    internal const int WM_KEYDOWN = 0x100;

    /// <summary>Posted when a nonsystem key is released (winuser.h).</summary>
    internal const int WM_KEYUP = 0x101;

    /// <summary>Posted when a key is pressed while ALT is held (or F10) (winuser.h).</summary>
    internal const int WM_SYSKEYDOWN = 0x104;

    /// <summary>Posted when a key pressed while ALT was held is released (winuser.h).</summary>
    internal const int WM_SYSKEYUP = 0x105;

    /// <summary>
    /// KBDLLHOOKSTRUCT.flags bit: the event was synthesized by software (SendInput) rather than
    /// pressed on real hardware. The emergency-stop (#135) uses this to react to physical input only.
    /// </summary>
    internal const int LLKHF_INJECTED = 0x10;

    /// <summary>
    /// Installs an application-defined hook procedure into a hook chain. Returns the hook handle
    /// (a pointer-sized HHOOK; #210), or <see cref="IntPtr.Zero"/> on failure (call GetLastError).
    /// </summary>
    [DllImport(User32, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

    /// <summary>
    /// Removes a hook procedure installed by <see cref="SetWindowsHookEx"/>. Returns false on
    /// failure (call GetLastError).
    /// </summary>
    [DllImport(User32, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr idHook);

    /// <summary>
    /// Passes the hook information to the next hook procedure in the current hook chain. A hook
    /// procedure must return this value unless it swallows the event.
    /// </summary>
    [DllImport(User32, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

    /// <summary>
    /// Retrieves the current double-click time in milliseconds (the maximum interval between two
    /// clicks for them to count as a double-click).
    /// </summary>
    [DllImport(User32)]
    internal static extern int GetDoubleClickTime();
}
