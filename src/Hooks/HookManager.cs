// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace MCEControl.Hooks;

/// <summary>
/// Monitors mouse and keyboard activity globally (system-wide, also outside of the application) via
/// low-level Windows hooks (WH_MOUSE_LL / WH_KEYBOARD_LL) and surfaces it as .NET events. The hooks
/// are installed lazily when the first handler subscribes and uninstalled when the last one detaches
/// (issue #197 — attach/detach must stay symmetric).
///
/// <para><b>First-party since #214.</b> This code descends from the vendored
/// <c>Gma.UserActivityMonitor</c> library (George Mamaladze's 2004 CodeProject sample,
/// https://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C),
/// which has no upstream to return to and had diverged materially: the <see cref="KeyDownExt"/>/
/// <see cref="KeyUpExt"/> injected-flag surface is load-bearing for the emergency stop (#135), and
/// the P/Invoke layer was rewritten for x64 correctness (#210). Only the fraction MCEC actually uses
/// was kept — the unused Ext mouse events, the <c>KeyPress</c>/ToAscii path (which consumed dead-key
/// state system-wide, #198), the mouse-wheel event, and the non-LL hook constants were deleted.</para>
///
/// <para>Handlers run INSIDE the hook callback, before CallNextHookEx — they must stay cheap.
/// Windows silently evicts a low-level hook whose callback exceeds LowLevelHooksTimeout, and the
/// emergency-stop hotkey rides the keyboard hook (#198).</para>
/// </summary>
public static partial class HookManager {
    //################################################################
    #region Mouse events

    private static event MouseEventHandler? _mouseMove;

    /// <summary>
    /// Occurs when the mouse pointer is moved.
    /// </summary>
    public static event MouseEventHandler MouseMove {
        add {
            EnsureSubscribedToGlobalMouseEvents();
            _mouseMove += value;
        }

        remove {
            _mouseMove -= value;
            TryUnsubscribeFromGlobalMouseEvents();
        }
    }

    private static event MouseEventHandler? _mouseClick;

    /// <summary>
    /// Occurs when a click was performed by the mouse.
    /// </summary>
    public static event MouseEventHandler MouseClick {
        add {
            EnsureSubscribedToGlobalMouseEvents();
            _mouseClick += value;
        }
        remove {
            _mouseClick -= value;
            TryUnsubscribeFromGlobalMouseEvents();
        }
    }

    private static event MouseEventHandler? _mouseDown;

    /// <summary>
    /// Occurs when a mouse button is pressed.
    /// </summary>
    public static event MouseEventHandler MouseDown {
        add {
            EnsureSubscribedToGlobalMouseEvents();
            _mouseDown += value;
        }
        remove {
            _mouseDown -= value;
            TryUnsubscribeFromGlobalMouseEvents();
        }
    }

    private static event MouseEventHandler? _mouseUp;

    /// <summary>
    /// Occurs when a mouse button is released.
    /// </summary>
    public static event MouseEventHandler MouseUp {
        add {
            EnsureSubscribedToGlobalMouseEvents();
            _mouseUp += value;
        }
        remove {
            _mouseUp -= value;
            TryUnsubscribeFromGlobalMouseEvents();
        }
    }

    private static event MouseEventHandler? _mouseDoubleClick;

    // The double click event is not provided directly by the hook. To fire it we monitor MouseUp
    // and fire when the same button goes up twice within the system double-click time.

    /// <summary>
    /// Occurs when a double click was performed by the mouse.
    /// </summary>
    public static event MouseEventHandler MouseDoubleClick {
        add {
            EnsureSubscribedToGlobalMouseEvents();
            if (_mouseDoubleClick == null) {
                // A timer to monitor the interval between two clicks (the system double-click time).
                _doubleClickTimer = new Timer {
                    Interval = HookNativeMethods.GetDoubleClickTime(),
                    // Not started yet; it starts when a click occurs.
                    Enabled = false,
                };
                _doubleClickTimer.Tick += DoubleClickTimeElapsed;
                MouseUp += OnMouseUp;
            }
            _mouseDoubleClick += value;
        }
        remove {
            if (_mouseDoubleClick != null) {
                _mouseDoubleClick -= value;
                if (_mouseDoubleClick == null) {
                    // Stop monitoring mouse up and dispose the timer.
                    MouseUp -= OnMouseUp;
                    _doubleClickTimer!.Tick -= DoubleClickTimeElapsed;
                    _doubleClickTimer.Dispose();
                    _doubleClickTimer = null;
                }
            }
            TryUnsubscribeFromGlobalMouseEvents();
        }
    }

