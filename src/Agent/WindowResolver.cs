// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace MCEControl;

/// <summary>
/// Resolves and enumerates top-level windows for the agent commands. Targeting is by (in priority
/// order) explicit handle, foreground window, title substring (case-insensitive), process name
/// (exact, without ".exe"), or window class name (exact). This replaces the pixel-hunting / sleep
/// loops a GUI script otherwise grows.
/// </summary>
public static class WindowResolver {
    // Windows MCEC owns that must never be agent targets; notably the on-screen command overlay (#119).
    // The overlay annotates the screen; if it could be resolved by handle/foreground/process, an agent
    // would see and try to drive its own overlay. Handles are registered by the windows themselves.
    private static readonly HashSet<long> _ignoredHandles = [];

    /// <summary>Marks a window handle as never-a-target (e.g. the command overlay registers itself).</summary>
    public static void RegisterIgnoredWindow(long handle) {
        lock (_ignoredHandles) {
            _ignoredHandles.Add(handle);
        }
    }

    /// <summary>Removes a previously-ignored handle (on window close).</summary>
    public static void UnregisterIgnoredWindow(long handle) {
        lock (_ignoredHandles) {
            _ignoredHandles.Remove(handle);
        }
    }

    /// <summary>True if the handle is registered as never-a-target.</summary>
    public static bool IsIgnoredWindow(long handle) {
        lock (_ignoredHandles) {
            return _ignoredHandles.Contains(handle);
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
        // enumerated window; screenshotting/driving an arbitrary user window. Refuse instead.
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(className)) {
            return null;
        }

        foreach (WindowInfo info in EnumerateTopLevel()) {
            if (Matches(info, title, processName, className)) {
                return info;
            }
        }
        return null;
    }

    /// <summary>
    /// True when <paramref name="info"/> satisfies every non-empty filter, using the SAME rules
    /// <see cref="Resolve"/> applies: title substring (case-insensitive), process name (exact, without
    /// ".exe"), window class name (exact). An empty filter is not applied, so all-empty matches anything.
    /// </summary>
    public static bool Matches(WindowInfo info, string? title, string? processName, string? className) {
        if (!string.IsNullOrEmpty(title) &&
            info.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) {
            return false;
        }
        if (!string.IsNullOrEmpty(processName) &&
            !info.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        if (!string.IsNullOrEmpty(className) &&
            !info.ClassName.Equals(className, StringComparison.Ordinal)) {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Lists every visible, titled top-level window matching the given filters (all windows when no
    /// filter is given). Unlike <see cref="Resolve"/> this returns the WHOLE matching set rather than the
    /// first hit, so an agent can enumerate available targets and pick one; listing is not the
    /// silent-arbitrary-selection hazard <see cref="Resolve"/> guards against.
    /// </summary>
    public static List<WindowInfo> ListTopLevel(string? title, string? processName, string? className) {
        List<WindowInfo> matches = [];
        foreach (WindowInfo info in EnumerateTopLevel()) {
            if (Matches(info, title, processName, className)) {
                matches.Add(info);
            }
        }
        return matches;
    }

    /// <summary>
    /// Polls <see cref="ListTopLevel"/> until at least one window matches or <paramref name="timeoutMs"/>
    /// elapses, then returns whatever matched (empty on timeout). Lets an agent wait for a top-level
    /// window (a launching app, a modal about to appear) without external sleeps. A zero timeout returns
    /// the current matches after a single pass. The caller is responsible for requiring a filter; waiting
    /// for "any window" is meaningless and callers (the <c>windows</c> tool) refuse it.
    /// </summary>
    public static List<WindowInfo> WaitForTopLevel(string? title, string? processName, string? className, int timeoutMs, int pollMs = 100) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            List<WindowInfo> matches = ListTopLevel(title, processName, className);
            if (matches.Count > 0 || sw.ElapsedMilliseconds >= timeoutMs) {
                return matches;
            }
            Thread.Sleep(Math.Min(pollMs, Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds)));
        }
    }

    /// <summary>
    /// Polls <see cref="ListTopLevel"/> until NO window matches the filter (the target closed) or
    /// <paramref name="timeoutMs"/> elapses. Returns the STILL-matching windows: empty on success (all
    /// gone), non-empty on timeout (what did not close). A zero timeout checks once. Lets an agent wait
    /// for a dialog/app window to disappear (a modal closing) without external sleeps. The caller requires
    /// a filter; waiting for "any window" to disappear is meaningless (the <c>windows</c> tool refuses it).
    /// </summary>
    public static List<WindowInfo> WaitUntilGone(string? title, string? processName, string? className, int timeoutMs, int pollMs = 100) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            List<WindowInfo> matches = ListTopLevel(title, processName, className);
            if (matches.Count == 0 || sw.ElapsedMilliseconds >= timeoutMs) {
                return matches;
            }
            Thread.Sleep(Math.Min(pollMs, Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds)));
        }
    }

    /// <summary>
    /// Polls until the FOREGROUND window matches the filter or <paramref name="timeoutMs"/> elapses.
    /// Returns the matching foreground window on success, or null on timeout (the foreground never
    /// matched). A zero timeout checks the current foreground once. Lets an agent wait for its target
    /// (a just-launched app, a dialog) to actually take focus before sending keystrokes.
    /// </summary>
    public static WindowInfo? WaitForForeground(string? title, string? processName, string? className, int timeoutMs, int pollMs = 100) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            WindowInfo? fg = Resolve(null, null, null, null, foreground: true);
            if (fg is not null && Matches(fg, title, processName, className)) {
                return fg;
            }
            if (sw.ElapsedMilliseconds >= timeoutMs) {
                return null;
            }
            Thread.Sleep(Math.Min(pollMs, Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds)));
        }
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
            _ = AgentNativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            info.Title = sb.ToString();
        }

        StringBuilder cls = new(256);
        _ = AgentNativeMethods.GetClassName(hwnd, cls, cls.Capacity);
        info.ClassName = cls.ToString();

        _ = AgentNativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
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
