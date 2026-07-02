// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Windows.Forms;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Helpers;

/// <summary>
/// Pins the status-bar text fitter: a status longer than the label's Spring-allocated width must
/// come back ellipsized and within the width, so a long informational version string can never
/// push the Client/Server/Serial indicators out of the status strip.
/// </summary>
public class EllipsizedTextTests {
    private static readonly Font TestFont = new("Segoe UI", 9f);

    private static int Measure(string s) => TextRenderer.MeasureText(s, TestFont).Width;

    [Fact]
    public void ShortText_ReturnedUnchanged() {
        const string text = "Version: 3.0.11";
        int width = Measure(text) + 20;

        Assert.Equal(text, EllipsizedText.Fit(text, width, TestFont));
    }

    [Fact]
    public void ExactFit_ReturnedUnchanged() {
        const string text = "Version: 3.0.11";
        int width = Measure(text);

        Assert.Equal(text, EllipsizedText.Fit(text, width, TestFont));
    }

    [Fact]
    public void LongText_IsEllipsizedAndFits() {
        // The real-world case: a full informational version with branch + sha.
        const string text = "Version: 3.0.12-claude-issue-123-super-long-branch-name.47+Branch.claude-issue-123.Sha.0123456789abcdef0123456789abcdef01234567";
        int width = Measure("Version: 3.0.12-clau"); // deliberately much narrower than the text

        string fitted = EllipsizedText.Fit(text, width, TestFont);

        Assert.EndsWith(EllipsizedText.Ellipsis, fitted);
        Assert.True(Measure(fitted) <= width, $"fitted '{fitted}' measures {Measure(fitted)}px > {width}px");
        Assert.True(fitted.Length < text.Length);
        Assert.StartsWith(fitted[..^1], text); // prefix of the original, plus the ellipsis
    }

    [Fact]
    public void LongText_KeepsTheLongestPrefixThatFits() {
        const string text = "Version: 3.0.12-develop.5+Branch.develop.Sha.abcdef";
        int width = Measure(text) - 10; // just barely too narrow

        string fitted = EllipsizedText.Fit(text, width, TestFont);

        Assert.EndsWith(EllipsizedText.Ellipsis, fitted);
        Assert.True(Measure(fitted) <= width);
        // One more character must NOT fit; otherwise the search stopped early.
        string oneMore = text[..(fitted.Length)] + EllipsizedText.Ellipsis;
        Assert.True(Measure(oneMore) > width, "a longer prefix would still have fit");
    }

    [Fact]
    public void TinyWidth_DegradesToEllipsisThenEmpty() {
        const string text = "Version: 3.0.11";

        string atEllipsisWidth = EllipsizedText.Fit(text, Measure(EllipsizedText.Ellipsis), TestFont);
        Assert.Equal(EllipsizedText.Ellipsis, atEllipsisWidth);

        Assert.Equal(string.Empty, EllipsizedText.Fit(text, 1, TestFont));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveWidth_ReturnsEmpty(int width) {
        Assert.Equal(string.Empty, EllipsizedText.Fit("anything", width, TestFont));
    }

    [Fact]
    public void EmptyText_ReturnsEmpty() {
        Assert.Equal(string.Empty, EllipsizedText.Fit("", 100, TestFont));
    }
}
