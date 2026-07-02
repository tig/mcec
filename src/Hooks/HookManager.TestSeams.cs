// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl.Hooks;

/// <summary>
/// Test seams (InternalsVisibleTo MCEControl.xUnit) for <see cref="HookManager"/>. They expose the
/// subscribe/unsubscribe bookkeeping so tests can verify consumers detach symmetrically; the
/// regression guarded is issue #197, where a consumer that never detached left the global
/// WH_MOUSE_LL/WH_KEYBOARD_LL hooks installed system-wide and stacked duplicate handlers on every
/// Stop/Start cycle; without installing real global hooks (hosted CI has no interactive desktop).
/// </summary>
public static partial class HookManager {
    /// <summary>
    /// Sentinel stored in the hook-handle fields when <see cref="SuppressRealHooksForTesting"/> is
    /// set, so install/uninstall state stays observable without a real hook. A real
    /// SetWindowsHookEx handle is never -1. (IntPtr since #210; hook handles are pointer-sized.)
    /// </summary>
    private static readonly IntPtr FakeHookHandle = new(-1);

    /// <summary>
    /// When true, the real SetWindowsHookEx/UnhookWindowsHookEx calls are skipped while ALL of the
    /// subscribe/unsubscribe bookkeeping still runs (handle fields get <see cref="FakeHookHandle"/>).
    /// For tests only; never set in production code.
    /// </summary>
    internal static bool SuppressRealHooksForTesting { get; set; }

    /// <summary>True while the low-level mouse hook is installed (real or fake).</summary>
    internal static bool IsMouseHookInstalled => _mouseHookHandle != IntPtr.Zero;

    /// <summary>True while the low-level keyboard hook is installed (real or fake).</summary>
    internal static bool IsKeyboardHookInstalled => _keyboardHookHandle != IntPtr.Zero;

    internal static int KeyDownSubscriberCount => SubscriberCount(_keyDown);
    internal static int KeyUpSubscriberCount => SubscriberCount(_keyUp);
    internal static int KeyDownExtSubscriberCount => SubscriberCount(_keyDownExt);
    internal static int KeyUpExtSubscriberCount => SubscriberCount(_keyUpExt);
    internal static int MouseMoveSubscriberCount => SubscriberCount(_mouseMove);
    internal static int MouseClickSubscriberCount => SubscriberCount(_mouseClick);
    internal static int MouseDownSubscriberCount => SubscriberCount(_mouseDown);
    internal static int MouseUpSubscriberCount => SubscriberCount(_mouseUp);
    internal static int MouseDoubleClickSubscriberCount => SubscriberCount(_mouseDoubleClick);

    private static int SubscriberCount(Delegate? handler) => handler?.GetInvocationList().Length ?? 0;
}
