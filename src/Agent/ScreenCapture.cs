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

    // SECURITY (#158): region width/height come straight from agent-controlled command attributes
    // (capture/record), so without a ceiling a single request (e.g. 40000x40000 = ~6.4 GB of ARGB,
    // then PNG + base64) can OOM the process. Window captures are naturally bounded by window size;
    // these caps bound the region path. They are fixed (not operator settings) on purpose: they are
    // an anti-DoS bound sized to plausible desktop geometry, not a quality knob; a raisable setting
    // would reintroduce the failure mode. Oversized requests FAIL FAST with a clear error (code
    // `region-too-large`) rather than being silently clamped, so agents get a diagnosable result.

    /// <summary>Max region width or height in pixels; comfortably above any single monitor and
    /// typical multi-monitor spans (e.g. four 4K displays side by side are 15360 px wide).</summary>
    public const int MaxRegionDimension = 16384;

    /// <summary>Max region area in pixels (64 MP ≈ 256 MB of ARGB; roughly eight 4K frames, twice
    /// an 8K frame), bounding the bitmap, the PNG encode, and the base64 reply.</summary>
    public const int MaxRegionPixels = 64_000_000;

    /// <summary>
    /// Validates agent-requested region dimensions against the #158 caps. Returns null when the
    /// region is acceptable, else a human-readable detail (stating the limits) suitable for the
    /// <c>region-too-large</c> error envelope. The single validation seam used by
    /// <see cref="CaptureRegionBitmap"/> and the capture/record commands.
    /// </summary>
    /// <param name="width">Requested region width in pixels.</param>
    /// <param name="height">Requested region height in pixels.</param>
    /// <returns>Null when valid; otherwise the rejection detail.</returns>
    internal static string? ValidateRegionSize(int width, int height) {
        if (width <= 0 || height <= 0) {
            return "Region has no area to capture.";
        }
        if (width > MaxRegionDimension || height > MaxRegionDimension) {
            return $"Region {width}x{height} exceeds the capture limit of {MaxRegionDimension} px per side. Request a smaller region.";
        }
        // 64-bit math so a huge product can never wrap around to "valid".
        if ((long)width * height > MaxRegionPixels) {
            return $"Region {width}x{height} ({(long)width * height} px) exceeds the capture limit of {MaxRegionPixels} px total. Request a smaller region.";
        }
        return null;
    }

    /// <summary>
    /// Captures a top-level window by handle. Uses <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, falling
    /// back to an on-screen blit if that fails, and reports which path ran plus blank-frame stats.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <returns>The PNG bytes and capture diagnostics.</returns>
    /// <exception cref="ArgumentException">Thrown when the window has no on-screen area.</exception>
    public static CaptureResult CaptureWindow(IntPtr hwnd) {
        using Bitmap bmp = CaptureWindowBitmap(hwnd, out bool usedFallback);
        ImageStats stats = AnalyzeBlank(bmp);
        return new CaptureResult(Encode(bmp), bmp.Width, bmp.Height, usedFallback, stats);
    }

    /// <summary>
    /// Captures a top-level window by handle into a <see cref="Bitmap"/> the caller owns (and must
    /// dispose). Shared by <see cref="CaptureWindow"/> (still PNG) and the GIF recorder, which needs the
    /// raw bitmap to feed many frames into one animation.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <returns>A new ARGB bitmap of the window; the caller owns and must dispose it.</returns>
    /// <exception cref="ArgumentException">Thrown when the window has no on-screen area.</exception>
    public static Bitmap CaptureWindowBitmap(IntPtr hwnd) => CaptureWindowBitmap(hwnd, out _);

    /// <summary>
    /// Captures a window into an owned <see cref="Bitmap"/>, reporting whether the degraded
    /// <c>CopyFromScreen</c> fallback was used (<c>PrintWindow</c> was refused).
    /// </summary>
    private static Bitmap CaptureWindowBitmap(IntPtr hwnd, out bool usedFallback) {
        if (!AgentNativeMethods.GetWindowRect(hwnd, out NativeRect rect)) {
            throw new ArgumentException("Could not get window bounds.", nameof(hwnd));
        }

        int width = rect.Width;
        int height = rect.Height;
        if (width <= 0 || height <= 0) {
            throw new ArgumentException("Window has no on-screen area to capture.", nameof(hwnd));
        }

        Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        try {
            using Graphics gfx = Graphics.FromImage(bmp);

            IntPtr hdc = gfx.GetHdc();
            bool printed = AgentNativeMethods.PrintWindow(hwnd, hdc, AgentNativeMethods.PW_RENDERFULLCONTENT);
            gfx.ReleaseHdc(hdc);

            usedFallback = false;
            if (!printed) {
                // Fallback: blit the corresponding screen region into the same bitmap. This grabs
                // whatever is on screen at those coordinates, so it returns black for composited/occluded
                // windows and is reported to the caller as a degraded path.
                gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                usedFallback = true;
            }
        }
        catch {
            bmp.Dispose();
            throw;
        }
        return bmp;
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
        using Bitmap bmp = CaptureRegionBitmap(x, y, width, height);
        ImageStats stats = AnalyzeBlank(bmp);
        return new CaptureResult(Encode(bmp), width, height, UsedFallback: false, stats);
    }

    /// <summary>
    /// Captures an arbitrary screen region (screen coordinates) into a <see cref="Bitmap"/> the caller
    /// owns (and must dispose). Shared by <see cref="CaptureRegion"/> and the GIF recorder.
    /// </summary>
    /// <param name="x">Left screen coordinate.</param>
    /// <param name="y">Top screen coordinate.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <returns>A new ARGB bitmap of the region; the caller owns and must dispose it.</returns>
    /// <exception cref="ArgumentException">Thrown when the region has no area or exceeds
    /// <see cref="MaxRegionDimension"/>/<see cref="MaxRegionPixels"/> (#158); thrown BEFORE any
    /// bitmap is allocated, so an oversized agent request costs nothing.</exception>
    public static Bitmap CaptureRegionBitmap(int x, int y, int width, int height) {
        string? sizeError = ValidateRegionSize(width, height);
        if (sizeError is not null) {
            throw new ArgumentException(sizeError, nameof(width));
        }

        Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        try {
            using Graphics gfx = Graphics.FromImage(bmp);
            gfx.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }
        catch {
            bmp.Dispose();
            throw;
        }
        return bmp;
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
