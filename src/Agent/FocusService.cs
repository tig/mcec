// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MCEControl;

/// <summary>
/// Verified foreground/focus primitives for the <c>focus</c> agent tool and the focus/foreground
/// diagnostics (#91, #270). Every operation here VERIFIES its result against the OS rather than trusting
/// a Win32 bool: <see cref="BringToForeground"/> confirms with <c>GetForegroundWindow</c> after the
/// AttachThreadInput dance, and <see cref="IsFocusInWindow"/> reads the target GUI thread's
/// <c>GUITHREADINFO</c> to prove keyboard focus actually landed. The verified booleans are what let a
/// command emit the closed taxonomy's <c>foreground</c> and <c>focus</c> categories honestly (they were
/// dead before; the taxonomy and the connect-time guidance were written ahead of any producer).
///
/// <para>These run on the calling thread (the MCP worker / dispatcher, never a WinForms message loop):
/// SetForegroundWindow and AttachThreadInput from a background thread are the documented pattern for a
/// non-foreground process (MCEC is a background server driving another app), and reading GUITHREADINFO is
/// thread-agnostic.</para>
/// </summary>
public static class FocusService {
    /// <summary>How long <see cref="BringToForeground"/> re-checks <c>GetForegroundWindow</c> after asking:
    /// the foreground switch is asynchronous, so a single immediate read can miss a change that lands a few
    /// milliseconds later. Kept short so a genuinely refused activation still fails fast.</summary>
    private const int ForegroundConfirmTimeoutMs = 300;
    private const int ForegroundPollMs = 30;

    /// <summary>How long <see cref="ConfirmFocusInWindow"/> re-checks after a click/SetFocus: the focus
    /// change a click triggers is delivered through the target's message queue, so it can land a few
    /// milliseconds after the synthesized input returns.</summary>
    private const int FocusConfirmTimeoutMs = 250;

    /// <summary>
    /// Brings <paramref name="hwnd"/>'s top-level window to the foreground and RETURNS WHETHER IT LANDED
    /// (verified with <c>GetForegroundWindow</c>), not whether the API returned true. Restores the window
    /// first if minimized (a minimized window can never hold focus). Uses AttachThreadInput to defeat the
    /// foreground lock Windows applies when the caller is not already the foreground process. A false
    /// return is the detectable <c>foreground</c> case: the OS refused to activate the target (foreground
    /// lock, a modal on another app, a full-screen exclusive window).
    /// </summary>
    public static bool BringToForeground(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) {
            return false;
        }
        IntPtr targetRoot = RootOf(hwnd);
        if (IsForeground(targetRoot)) {
            return true;
        }

        IntPtr fg = AgentNativeMethods.GetForegroundWindow();
        uint thisThread = AgentNativeMethods.GetCurrentThreadId();
        uint fgThread = fg == IntPtr.Zero ? 0 : AgentNativeMethods.GetWindowThreadProcessId(fg, out _);
        uint targetThread = AgentNativeMethods.GetWindowThreadProcessId(hwnd, out _);

        // Attach our input queue to the outgoing foreground thread (and the target's) for the duration of
        // the call so SetForegroundWindow is honoured instead of throttled to a taskbar flash.
        bool attachedFg = fgThread != 0 && fgThread != thisThread && AgentNativeMethods.AttachThreadInput(thisThread, fgThread, true);
        bool attachedTarget = targetThread != 0 && targetThread != thisThread && targetThread != fgThread
            && AgentNativeMethods.AttachThreadInput(thisThread, targetThread, true);
        try {
            if (AgentNativeMethods.IsIconic(hwnd)) {
                AgentNativeMethods.ShowWindow(hwnd, AgentNativeMethods.SW_RESTORE);
            }
            AgentNativeMethods.BringWindowToTop(hwnd);
            AgentNativeMethods.SetForegroundWindow(hwnd);
        }
        finally {
            if (attachedTarget) {
                AgentNativeMethods.AttachThreadInput(thisThread, targetThread, false);
            }
            if (attachedFg) {
                AgentNativeMethods.AttachThreadInput(thisThread, fgThread, false);
            }
        }

        return ConfirmForeground(targetRoot);
    }

    /// <summary>
    /// True when keyboard focus currently sits on <paramref name="hwnd"/> or any descendant of its
    /// top-level window, read from the target GUI thread's <c>GUITHREADINFO</c>. This is the verification
    /// the focus tool gates on: a zero focus window (the thread holds no focus at all) or a focus window
    /// under a DIFFERENT top-level owner both mean focus did NOT land where we drove it; the detectable
    /// <c>focus</c> case.
    /// </summary>
    public static bool IsFocusInWindow(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) {
            return false;
        }
        IntPtr focus = FocusedWindow(hwnd);
        return focus != IntPtr.Zero && RootOf(focus) == RootOf(hwnd);
    }

    /// <summary>Polls <see cref="IsFocusInWindow"/> briefly, since the focus change a click triggers is
    /// delivered asynchronously through the target's message queue after the synthesized input returns.</summary>
    public static bool ConfirmFocusInWindow(IntPtr hwnd) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            if (IsFocusInWindow(hwnd)) {
                return true;
            }
            if (sw.ElapsedMilliseconds >= FocusConfirmTimeoutMs) {
                return false;
            }
            Thread.Sleep(ForegroundPollMs);
        }
    }

    /// <summary>
    /// The window with keyboard focus in <paramref name="hwnd"/>'s GUI thread, or <see cref="IntPtr.Zero"/>
    /// when the thread has no focus (typically because it is not the foreground thread) or the query fails.
    /// Reported in the focus tool's result so an agent can see exactly which control took focus.
    /// </summary>
    public static IntPtr FocusedWindow(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) {
            return IntPtr.Zero;
        }
        uint tid = AgentNativeMethods.GetWindowThreadProcessId(hwnd, out _);
        if (tid == 0) {
            return IntPtr.Zero;
        }
        GuiThreadInfo gti = new() { Cb = Marshal.SizeOf<GuiThreadInfo>() };
        return AgentNativeMethods.GetGUIThreadInfo(tid, ref gti) ? gti.HwndFocus : IntPtr.Zero;
    }

    /// <summary>The top-level owner of <paramref name="hwnd"/> (GA_ROOT), or the handle itself when it has none.</summary>
    public static IntPtr RootOf(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) {
            return IntPtr.Zero;
        }
        IntPtr root = AgentNativeMethods.GetAncestor(hwnd, AgentNativeMethods.GA_ROOT);
        return root == IntPtr.Zero ? hwnd : root;
    }

    /// <summary>Polls <c>GetForegroundWindow</c> briefly, since the foreground switch is asynchronous.</summary>
    private static bool ConfirmForeground(IntPtr targetRoot) {
        Stopwatch sw = Stopwatch.StartNew();
        while (true) {
            if (IsForeground(targetRoot)) {
                return true;
            }
            if (sw.ElapsedMilliseconds >= ForegroundConfirmTimeoutMs) {
                return false;
            }
            Thread.Sleep(ForegroundPollMs);
        }
    }

    private static bool IsForeground(IntPtr targetRoot) =>
        targetRoot != IntPtr.Zero && RootOf(AgentNativeMethods.GetForegroundWindow()) == targetRoot;
}
