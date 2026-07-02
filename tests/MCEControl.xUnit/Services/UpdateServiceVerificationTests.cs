// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Security regression tests for issue #146: the auto-updater must (1) pick the pinned installer asset
/// rather than a blind <c>Assets[0]</c>, (2) only download from https + a GitHub host, and (3) verify the
/// downloaded file's Authenticode signature/publisher before launching it.
/// </summary>
public class UpdateServiceVerificationTests {
    // ---- (1) asset pinning ----

    private static readonly (string Name, string Url)[] _assets = [
        ("SHA256SUMS", "https://github.com/tig/mcec/releases/download/v3.0/SHA256SUMS"),
        ("mcec.Setup.exe", "https://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe"),
        ("source.zip", "https://github.com/tig/mcec/releases/download/v3.0/source.zip"),
    ];

    [Fact]
    public void SelectInstallerAssetUrl_PicksPinnedName_NotFirstAsset() {
        string? url = UpdateService.SelectInstallerAssetUrl(_assets, "mcec.Setup.exe");
        Assert.Equal("https://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe", url);
    }

    [Fact]
    public void SelectInstallerAssetUrl_IsCaseInsensitiveOnName() {
        string? url = UpdateService.SelectInstallerAssetUrl(_assets, "MCEC.SETUP.EXE");
        Assert.NotNull(url);
    }

    [Fact]
    public void SelectInstallerAssetUrl_ReturnsNull_WhenNoMatchingAsset() {
        var assets = new[] { ("readme.txt", "https://github.com/x/y/releases/download/v1/readme.txt") };
        Assert.Null(UpdateService.SelectInstallerAssetUrl(assets, "mcec.Setup.exe"));
    }

    [Fact]
    public void SelectInstallerAssetUrl_ReturnsNull_WhenNoAssets() {
        Assert.Null(UpdateService.SelectInstallerAssetUrl(Array.Empty<(string, string)>(), "mcec.Setup.exe"));
    }

    // ---- release-tag filtering (#214) ----

    [Theory]
    [InlineData("v3.0.8", "3.0.8")]
    [InlineData("3.0.8", "3.0.8")]
    [InlineData("V2.4", "2.4")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    public void TryParseVersionTag_ParsesValidTags(string tag, string expected) {
        Assert.Equal(Version.Parse(expected), UpdateService.TryParseVersionTag(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("v3.0.8-rc.1")] // System.Version has no prerelease syntax
    [InlineData("latest")]
    public void TryParseVersionTag_ReturnsNull_ForMalformedTags(string? tag) {
        Assert.Null(UpdateService.TryParseVersionTag(tag));
    }

    [Fact]
    public void PickLatestVersionTag_PicksHighestVersion_NotStringOrder() {
        // "v2.9.9" sorts after "v2.10.0" as a string; version order must win.
        string? tag = UpdateService.PickLatestVersionTag(["v2.9.9", "v2.10.0", "v1.0.0"]);
        Assert.Equal("v2.10.0", tag);
    }

    [Fact]
    public void PickLatestVersionTag_IgnoresMalformedTags() {
        // One malformed GitHub tag must not abort the whole check (#214).
        string? tag = UpdateService.PickLatestVersionTag(["garbage", "v3.0.8", "v9.1.2.3-Fake+meta", "v3.0.7"]);
        Assert.Equal("v3.0.8", tag);
    }

    [Fact]
    public void PickLatestVersionTag_ReturnsNull_WhenNothingParses() {
        Assert.Null(UpdateService.PickLatestVersionTag(["garbage", "", null, "latest"]));
    }

    [Fact]
    public void PickLatestVersionTag_ReturnsNull_WhenEmpty() {
        Assert.Null(UpdateService.PickLatestVersionTag([]));
    }

    // ---- (2) download-URL pinning ----

    [Theory]
    [InlineData("https://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1/2")]
    [InlineData("https://release-assets.githubusercontent.com/x/y")]
    public void IsTrustedDownloadUri_AllowsHttpsGitHubHosts(string uri) {
        Assert.True(UpdateService.IsTrustedDownloadUri(new Uri(uri)));
    }

    [Theory]
    [InlineData("http://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe")] // not https
    [InlineData("https://evil.com/mcec.Setup.exe")]                                   // wrong host
    [InlineData("https://github.com.evil.com/x")]                                     // suffix trick
    [InlineData("https://notgithub.com/x")]
    [InlineData("ftp://github.com/x")]
    [InlineData("https://raw.githubusercontent.com/tig/mcec/main/evil.exe")]          // arbitrary user content
    [InlineData("https://github.com@evil.com/x")]                                     // userinfo trick
    public void IsTrustedDownloadUri_RejectsUntrusted(string uri) {
        Assert.False(UpdateService.IsTrustedDownloadUri(new Uri(uri)));
    }

    [Fact]
    public void IsTrustedDownloadUri_RejectsNull() {
        Assert.False(UpdateService.IsTrustedDownloadUri(null));
    }

    // ---- (3) Authenticode verification (negative paths; no signed fixture available) ----

    [Fact]
    public void Verify_ReturnsFalse_ForMissingFile() {
        bool ok = AuthenticodeVerifier.Verify(@"C:\does\not\exist\mcec.Setup.exe", "Kindel", out string reason);
        Assert.False(ok);
        Assert.Contains("file not found", reason);
    }

    [Fact]
    public void Verify_ReturnsFalse_ForUnsignedFile() {
        // The test assembly itself is not Authenticode-signed, so verification must fail closed.
        string unsigned = Assembly.GetExecutingAssembly().Location;
        bool ok = AuthenticodeVerifier.Verify(unsigned, "Kindel", out string reason);
        Assert.False(ok);
        Assert.Contains("signature", reason, StringComparison.OrdinalIgnoreCase);
    }
}
