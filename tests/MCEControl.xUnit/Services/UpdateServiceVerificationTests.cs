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

    private static readonly (string Name, string Url)[] Assets = [
        ("SHA256SUMS", "https://github.com/tig/mcec/releases/download/v3.0/SHA256SUMS"),
        ("mcec.Setup.exe", "https://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe"),
        ("source.zip", "https://github.com/tig/mcec/releases/download/v3.0/source.zip"),
    ];

    [Fact]
    public void SelectInstallerAssetUrl_PicksPinnedName_NotFirstAsset() {
        string? url = UpdateService.SelectInstallerAssetUrl(Assets, "mcec.Setup.exe");
        Assert.Equal("https://github.com/tig/mcec/releases/download/v3.0/mcec.Setup.exe", url);
    }

    [Fact]
    public void SelectInstallerAssetUrl_IsCaseInsensitiveOnName() {
        string? url = UpdateService.SelectInstallerAssetUrl(Assets, "MCEC.SETUP.EXE");
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
    public void IsTrustedDownloadUri_RejectsUntrusted(string uri) {
        Assert.False(UpdateService.IsTrustedDownloadUri(new Uri(uri)));
    }

    [Fact]
    public void IsTrustedDownloadUri_RejectsNull() {
        Assert.False(UpdateService.IsTrustedDownloadUri(null));
    }

    // ---- (3) Authenticode verification (negative paths — no signed fixture available) ----

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
