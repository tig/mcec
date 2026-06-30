// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MCEControl;

/// <summary>
/// Resolves and enumerates top-level windows for the agent commands. Targeting is by (in priority
/// order) explicit handle, foreground window, title substring (case-insensitive), process name
/// (exact, without ".exe"), or window class name (exact). This replaces the pixel-hunting / sleep
/// loops a GUI script otherwise grows.
/// </summary>
public static class WindowResolver {
    // Windows MCEC owns that must never be agent targets — notably the on-screen command overlay (#119).
    // The overlay annotates the screen; if it could be resolved by handle/foreground/process, an agent
    // would see and try to drive its own overlay. Handles are registered by the windows themselves.
    private static readonly HashSet<long> IgnoredHandles = [];

    /// <summary>Marks a window handle as never-a-target (e.g. the command overlay registers itself).</summary>
    public static void RegisterIgnoredWindow(long handle) {
        lock (IgnoredHandles) {
            IgnoredHandles.Add(handle);
        }
    }

    /// <summary>Removes a previously-ignored handle (on window close).</summary>
    public static void UnregisterIgnoredWindow(long handle) {
        lock (IgnoredHandles) {
            IgnoredHandles.Remove(handle);
        }
    }

    /// <summary>True if the handle is registered as never-a-target.</summary>
    public static bool IsIgnoredWindow(long handle) {
        lock (IgnoredHandles) {
            return IgnoredHandles.Contains(handle);
        }
    }

    /// <summary>
    /// Resolves a single target window. Returns null if nothing matches. Precedence: handle &gt;
    /// foreground &gt; (title / process / className filters applied to the enumerated top-level set).
    /// </summary>
    public static WindowInfo? Resolve(long? handle, string? title, string? processName, string? className, bool foreground) {
        if (handle is > 0) {
            if (IsIgnoredWindow(handle.Value)) {
                return null;
            }
            IntPtr h = new(handle.Value);
            return AgentNativeMethods.IsWindow(h) ? Describe(h) : null;
        }

        if (foreground) {
            IntPtr fg = AgentNativeMethods.GetForegroundWindow();
            return fg == IntPtr.Zero || IsIgnoredWindow(fg.ToInt64()) ? null : Describe(fg);
        }

        // SECURITY/CORRECTNESS: require at least one explicit criterion. Without this, a call with no
        // target (e.g. an MCP tool call with empty arguments) would silently match the first
        // enumerated window — screenshotting/driving an arbitrary user window. Refuse instead.
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(className)) {
            return null;
        }

        foreach (WindowInfo info in EnumerateTopLevel()) {
            if (!string.IsNullOrEmpty(title) &&
                info.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) {
                continue;
            }
            if (!string.IsNullOrEmpty(processName) &&
                !info.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (!string.IsNullOrEmpty(className) &&
                !info.ClassName.Equals(className, StringComparison.Ordinal)) {
                continue;
            }
            return info;
        }
        return null;
    }

    /// <summary>Enumerates visible, titled top-level windows.</summary>
    public static List<WindowInfo> EnumerateTopLevel() {
        List<WindowInfo> windows = [];
        AgentNativeMethods.EnumWindows((hwnd, _) => {
            if (!AgentNativeMethods.IsWindowVisible(hwnd)) {
                return true;
            }
            if (AgentNativeMethods.GetWindowTextLength(hwnd) == 0) {
                return true;
            }
            if (IsIgnoredWindow(hwnd.ToInt64())) {
                return true; // never surface MCEC's own overlay as a candidate target (#119)
            }
            windows.Add(Describe(hwnd));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    /// <summary>Builds a <see cref="WindowInfo"/> for a window handle.</summary>
    public static WindowInfo Describe(IntPtr hwnd) {
        WindowInfo info = new() { Handle = hwnd.ToInt64() };

        int len = AgentNativeMethods.GetWindowTextLength(hwnd);
        if (len > 0) {
            StringBuilder sb = new(len + 1);
            AgentNativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            info.Title = sb.ToString();
        }

        StringBuilder cls = new(256);
        AgentNativeMethods.GetClassName(hwnd, cls, cls.Capacity);
        info.ClassName = cls.ToString();

        AgentNativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        info.ProcessId = (int)pid;
        try {
            using Process p = Process.GetProcessById((int)pid);
            info.ProcessName = p.ProcessName;
        }
        catch (ArgumentException) {
            // Process exited between enumeration and lookup; leave ProcessName empty.
        }
        catch (InvalidOperationException) {
            // Same race; non-fatal.
        }

        if (AgentNativeMethods.GetWindowRect(hwnd, out NativeRect rect)) {
            info.X = rect.Left;
            info.Y = rect.Top;
            info.Width = rect.Width;
            info.Height = rect.Height;
        }
        return info;
    }
}
