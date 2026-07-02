// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static MCEControl.Hooks.HookNativeMethods;

namespace MCEControl.Hooks;

public static partial class HookManager {
    //##############################################################################
    #region Mouse hook processing

    /// <summary>
    /// Keeps the delegate passed to unmanaged code alive. When passing delegates to unmanaged code
    /// they must be kept referenced by the managed application until it is guaranteed they will
    /// never be called, or the GC may collect them out from under the hook.
    /// </summary>
    private static HookProc? _mouseDelegate;

    /// <summary>
    /// The handle to the installed mouse hook (a pointer-sized HHOOK; #210), or
    /// <see cref="IntPtr.Zero"/> when not installed.
    /// </summary>
    private static IntPtr _mouseHookHandle;

    private static int _oldX;
    private static int _oldY;

    /// <summary>
    /// The WH_MOUSE_LL callback: decodes the low-level mouse event and raises the corresponding
    /// managed events. Runs on the hook-installing thread before other applications see the event —
    /// subscribers must stay cheap (see the class remarks).
    /// </summary>
    private static int MouseHookProc(int nCode, int wParam, IntPtr lParam) {
        if (nCode >= 0) {
            MouseLLHookStruct mouseHookStruct = Marshal.PtrToStructure<MouseLLHookStruct>(lParam);

            // Detect which button (if any) and whether it went down/up. Only the events MCEC
            // consumes are decoded — X-button and wheel messages fall through untouched (#214).
            MouseButtons button = MouseButtons.None;
            int clickCount = 0;
            bool mouseDown = false;
            bool mouseUp = false;

            switch (wParam) {
                case WM_LBUTTONDOWN:
                    mouseDown = true;
                    button = MouseButtons.Left;
                    clickCount = 1;
                    break;
                case WM_LBUTTONUP:
                    mouseUp = true;
                    button = MouseButtons.Left;
                    clickCount = 1;
                    break;
                case WM_LBUTTONDBLCLK:
                    button = MouseButtons.Left;
                    clickCount = 2;
                    break;
                case WM_RBUTTONDOWN:
                    mouseDown = true;
                    button = MouseButtons.Right;
                    clickCount = 1;
                    break;
                case WM_RBUTTONUP:
                    mouseUp = true;
                    button = MouseButtons.Right;
                    clickCount = 1;
                    break;
                case WM_RBUTTONDBLCLK:
                    button = MouseButtons.Right;
                    clickCount = 2;
                    break;
                default:
                    break;
            }

            MouseEventArgs e = new(
                button,
                clickCount,
                mouseHookStruct.Point.X,
                mouseHookStruct.Point.Y,
                0);

            if (mouseUp) {
                _mouseUp?.Invoke(null, e);
            }

            if (mouseDown) {
                _mouseDown?.Invoke(null, e);
            }

            if (clickCount > 0) {
                _mouseClick?.Invoke(null, e);
            }

            if (clickCount == 2) {
                _mouseDoubleClick?.Invoke(null, e);
            }

            // Raise MouseMove only when the coordinates actually changed.
            if (_mouseMove != null && (_oldX != mouseHookStruct.Point.X || _oldY != mouseHookStruct.Point.Y)) {
                _oldX = mouseHookStruct.Point.X;
                _oldY = mouseHookStruct.Point.Y;
                _mouseMove.Invoke(null, e);
            }
        }

        // Chain to the next hook.
        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private static void EnsureSubscribedToGlobalMouseEvents() {
        // Install the mouse hook only if it is not installed yet.
        if (_mouseHookHandle == IntPtr.Zero) {
            // Test seam (InternalsVisibleTo MCEControl.xUnit): skip the real hook so the
            // subscribe/unsubscribe bookkeeping is testable on hosted CI (no interactive desktop).
            if (SuppressRealHooksForTesting) {
                _mouseHookHandle = FakeHookHandle;
                return;
            }

            // See the field comment: keep the delegate alive for the lifetime of the hook.
            _mouseDelegate = MouseHookProc;
            // hMod is deliberately NULL — see the remarks on HookNativeMethods (#210).
            _mouseHookHandle = SetWindowsHookEx(
                WH_MOUSE_LL,
                _mouseDelegate,
                IntPtr.Zero,
                0);

            if (_mouseHookHandle == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    private static void TryUnsubscribeFromGlobalMouseEvents() {
        // If no subscribers remain, uninstall the hook.
        if (_mouseClick == null &&
            _mouseDown == null &&
            _mouseMove == null &&
            _mouseUp == null &&
            _mouseDoubleClick == null) {
            ForceUnsubscribeFromGlobalMouseEvents();
        }
    }

    private static void ForceUnsubscribeFromGlobalMouseEvents() {
        if (_mouseHookHandle != IntPtr.Zero) {
            // Test seam: a fake hook (see EnsureSubscribedToGlobalMouseEvents) is "uninstalled" by
            // resetting the bookkeeping — there is no real hook to unhook.
            if (_mouseHookHandle == FakeHookHandle) {
                _mouseHookHandle = IntPtr.Zero;
                _mouseDelegate = null;
                return;
            }

            bool result = UnhookWindowsHookEx(_mouseHookHandle);
            // Reset the handle and free the delegate for GC regardless of the outcome.
            _mouseHookHandle = IntPtr.Zero;
            _mouseDelegate = null;
            if (!result) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    #endregion

    //##############################################################################
    #region Keyboard hook processing

    /// <summary>
    /// Keeps the delegate passed to unmanaged code alive (see <see cref="_mouseDelegate"/>).
    /// </summary>
    private static HookProc? _keyboardDelegate;

    /// <summary>
    /// The handle to the installed keyboard hook (a pointer-sized HHOOK; #210), or
    /// <see cref="IntPtr.Zero"/> when not installed.
    /// </summary>
    private static IntPtr _keyboardHookHandle;

    /// <summary>
    /// The WH_KEYBOARD_LL callback: decodes the low-level keyboard event and raises the
    /// corresponding managed events. Runs on the hook-installing thread before other applications
    /// see the event — subscribers must stay cheap (see the class remarks).
    /// </summary>
    private static int KeyboardHookProc(int nCode, int wParam, IntPtr lParam) {
        // Set when any subscriber sets e.Handled — the event is then swallowed (not chained).
        bool handled = false;

        if (nCode >= 0) {
            KeyboardHookStruct keyboardHookStruct = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);

            // LLKHF_INJECTED: the event was synthesized by SendInput rather than pressed on real
            // hardware. Surfaced on the *Ext events so the emergency-stop (#135) can react to physical
            // input only — MCEC's own agent actuation injects keys, and the panic hotkey must be immune
            // to both accidental self-trip and deliberate self-defeat.
            bool injected = (keyboardHookStruct.Flags & LLKHF_INJECTED) != 0;
            bool isKeyDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            bool isKeyUp = wParam == WM_KEYUP || wParam == WM_SYSKEYUP;

            if (_keyDown != null && isKeyDown) {
                KeyEventArgs e = new((Keys)keyboardHookStruct.VirtualKeyCode);
                _keyDown.Invoke(null, e);
                handled = e.Handled;
            }

            if (_keyDownExt != null && isKeyDown) {
                GlobalKeyEventArgs ge = new((Keys)keyboardHookStruct.VirtualKeyCode, injected);
                _keyDownExt.Invoke(null, ge);
                handled = handled || ge.Handled;
            }

            if (_keyUpExt != null && isKeyUp) {
                GlobalKeyEventArgs ge = new((Keys)keyboardHookStruct.VirtualKeyCode, injected);
                _keyUpExt.Invoke(null, ge);
                handled = handled || ge.Handled;
            }

            if (_keyUp != null && isKeyUp) {
                KeyEventArgs e = new((Keys)keyboardHookStruct.VirtualKeyCode);
                _keyUp.Invoke(null, e);
                handled = handled || e.Handled;
            }
        }

        // If a subscriber handled the event, do not hand it off to other applications.
        if (handled) {
            return -1;
        }

        // Chain to the next hook.
        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private static void EnsureSubscribedToGlobalKeyboardEvents() {
        // Install the keyboard hook only if it is not installed yet.
        if (_keyboardHookHandle == IntPtr.Zero) {
            // Test seam (InternalsVisibleTo MCEControl.xUnit): skip the real hook so the
            // subscribe/unsubscribe bookkeeping is testable on hosted CI (no interactive desktop).
            if (SuppressRealHooksForTesting) {
                _keyboardHookHandle = FakeHookHandle;
                return;
            }

            // See the field comment: keep the delegate alive for the lifetime of the hook.
            _keyboardDelegate = KeyboardHookProc;
            // hMod is deliberately NULL — see the remarks on HookNativeMethods (#210).
            _keyboardHookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _keyboardDelegate,
                IntPtr.Zero,
                0);

            if (_keyboardHookHandle == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    private static void TryUnsubscribeFromGlobalKeyboardEvents() {
        // If no subscribers remain, uninstall the hook.
        if (_keyDown == null &&
            _keyUp == null &&
            _keyDownExt == null &&
            _keyUpExt == null) {
            ForceUnsubscribeFromGlobalKeyboardEvents();
        }
    }

    private static void ForceUnsubscribeFromGlobalKeyboardEvents() {
        if (_keyboardHookHandle != IntPtr.Zero) {
            // Test seam: a fake hook (see EnsureSubscribedToGlobalKeyboardEvents) is "uninstalled" by
            // resetting the bookkeeping — there is no real hook to unhook.
            if (_keyboardHookHandle == FakeHookHandle) {
                _keyboardHookHandle = IntPtr.Zero;
                _keyboardDelegate = null;
                return;
            }

            bool result = UnhookWindowsHookEx(_keyboardHookHandle);
            // Reset the handle and free the delegate for GC regardless of the outcome.
            _keyboardHookHandle = IntPtr.Zero;
            _keyboardDelegate = null;
            if (!result) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    #endregion
}
