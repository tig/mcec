// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Region size caps (#158 security hardening): agent-controlled region width/height must be bounded
/// before any Bitmap allocation, or a single capture/record request (e.g. 40000x40000 = ~6.4 GB of
/// ARGB, plus PNG + base64) can OOM the process. These tests exercise the validation seam
/// (<see cref="ScreenCapture.ValidateRegionSize"/>) and the throwing guard in
/// <see cref="ScreenCapture.CaptureRegionBitmap"/> without touching the real desktop.
/// </summary>
public class ScreenCaptureRegionLimitTests {
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)] // a full 4K monitor
    public void ValidateRegionSize_NormalRegions_Accepted(int width, int height) {
        Assert.Null(ScreenCapture.ValidateRegionSize(width, height));
    }

    [Fact]
    public void ValidateRegionSize_MaxDimensionBoundary_Accepted() {
        // Exactly at the per-side cap (with a tiny other side so the pixel cap is not hit).
        Assert.Null(ScreenCapture.ValidateRegionSize(ScreenCapture.MaxRegionDimension, 1));
        Assert.Null(ScreenCapture.ValidateRegionSize(1, ScreenCapture.MaxRegionDimension));
    }

    [Fact]
    public void ValidateRegionSize_MaxPixelsBoundary_Accepted() {
        // Exactly at the total-pixel cap: 8000 * 8000 == MaxRegionPixels.
        Assert.Equal(64_000_000, ScreenCapture.MaxRegionPixels);
        Assert.Null(ScreenCapture.ValidateRegionSize(8000, 8000));
    }

    [Fact]
    public void ValidateRegionSize_WidthOverMaxDimension_Rejected_DetailStatesLimit() {
        string? error = ScreenCapture.ValidateRegionSize(ScreenCapture.MaxRegionDimension + 1, 1);

        Assert.NotNull(error);
        Assert.Contains(ScreenCapture.MaxRegionDimension.ToString(), error);
    }

    [Fact]
    public void ValidateRegionSize_HeightOverMaxDimension_Rejected_DetailStatesLimit() {
        string? error = ScreenCapture.ValidateRegionSize(1, ScreenCapture.MaxRegionDimension + 1);

        Assert.NotNull(error);
        Assert.Contains(ScreenCapture.MaxRegionDimension.ToString(), error);
    }

    [Fact]
    public void ValidateRegionSize_ProductOverMaxPixels_Rejected_DetailStatesLimit() {
        // Each side is under the per-side cap, but the product (72 MP) exceeds the pixel cap.
        string? error = ScreenCapture.ValidateRegionSize(9000, 8000);

        Assert.NotNull(error);
        Assert.Contains(ScreenCapture.MaxRegionPixels.ToString(), error);
    }

    [Fact]
    public void ValidateRegionSize_ProductJustOverMaxPixels_Rejected() {
        // 8001 * 8000 = 64_008_000; one row over the cap.
        Assert.NotNull(ScreenCapture.ValidateRegionSize(8001, 8000));
    }

    [Fact]
    public void ValidateRegionSize_MaxDimensionSquared_RejectedByPixelCap() {
        // Both sides at the per-side cap: 16384^2 = 268 MP >> 64 MP. The product check must
        // use 64-bit math so a large product can never wrap around to "valid".
        Assert.NotNull(ScreenCapture.ValidateRegionSize(
            ScreenCapture.MaxRegionDimension, ScreenCapture.MaxRegionDimension));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -5)]
    public void ValidateRegionSize_ZeroOrNegative_Rejected(int width, int height) {
        Assert.NotNull(ScreenCapture.ValidateRegionSize(width, height));
    }

    [Fact]
    public void ValidateRegionSize_IntMaxValues_RejectedWithoutOverflow() {
        Assert.NotNull(ScreenCapture.ValidateRegionSize(int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void CaptureRegionBitmap_OversizedRegion_ThrowsBeforeAllocating() {
        // The issue's attack: 40000x40000 asks GDI+ for ~6.4 GB. The guard must throw the
        // validation message BEFORE any Bitmap is constructed; the exception message matching
        // the validation seam's output proves the fast-fail path ran (no allocation, no screen IO).
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => ScreenCapture.CaptureRegionBitmap(0, 0, 40000, 40000));

        Assert.Equal(ScreenCapture.ValidateRegionSize(40000, 40000), ex.Message.Split(" (Parameter")[0]);
        Assert.Contains(ScreenCapture.MaxRegionDimension.ToString(), ex.Message);
    }

    [Fact]
    public void CaptureRegionBitmap_ZeroArea_StillThrows() {
        // The pre-existing lower-bound behavior must survive the new upper-bound guard.
        Assert.Throws<ArgumentException>(() => ScreenCapture.CaptureRegionBitmap(0, 0, 0, 100));
        Assert.Throws<ArgumentException>(() => ScreenCapture.CaptureRegionBitmap(0, 0, 100, -1));
    }
}