    // Remembers which button was clicked first — a double-click must repeat the same button.
    private static MouseButtons _prevClickedButton;
    // The timer monitoring the interval between two clicks.
    private static Timer? _doubleClickTimer;

    private static void DoubleClickTimeElapsed(object? sender, EventArgs e) {
        // Timer elapsed and no second click occurred.
        _doubleClickTimer!.Enabled = false;
        _prevClickedButton = MouseButtons.None;
    }

    /// <summary>
    /// Monitors mouse-up events to fire <see cref="MouseDoubleClick"/> when two clicks of the same
    /// button occur within the system double-click time.
    /// </summary>
    /// <param name="sender">Always null (the events are static).</param>
    /// <param name="e">Information about the click.</param>
    private static void OnMouseUp(object? sender, MouseEventArgs e) {
        if (e.Clicks < 1) {
            return;
        }
        if (e.Button.Equals(_prevClickedButton)) {
            // Second click on the same button within the interval: fire double click.
            _mouseDoubleClick?.Invoke(null, e);
            _doubleClickTimer!.Enabled = false;
            _prevClickedButton = MouseButtons.None;
        }
        else {
            // First click: start the timer.
            _doubleClickTimer!.Enabled = true;
            _prevClickedButton = e.Button;
        }
    }
    #endregion

    //################################################################
    #region Keyboard events

    private static event KeyEventHandler? _keyUp;

    /// <summary>
    /// Occurs when a key is released.
    /// </summary>
    public static event KeyEventHandler KeyUp {
        add {
            EnsureSubscribedToGlobalKeyboardEvents();
            _keyUp += value;
        }
        remove {
            _keyUp -= value;
            TryUnsubscribeFromGlobalKeyboardEvents();
        }
    }

    private static event KeyEventHandler? _keyDown;

    /// <summary>
    /// Occurs when a key is pressed.
    /// </summary>
    public static event KeyEventHandler KeyDown {
        add {
            EnsureSubscribedToGlobalKeyboardEvents();
            _keyDown += value;
        }
        remove {
            _keyDown -= value;
            TryUnsubscribeFromGlobalKeyboardEvents();
        }
    }

    private static event EventHandler<GlobalKeyEventArgs>? _keyDownExt;

    /// <summary>
    /// Occurs when a key is pressed, with the software-injected flag exposed (see
    /// <see cref="GlobalKeyEventArgs.Injected"/>). Used by the emergency-stop (#135) so the panic hotkey
    /// reacts to real hardware input only and can never be tripped or defeated by injected keystrokes.
    /// </summary>
    public static event EventHandler<GlobalKeyEventArgs> KeyDownExt {
        add {
            EnsureSubscribedToGlobalKeyboardEvents();
            _keyDownExt += value;
        }
        remove {
            _keyDownExt -= value;
            TryUnsubscribeFromGlobalKeyboardEvents();
        }
    }

    private static event EventHandler<GlobalKeyEventArgs>? _keyUpExt;

    /// <summary>
    /// Occurs when a key is released, with the software-injected flag exposed. The emergency-stop tracks
    /// physically-held modifiers via this so a released modifier never leaves the chord half-armed.
    /// </summary>
    public static event EventHandler<GlobalKeyEventArgs> KeyUpExt {
        add {
            EnsureSubscribedToGlobalKeyboardEvents();
            _keyUpExt += value;
        }
        remove {
            _keyUpExt -= value;
            TryUnsubscribeFromGlobalKeyboardEvents();
        }
    }

    #endregion
}
