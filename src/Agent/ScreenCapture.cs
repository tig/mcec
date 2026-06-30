// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MCEControl;

/// <summary>
/// Screenshotting primitives for the MCEC 3.0 agent "see the screen" feature. Window capture uses
/// <c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c> (which correctly renders DirectComposition /
/// WinUI 3 / WPF surfaces that a plain screen grab returns black for) and falls back to
/// <see cref="Graphics.CopyFromScreen(int, int, int, int, Size)"/> when the driver refuses
/// <c>PrintWindow</c>. All captures are encoded as PNG and analyzed for blank/black content.
/// </summary>
public static class ScreenCapture {
    /// <summary>A frame whose dominant color covers at least this fraction of sampled pixels is blank.</summary>
    public const double BlankDominantFractionThreshold = 0.99;

    /// <summary>Luminance (0-255) below which the dominant color counts as "dark" (a black frame).</summary>
    private const double DarkLuminanceThreshold = 24.0;

    /// <summary>Upper bound on samples per axis; keeps blank analysis O(1) regardless of image size.</summary>
    private const int MaxSamplesPerAxis = 64;

    /// <summary>
    /// Captures a top-level window by handle. Uses <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, falling
    /// back to an on-screen blit if that fails, and reports which path ran plus blank-frame stats.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <returns>The PNG bytes and capture diagnostics.</returns>
    /// <exception cref="ArgumentException">Thrown when the window has no on-screen area.</exception>
    public static CaptureResult CaptureWindow(IntPtr hwnd) {
        if (!AgentNativeMethods.GetWindowRect(hwnd, out NativeRect rect)) {
            throw new ArgumentException("Could not get window bounds.", nameof(hwnd));
        }

        int width = rect.Width;
        int height = rect.Height;
        if (width <= 0 || height <= 0) {
            throw new ArgumentException("Window has no on-screen area to capture.", nameof(hwnd));
        }

        using Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics gfx = Graphics.FromImage(bmp);

        IntPtr hdc = gfx.GetHdc();
        bool printed = AgentNativeMethods.PrintWindow(hwnd, hdc, AgentNativeMethods.PW_RENDERFULLCONTENT);
        gfx.ReleaseHdc(hdc);

        bool usedFallback = false;
        if (!printed) {
            // Fallback: blit the corresponding screen region into the same bitmap. This grabs whatever
            // is on screen at those coordinates, so it returns black for composited/occluded windows
            // and is reported to the caller as a degraded path.
            gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            usedFallback = true;
        }

        ImageStats stats = AnalyzeBlank(bmp);
        return new CaptureResult(Encode(bmp), width, height, usedFallback, stats);
    }

    /// <summary>
    /// Captures an arbitrary screen region (screen coordinates) and reports blank-frame stats.
    /// </summary>
    /// <param name="x">Left screen coordinate.</param>
    /// <param name="y">Top screen coordinate.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <returns>The PNG bytes and capture diagnostics (<c>UsedFallback</c> is always false for regions).</returns>
    /// <exception cref="ArgumentException">Thrown when the region has no area.</exception>
    public static CaptureResult CaptureRegion(int x, int y, int width, int height) {
        if (width <= 0 || height <= 0) {
            throw new ArgumentException("Region has no area to capture.", nameof(width));
        }

        using Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics gfx = Graphics.FromImage(bmp);
        gfx.CopyFromScreen(x, y, 0, 0, new Size(width, height));

        ImageStats stats = AnalyzeBlank(bmp);
        return new CaptureResult(Encode(bmp), width, height, UsedFallback: false, stats);
    }

    private static byte[] Encode(Bitmap bmp) {
        using MemoryStream ms = new();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Estimates whether a bitmap is blank (a flat fill) by sampling a bounded grid of pixels,
    /// quantizing each to 5 bits per channel (so anti-aliasing noise collapses), and measuring the
    /// share held by the single most common color. A busy app window scores well under the threshold;
    /// a failed black/empty grab scores ~1.0. Sampling is capped so cost is independent of image size.
    /// </summary>
    public static ImageStats AnalyzeBlank(Bitmap bmp) {
        int w = bmp.Width;
        int h = bmp.Height;
        if (w <= 0 || h <= 0) {
            return new ImageStats(IsBlank: true, DominantFraction: 1.0, DominantIsDark: true);
        }

        int stepX = Math.Max(1, w / MaxSamplesPerAxis);
        int stepY = Math.Max(1, h / MaxSamplesPerAxis);

        Dictionary<int, int> counts = [];
        int total = 0;
        int bestKey = 0;
        int bestCount = 0;

        for (int y = 0; y < h; y += stepY) {
            for (int x = 0; x < w; x += stepX) {
                Color c = bmp.GetPixel(x, y);
                int key = ((c.R >> 3) << 10) | ((c.G >> 3) << 5) | (c.B >> 3);
                int n = counts.TryGetValue(key, out int existing) ? existing + 1 : 1;
                counts[key] = n;
                if (n > bestCount) {
                    bestCount = n;
                    bestKey = key;
                }
                total++;
            }
        }

        double dominantFraction = total == 0 ? 1.0 : (double)bestCount / total;

        // Reconstruct the mid-value of the dominant quantized color to judge dark-vs-light.
        int r = (((bestKey >> 10) & 0x1F) << 3) | 0x04;
        int g = (((bestKey >> 5) & 0x1F) << 3) | 0x04;
        int b = ((bestKey & 0x1F) << 3) | 0x04;
        double luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);

        return new ImageStats(
            IsBlank: dominantFraction >= BlankDominantFractionThreshold,
            DominantFraction: dominantFraction,
            DominantIsDark: luminance < DarkLuminanceThreshold);
    }
}
