// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 overlay geometry: the right ~30% of the working area, inset by a margin.</summary>
public class OverlayLayoutTests {
    [Fact]
    public void RightFraction_IsRightAligned_AndAboutThirtyPercentWide() {
        Rectangle work = new(0, 0, 1000, 800);

        Rectangle r = OverlayLayout.RightFraction(work, 0.30, margin: 12);

        // 30% of 1000 = 300, inset 12 each side => 276 wide, right edge at 1000-12.
        Assert.Equal(276, r.Width);
        Assert.Equal(988, r.Right);
        Assert.Equal(12, r.Top);
        Assert.Equal(788, r.Bottom);
    }

    [Fact]
    public void RightFraction_RespectsWorkingAreaOrigin() {
        Rectangle work = new(100, 50, 1000, 800); // non-zero origin (taskbar/secondary monitor)

        Rectangle r = OverlayLayout.RightFraction(work, 0.30, margin: 0);

        Assert.Equal(work.Right, r.Right);
        Assert.Equal(300, r.Width);
        Assert.Equal(50, r.Top);
    }

    [Fact]
    public void RightFraction_ClampsFractionToSaneBounds() {
        Rectangle work = new(0, 0, 1000, 800);

        Assert.True(OverlayLayout.RightFraction(work, 5.0, 0).Width <= work.Width);
        Assert.True(OverlayLayout.RightFraction(work, 0.0, 0).Width >= (int)(work.Width * 0.1) - 1);
    }
}
