// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MCEControl;

/// <summary>
/// P/Invoke surface for the MCEC 3.0 agent observation features: window screenshotting
/// (<c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c>, which correctly captures DirectComposition
/// surfaces; WinUI 3 / WPF; that plain screen grabs return black for) and top-level window
/// enumeration/metadata for targeting. Kept separate from the security-focused
/// <c>Microsoft.Win32.Security.Win32</c> interop so the agent subsystem is self-contained.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "Agent P/Invokes are grouped thematically and kept separate from the security interop, matching the repo's existing Win32 grouping.")]
internal static class AgentNativeMethods {
    private const string User32 = "user32.dll";
    private const string Dwmapi = "dwmapi.dll";
    private const string Gdi32 = "gdi32.dll";
    private const string Shcore = "shcore.dll";

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
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport(Dwmapi)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out NativeRect pvAttribute, int cbAttribute);

    // --- Per-monitor DPI for the displays geometry query (#122) ---
    // System.Drawing.Point is blittable and matches the Win32 POINT layout, so MonitorFromPoint takes it
    // by value without a dedicated interop struct (which the one-type-per-file analyzer would reject here).

    [DllImport(User32)]
    public static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

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
        IntPtr hwnd, IntPtr hdcDst, ref System.Drawing.Point pptDst, ref System.Drawing.Size psize,
        IntPtr hdcSrc, ref System.Drawing.Point pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);
}
