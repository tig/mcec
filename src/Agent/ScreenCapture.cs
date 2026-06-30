// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MCEControl;

/// <summary>
/// Screenshotting primitives for the MCEC 3.0 agent "see the screen" feature. Window capture uses
/// <c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c> (which correctly renders DirectComposition /
/// WinUI 3 / WPF surfaces that a plain screen grab returns black for) and falls back to
/// <see cref="Graphics.CopyFromScreen(int, int, int, int, Size)"/> when the driver refuses
/// <c>PrintWindow</c>. All captures are encoded as PNG.
/// </summary>
public static class ScreenCapture {
    /// <summary>
    /// Captures a top-level window by handle and returns PNG-encoded bytes. Uses
    /// <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, falling back to an on-screen blit if that fails.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <returns>PNG-encoded image bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when the window has no on-screen area.</exception>
    public static byte[] CaptureWindow(IntPtr hwnd) {
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
        bool ok = AgentNativeMethods.PrintWindow(hwnd, hdc, AgentNativeMethods.PW_RENDERFULLCONTENT);
        gfx.ReleaseHdc(hdc);

        if (!ok) {
            // Fallback: blit the corresponding screen region into the same bitmap.
            gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }

        using MemoryStream ms = new();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Captures an arbitrary screen region (screen coordinates) and returns PNG-encoded bytes.
    /// </summary>
    /// <param name="x">Left screen coordinate.</param>
    /// <param name="y">Top screen coordinate.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <returns>PNG-encoded image bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when the region has no area.</exception>
    public static byte[] CaptureRegion(int x, int y, int width, int height) {
        if (width <= 0 || height <= 0) {
            throw new ArgumentException("Region has no area to capture.", nameof(width));
        }

        using Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics gfx = Graphics.FromImage(bmp);
        gfx.CopyFromScreen(x, y, 0, 0, new Size(width, height));

        using MemoryStream ms = new();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
