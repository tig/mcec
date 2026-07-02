// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// The complete window-messaging P/Invoke surface for the core app (#210): the handful of
/// user32 imports that <see cref="MainWindow"/>, <see cref="SendMessageCommand"/> and
/// <see cref="SetForegroundWindowCommand"/> actually call, plus the two message constants they
/// pass. This replaced the vendored <c>Microsoft.Win32.Security</c> fork (58 files / ~5,200
/// lines of dead token/ACL/SID code) that previously hosted these declarations. Other
/// subsystems keep their own thematic islands: <c>WindowsInput.Native.NativeMethods</c>
/// (SendInput), <c>Gma.UserActivityMonitor.NativeMethods</c>/<c>HookManager</c> (global hooks,
/// power notifications) and <c>AgentNativeMethods</c> (agent observation) — no import is
/// declared twice.
/// </summary>
/// <remarks>
/// Plain <c>[DllImport]</c> is deliberate: <c>[LibraryImport]</c> source generation emits
/// <c>unsafe</c> stubs and would force <c>AllowUnsafeBlocks</c> back on, which #210 removed.
/// CsWin32 is a possible follow-up (see the issue) but is not taken as a dependency here.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "P/Invokes are grouped thematically per subsystem, matching the repo's existing Win32 grouping.")]
internal static class Win32NativeMethods {
    private const string User32 = "user32.dll";

    /// <summary>WM_SYSCOMMAND — a window menu command (winuser.h).</summary>
    public const uint WM_SYSCOMMAND = 0x0112;

    /// <summary>SC_CLOSE — WM_SYSCOMMAND wParam: close the window (winuser.h).</summary>
    public const nint SC_CLOSE = 0xF060;

    // #203: WPARAM/LPARAM are pointer-sized (UINT_PTR/LONG_PTR), not 32-bit DWORDs.
    // Declared as nint so call sites can sign-extend stored ints correctly (e.g. the
    // built-in monitoron command's lParam of -1 must stay -1 on x64).
    [DllImport(User32, SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
