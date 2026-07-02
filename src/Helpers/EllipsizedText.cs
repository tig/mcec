// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Fits a string into a pixel width by trimming it to the longest prefix that fits with a
/// trailing ellipsis. Used by the main window's status bar: the status label holds the full
/// informational version (e.g. a long prerelease "x.y.z-branch.n+sha" string), and
/// <c>ToolStripStatusLabel</c> has no <c>AutoEllipsis</c>, so without this a long status pushes
/// the Client/Server/Serial indicators out of the strip. Pure (no control state); unit-tested
/// directly.
/// </summary>
internal static class EllipsizedText {
    public const string Ellipsis = "…";

    /// <summary>
    /// Returns <paramref name="text"/> unchanged when it renders within <paramref name="maxWidth"/>
    /// pixels in <paramref name="font"/>; otherwise the longest prefix that fits with a trailing
    /// ellipsis. A width too small for even the ellipsis yields an empty string; a non-positive
    /// width yields an empty string.
    /// </summary>
    public static string Fit(string text, int maxWidth, Font font) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }
        if (maxWidth <= 0) {
            return string.Empty;
        }
        if (Measure(text, font) <= maxWidth) {
            return text;
        }

        // Binary-search the longest prefix whose "prefix…" rendering fits.
        int lo = 0, hi = text.Length - 1;
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (Measure(text[..mid] + Ellipsis, font) <= maxWidth) {
                lo = mid;
            }
            else {
                hi = mid - 1;
            }
        }

        if (lo == 0) {
            return Measure(Ellipsis, font) <= maxWidth ? Ellipsis : string.Empty;
        }
        return text[..lo] + Ellipsis;
    }

    private static int Measure(string s, Font font) => TextRenderer.MeasureText(s, font).Width;
}
