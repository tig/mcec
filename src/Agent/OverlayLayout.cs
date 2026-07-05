// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;

namespace MCEControl;

/// <summary>
/// Geometry for the on-screen command overlay (#119): where on the screen it sits; the left or right
/// <c>fraction</c> of the working area (≈30%), inset by a small margin. Pure so it is unit-testable
/// without a screen.
/// </summary>
public static class OverlayLayout {
    /// <summary>
    /// The overlay rectangle: the <paramref name="side"/> <paramref name="fraction"/> of
    /// <paramref name="workingArea"/>, full height, inset by <paramref name="margin"/> px on every side.
    /// <paramref name="fraction"/> is clamped to [0.1, 1.0].
    /// </summary>
    public static Rectangle ForSide(Rectangle workingArea, double fraction, OverlayPosition side, int margin = 12) {
        fraction = Math.Clamp(fraction, 0.1, 1.0);
        int width = (int)(workingArea.Width * fraction);
        int x = side == OverlayPosition.Left ? workingArea.Left : workingArea.Right - width;
        Rectangle rect = new(x, workingArea.Top, width, workingArea.Height);
        rect.Inflate(-margin, -margin);
        return rect;
    }

    /// <summary>
    /// Approximate on-screen height of one command box (the 14pt bold line plus its padding and the
    /// inter-box gap). Deliberately a slight under-estimate so <see cref="MaxLines"/> errs generous:
    /// the real limiter is the geometric "ran out of room" break in the renderer, so the feed cap
    /// only needs to be large enough not to trim before the screen edge does.
    /// </summary>
    internal const int ApproxLineStridePx = 28;

    /// <summary>The smallest feed cap; keeps a usable history even on a very short overlay.</summary>
    internal const int MinLines = 8;

    /// <summary>
    /// How many command lines the feed should retain to fill an overlay <paramref name="heightPx"/>
    /// tall. The old fixed cap of 8 left tall screens mostly empty (lines stacked at the bottom); a
    /// height-derived cap lets the feed scroll the full height. Floored at <see cref="MinLines"/> and
    /// safe for zero/negative heights.
    /// </summary>
    public static int MaxLines(int heightPx) =>
        Math.Max(MinLines, heightPx / ApproxLineStridePx);

    /// <summary>
    /// The persistent "MCEC is being controlled" banner text (#266): a plain-language reminder that an
    /// agent may be driving this machine, plus how to stop it. <paramref name="hotkeyDisplay"/> is the
    /// operator's configured emergency-stop chord (e.g. <c>Ctrl+Alt+Shift+S</c>) so the hint stays correct
    /// if the chord is reconfigured. Pure so it is unit-testable without a screen.
    /// </summary>
    public static string ControlBannerText(string? hotkeyDisplay) {
        string chord = string.IsNullOrWhiteSpace(hotkeyDisplay) ? "the emergency-stop hotkey" : hotkeyDisplay;
        return $"MCEC is being controlled; to stop press {chord}";
    }
}
