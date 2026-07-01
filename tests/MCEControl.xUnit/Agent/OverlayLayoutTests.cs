// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 overlay geometry: the right ~30% of the working area, inset by a margin.</summary>
public class OverlayLayoutTests {
    [Fact]
    public void ForSide_Right_IsRightAligned_AndAboutThirtyPercentWide() {
        Rectangle work = new(0, 0, 1000, 800);

        Rectangle r = OverlayLayout.ForSide(work, 0.30, OverlayPosition.Right, margin: 12);

        // 30% of 1000 = 300, inset 12 each side => 276 wide, right edge at 1000-12.
        Assert.Equal(276, r.Width);
        Assert.Equal(988, r.Right);
        Assert.Equal(12, r.Top);
        Assert.Equal(788, r.Bottom);
    }

    [Fact]
    public void ForSide_Left_IsLeftAligned() {
        Rectangle work = new(0, 0, 1000, 800);

        Rectangle r = OverlayLayout.ForSide(work, 0.30, OverlayPosition.Left, margin: 0);

        Assert.Equal(0, r.Left);
        Assert.Equal(300, r.Width);
    }

    [Fact]
    public void ForSide_RespectsWorkingAreaOrigin() {
        Rectangle work = new(100, 50, 1000, 800); // non-zero origin (taskbar/secondary monitor)

        Assert.Equal(work.Right, OverlayLayout.ForSide(work, 0.30, OverlayPosition.Right, 0).Right);
        Assert.Equal(work.Left, OverlayLayout.ForSide(work, 0.30, OverlayPosition.Left, 0).Left);
    }

    [Fact]
    public void ForSide_ClampsFractionToSaneBounds() {
        Rectangle work = new(0, 0, 1000, 800);

        Assert.True(OverlayLayout.ForSide(work, 5.0, OverlayPosition.Right, 0).Width <= work.Width);
        Assert.True(OverlayLayout.ForSide(work, 0.0, OverlayPosition.Right, 0).Width >= (int)(work.Width * 0.1) - 1);
    }
}
