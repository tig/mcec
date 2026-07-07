// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;
using System.Text;

// The SCREAMING_SNAKE constants below mirror the Win32 API names they stand for; renaming them would
// break the 1:1 mapping to the Windows SDK headers.
// ReSharper disable InconsistentNaming

namespace MCEControl;

/// <summary>
/// P/Invoke surface for the MCEC 3.0 agent observation features: window screenshotting
/// (<c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c>, which correctly captures DirectComposition
/// surfaces; WinUI 3 / WPF; that plain screen grabs return black for) and top-level window
/// enumeration/metadata for targeting. Kept separate from the other per-subsystem interop
/// interop islands so the agent subsystem is self-contained. Also declares
/// <c>GetForegroundWindow</c>, shared with SendMessageCommand (#210: one declaration per import).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "Agent P/Invokes are grouped thematically and kept separate from the security interop, matching the repo's existing Win32 grouping.")]
internal static class AgentNativeMethods {
    private const string User32 = "user32.dll";
    private const string Dwmapi = "dwmapi.dll";
    private const string Gdi32 = "gdi32.dll";
    private const string Shcore = "shcore.dll";
    private const string Kernel32 = "kernel32.dll";

    /// <summary>GetAncestor gaFlags: the root window (walks parents to the top-level owner). Used to map a
    /// focused child HWND back to its top-level window when verifying foreground/focus (#91, #270).</summary>
    public const uint GA_ROOT = 2;

    /// <summary>ShowWindow nCmdShow: minimize a window.</summary>
    public const int SW_MINIMIZE = 6;

    /// <summary>ShowWindow nCmdShow: maximize a window.</summary>
    public const int SW_MAXIMIZE = 3;

    /// <summary>ShowWindow nCmdShow: restore a minimized window (a minimized target can never take focus).</summary>
    public const int SW_RESTORE = 9;

    /// <summary>MonitorFromPoint flag: return the monitor nearest the point when it is on none of them.</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary>GetDpiForMonitor MONITOR_DPI_TYPE: the effective (scaled) DPI, honouring the user's scale.</summary>
    public const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>UpdateLayeredWindow flag: use the per-pixel alpha of the source bitmap.</summary>
    public const int ULW_ALPHA = 0x02;

    /// <summary>BLENDFUNCTION BlendOp: source-over alpha blend.</summary>
    public const byte AC_SRC_OVER = 0x00;

    /// <summary>BLENDFUNCTION AlphaFormat: the source bitmap has per-pixel alpha.</summary>
    public const byte AC_SRC_ALPHA = 0x01;

    /// <summary>SetWindowPos hWndInsertAfter: place the window at the top of the always-on-top band.</summary>
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    /// <summary>SetWindowPos flag: keep the current size (ignore cx/cy).</summary>
    public const uint SWP_NOSIZE = 0x0001;

    /// <summary>SetWindowPos flag: keep the current position (ignore X/Y).</summary>
    public const uint SWP_NOMOVE = 0x0002;

    /// <summary>SetWindowPos flag: do not activate the window (never steal focus from the driven app).</summary>
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>Render the full window content, including DirectComposition/DWM surfaces.</summary>
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    /// <summary>DWMWA_EXTENDED_FRAME_BOUNDS; the true visible bounds (excludes the invisible border).</summary>
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, out NativeRect lpRect);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hwnd, out NativeRect lpRect);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport(User32, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hwnd);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport(User32, SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    [DllImport(User32)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(Dwmapi)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out NativeRect pvAttribute, int cbAttribute);

    // --- Per-monitor DPI for the displays geometry query (#122) ---
    // System.Drawing.Point is blittable and matches the Win32 POINT layout, so MonitorFromPoint takes it
    // by value without a dedicated interop struct (which the one-type-per-file analyzer would reject here).

    [DllImport(User32)]
    public static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

    [DllImport(Shcore)]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    // --- Per-pixel-alpha layered window plumbing for the command overlay (#119) ---

    [DllImport(User32)]
    public static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport(User32)]
    public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport(Gdi32)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport(Gdi32)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hgdiobj);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
        IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    // --- Foreground/focus actuation and verification for the `focus` tool and diagnostics (#91, #270) ---
    // SetForegroundWindow alone is throttled by Windows' foreground lock when the caller is not itself the
    // foreground process (the common case: MCEC is a background server driving another app). The standard
    // workaround is to AttachThreadInput the calling thread to the current foreground thread for the
    // duration of the call, then verify the result with GetForegroundWindow rather than trusting the bool.

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hwnd);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    /// <summary>Walks a window's ancestors; with <see cref="GA_ROOT"/> returns its top-level owner.</summary>
    [DllImport(User32)]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    /// <summary>Reads a GUI thread's input state; <see cref="GuiThreadInfo.HwndFocus"/> is the window with
    /// keyboard focus in that thread, the signal the focus tool verifies against (#91, #270).</summary>
    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo lpgui);

    [DllImport(Kernel32)]
    public static extern uint GetCurrentThreadId();
}
