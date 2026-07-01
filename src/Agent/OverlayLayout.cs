// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;

namespace MCEControl;

/// <summary>
/// Geometry for the on-screen command overlay (#119): where on the screen it sits — the left or right
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
}
