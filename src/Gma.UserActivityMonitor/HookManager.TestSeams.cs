// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace Gma.UserActivityMonitor;

/// <summary>
/// Test seams (InternalsVisibleTo MCEControl.xUnit) for <see cref="HookManager"/>. They expose the
/// subscribe/unsubscribe bookkeeping so tests can verify consumers detach symmetrically — the
/// regression guarded is issue #197, where a consumer that never detached left the global
/// WH_MOUSE_LL/WH_KEYBOARD_LL hooks installed system-wide and stacked duplicate handlers on every
/// Stop/Start cycle — without installing real global hooks (hosted CI has no interactive desktop).
/// </summary>
public static partial class HookManager {
    /// <summary>
    /// Sentinel stored in the hook-handle fields when <see cref="SuppressRealHooksForTesting"/> is
    /// set, so install/uninstall state stays observable without a real hook. A real
    /// SetWindowsHookEx handle is never -1.
    /// </summary>
    private const int FakeHookHandle = -1;

    /// <summary>
    /// When true, the real SetWindowsHookEx/UnhookWindowsHookEx calls are skipped while ALL of the
    /// subscribe/unsubscribe bookkeeping still runs (handle fields get <see cref="FakeHookHandle"/>).
    /// For tests only — never set in production code.
    /// </summary>
    internal static bool SuppressRealHooksForTesting { get; set; }

    /// <summary>True while the low-level mouse hook is installed (real or fake).</summary>
    internal static bool IsMouseHookInstalled => s_MouseHookHandle != 0;

    /// <summary>True while the low-level keyboard hook is installed (real or fake).</summary>
    internal static bool IsKeyboardHookInstalled => s_KeyboardHookHandle != 0;

    internal static int KeyDownSubscriberCount => SubscriberCount(s_KeyDown);
    internal static int KeyUpSubscriberCount => SubscriberCount(s_KeyUp);
    internal static int KeyPressSubscriberCount => SubscriberCount(s_KeyPress);
    internal static int KeyDownExtSubscriberCount => SubscriberCount(s_KeyDownExt);
    internal static int KeyUpExtSubscriberCount => SubscriberCount(s_KeyUpExt);
    internal static int MouseMoveSubscriberCount => SubscriberCount(s_MouseMove);
    internal static int MouseClickSubscriberCount => SubscriberCount(s_MouseClick);
    internal static int MouseDownSubscriberCount => SubscriberCount(s_MouseDown);
    internal static int MouseUpSubscriberCount => SubscriberCount(s_MouseUp);
    internal static int MouseDoubleClickSubscriberCount => SubscriberCount(s_MouseDoubleClick);
    internal static int MouseWheelSubscriberCount => SubscriberCount(s_MouseWheel);
    internal static int MouseMoveExtSubscriberCount => SubscriberCount(s_MouseMoveExt);
    internal static int MouseClickExtSubscriberCount => SubscriberCount(s_MouseClickExt);

    private static int SubscriberCount(Delegate? handler) => handler?.GetInvocationList().Length ?? 0;
}
