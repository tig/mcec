// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Blank-frame detection (#90 observation hardening): a failed capture is a flat fill and must be
/// flagged, while a real (busy) window must not trip the detector.
/// </summary>
public class ScreenCaptureTests {
    private static Bitmap SolidBitmap(Color color, int w = 200, int h = 150) {
        Bitmap bmp = new(w, h);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    [Fact]
    public void AnalyzeBlank_AllBlack_IsBlankAndDark() {
        using Bitmap bmp = SolidBitmap(Color.Black);

        ImageStats stats = ScreenCapture.AnalyzeBlank(bmp);

        Assert.True(stats.IsBlank);
        Assert.True(stats.DominantIsDark);
        Assert.Equal(1.0, stats.DominantFraction, 3);
    }

    [Fact]
    public void AnalyzeBlank_AllWhite_IsBlankButNotDark() {
        using Bitmap bmp = SolidBitmap(Color.White);

        ImageStats stats = ScreenCapture.AnalyzeBlank(bmp);

        // A legitimately empty white surface is still "blank" but must not be reported as a black frame.
        Assert.True(stats.IsBlank);
        Assert.False(stats.DominantIsDark);
    }

    [Fact]
    public void AnalyzeBlank_BusyImage_IsNotBlank() {
        using Bitmap bmp = new(200, 150);
        // Paint a high-variety gradient/checker so no single quantized color dominates.
        for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
                bmp.SetPixel(x, y, Color.FromArgb((x * 7) % 256, (y * 11) % 256, ((x + y) * 13) % 256));
            }
        }

        ImageStats stats = ScreenCapture.AnalyzeBlank(bmp);

        Assert.False(stats.IsBlank);
        Assert.True(stats.DominantFraction < ScreenCapture.BlankDominantFractionThreshold);
    }

    [Fact]
    public void AnalyzeBlank_MostlyUniformWithSpeck_StillBlank() {
        using Bitmap bmp = SolidBitmap(Color.Black);
        // A couple of stray bright pixels (a cursor/artifact) must not rescue an otherwise-black frame.
        bmp.SetPixel(0, 0, Color.Red);
        bmp.SetPixel(199, 149, Color.Lime);

        ImageStats stats = ScreenCapture.AnalyzeBlank(bmp);

        Assert.True(stats.IsBlank);
    }
}
