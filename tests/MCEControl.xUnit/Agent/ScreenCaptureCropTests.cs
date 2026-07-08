// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

public class ScreenCaptureCropTests {
    [Fact]
    public void CropBitmap_ValidSubrect_ReturnsClone() {
        using Bitmap source = new(100, 80);
        using Bitmap cropped = ScreenCapture.CropBitmap(source, 10, 20, 30, 40);
        Assert.Equal(30, cropped.Width);
        Assert.Equal(40, cropped.Height);
    }

    [Fact]
    public void CropBitmap_OutOfBounds_Throws() {
        using Bitmap source = new(50, 50);
        Assert.Throws<ArgumentException>(() => ScreenCapture.CropBitmap(source, 40, 40, 20, 20));
    }
}