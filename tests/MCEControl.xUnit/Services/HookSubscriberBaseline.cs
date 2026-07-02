//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using MCEControl.Hooks;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Snapshot of <see cref="HookManager" />'s per-event subscriber counts, so tests (issue #197) can
/// assert deltas rather than absolute counts and stay robust if anything else in the test process
/// subscribes to the static events.
/// </summary>
public sealed record HookSubscriberBaseline(
    int KeyDown,
    int KeyUp,
    int KeyDownExt,
    int KeyUpExt,
    int MouseMove,
    int MouseClick,
    int MouseDown,
    int MouseUp,
    int MouseDoubleClick) {
    /// <summary>Total keyboard-event subscribers; when zero, the keyboard hook must be uninstalled.</summary>
    public int KeyboardTotal => KeyDown + KeyUp + KeyDownExt + KeyUpExt;

    /// <summary>Total mouse-event subscribers; when zero, the mouse hook must be uninstalled.</summary>
    public int MouseTotal => MouseMove + MouseClick + MouseDown + MouseUp + MouseDoubleClick;

    public static HookSubscriberBaseline Capture() => new(
        HookManager.KeyDownSubscriberCount,
        HookManager.KeyUpSubscriberCount,
        HookManager.KeyDownExtSubscriberCount,
        HookManager.KeyUpExtSubscriberCount,
        HookManager.MouseMoveSubscriberCount,
        HookManager.MouseClickSubscriberCount,
        HookManager.MouseDownSubscriberCount,
        HookManager.MouseUpSubscriberCount,
        HookManager.MouseDoubleClickSubscriberCount);
}
