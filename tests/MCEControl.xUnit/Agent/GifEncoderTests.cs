// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Unit tests for the dependency-free animated-GIF assembler. These run anywhere GDI+ is available
/// (no desktop capture needed): they synthesize bitmaps, encode them, and decode the result back with
/// System.Drawing to prove the assembled stream is a valid multi-frame GIF89a.
/// </summary>
public class GifEncoderTests {
    private static Bitmap SolidBitmap(int w, int h, Color color) {
        Bitmap bmp = new(w, h, PixelFormat.Format24bppRgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    [Fact]
    public void Assemble_TwoFrames_ProducesValidAnimatedGif89a() {
        using Bitmap a = SolidBitmap(32, 24, Color.Red);
        using Bitmap b = SolidBitmap(32, 24, Color.Blue);
        List<byte[]> frames = [GifEncoder.EncodeFrame(a), GifEncoder.EncodeFrame(b)];

        byte[] gif = GifEncoder.Assemble(frames, delayMs: 200, loop: true);

        // GIF89a magic.
        Assert.True(gif.Length > 6);
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));

        // Decodes back to a 2-frame animation at the declared dimensions.
        using MemoryStream ms = new(gif);
        using Image img = Image.FromStream(ms);
        FrameDimension time = new(img.FrameDimensionsList[0]);
        Assert.Equal(2, img.GetFrameCount(time));
        Assert.Equal(32, img.Width);
        Assert.Equal(24, img.Height);
    }

    [Fact]
    public void Assemble_DimensionsAreTheLargestFrame() {
        using Bitmap small = SolidBitmap(10, 10, Color.Green);
        using Bitmap big = SolidBitmap(40, 20, Color.Yellow);
        List<byte[]> frames = [GifEncoder.EncodeFrame(small), GifEncoder.EncodeFrame(big)];

        byte[] gif = GifEncoder.Assemble(frames, delayMs: 100, loop: false);

        using MemoryStream ms = new(gif);
        using Image img = Image.FromStream(ms);
        Assert.Equal(40, img.Width);
        Assert.Equal(20, img.Height);
    }

    [Fact]
    public void Assemble_NoFrames_Throws() {
        Assert.Throws<ArgumentException>(() => GifEncoder.Assemble([], delayMs: 100, loop: true));
    }
}
