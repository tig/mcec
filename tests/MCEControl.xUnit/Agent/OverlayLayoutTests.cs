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

    [Fact]
    public void MaxLines_ScalesWithHeight_SoATallerScreenFillsMoreOfIt() {
        // #119 fix: the feed cap must grow with the overlay height, otherwise a fixed small cap
        // leaves a tall screen only partly filled (lines stacked from the bottom, empty at the top).
        int shortScreen = OverlayLayout.MaxLines(500);
        int tallScreen = OverlayLayout.MaxLines(1440);

        Assert.True(tallScreen > shortScreen, "a taller overlay must hold more lines");
        // A 1440px-tall overlay should hold roughly one line per ~28px, i.e. dozens, not 8.
        Assert.True(tallScreen >= 40, $"expected the full height to hold many lines, got {tallScreen}");
    }

    [Fact]
    public void MaxLines_HasASaneFloor_ForTinyOrDegenerateHeights() {
        Assert.True(OverlayLayout.MaxLines(10) >= 8);
        Assert.True(OverlayLayout.MaxLines(0) >= 8);
        Assert.True(OverlayLayout.MaxLines(-100) >= 8);
    }

    [Fact]
    public void MaxLines_ApproximatesHeightOverLineStride() {
        // The cap tracks how many command boxes fit; allow generous slack (the on-screen geometry
        // break is the true limiter, so the cap only needs to be in the right ballpark).
        int lines = OverlayLayout.MaxLines(1080);

        Assert.InRange(lines, 30, 60);
    }

    [Fact]
    public void ControlBannerText_IsTheSingleControllingLine() {
        // #266: one short, plain-language line telling the human MCEC is driving; no hotkey clutter.
        Assert.Equal("MCEC is controlling your PC", OverlayLayout.ControlBannerText);
    }

    [Fact]
    public void ControlBannerText_IsASingleLine() {
        // "One line": the centered banner must never wrap to a second row.
        Assert.DoesNotContain("\n", OverlayLayout.ControlBannerText);
        Assert.DoesNotContain("\r", OverlayLayout.ControlBannerText);
    }

    [Fact]
    public void FeedColumnWidth_IsAboutThirtyPercentOfTheFullWidthWindow() {
        // #266: the window is full-width (so the banner centers across the screen), but the feed stays
        // in a ~30% docked column.
        Assert.Equal(300, OverlayLayout.FeedColumnWidth(1000));
        Assert.Equal(576, OverlayLayout.FeedColumnWidth(1920));
    }

    [Fact]
    public void FeedColumnWidth_HasASaneFloor_ForTinyOrDegenerateWidths() {
        Assert.True(OverlayLayout.FeedColumnWidth(0) >= 1);
        Assert.True(OverlayLayout.FeedColumnWidth(-100) >= 1);
    }
}
