// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Octokit;
using Application = System.Windows.Forms.Application;
using Timer = System.Windows.Forms.Timer;

namespace MCEControl;

public class UpdateService {
    private static readonly Lazy<UpdateService> _lazy = new(() => new UpdateService());

    private string _tempFilename = null!;

    // SECURITY (#146): the exact release asset the updater will download and run. Selected by name so a
    // rogue/extra asset that merely sorts first can't be delivered as `Assets[0]`.
    internal const string InstallerFileName = "mcec.Setup.exe";

    // The publisher name the installer's Authenticode signer certificate subject must contain. The
    // release is signed via Azure Trusted Signing under the Kindel LLC publisher identity (see
    // docs/code-signing.md); a substring keeps this stable across certificate rotations.
    private const string ExpectedPublisher = "Kindel";

    public UpdateService() {
        LatestStableVersion = new Version(0, 0);

        // Every ~24 hours check for a new version again
        Timer t = new Timer();
        t.Interval = 24 * 60 * 60 * 1000;
        t.Tick += (sender, args) => OnCheckForUpdates();
        t.Start();
    }

    public static UpdateService Instance => _lazy.Value;

    public string ErrorMessage { get; private set; } = null!;

    public static Version CurrentVersion {
        get {
            // Application.ProductVersion is the assembly's informational version. On non-tagged
            // (dev/CI) builds GitVersion appends a SemVer pre-release/build suffix
            // (e.g. "2.4.1-develop.3+Branch.develop.Sha.abc"); released builds are clean ("2.4.0").
            // Parse only the leading numeric "Major.Minor.Patch" so Version never throws.
            string core = Application.ProductVersion.Split('-', '+')[0];
            return Version.TryParse(core, out Version? v) ? v : new Version(0, 0, 0);
        }
    }

    public Version LatestStableVersion { get; private set; }

    public Uri ReleasePageUri { get; set; } = null!;
    public Uri DownloadUri { get; private set; } = null!;

    // FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LogService)).Location).FileVersion;
    public event EventHandler<Version> GotLatestVersion = null!;
    protected void OnGotLatestVersion(Version v) => GotLatestVersion?.Invoke(this, v);

    public event EventHandler CheckForUpdates = null!;
    protected void OnCheckForUpdates() => CheckForUpdates?.Invoke(this, null!);

    public void CheckVersion() {
        Task.Run(() => {
            GetLatestStableVersionAsync().ConfigureAwait(false);
        });
        ;
    }

    private async Task GetLatestStableVersionAsync() {
        ReleasePageUri = new Uri("https://github.com/tig/mcec/releases");
        ErrorMessage = null!;
        Logger.Instance.Log4.Debug("Checking for new release...");
        try {
            GitHubClient github = new GitHubClient(new ProductHeaderValue("tig-mcec"));
            IReadOnlyList<Release> allReleases = await github.Repository.Release.GetAll("tig", "mcec").ConfigureAwait(false);

            // Ignore bogus historical test releases (the v9 "Fake" pre-release used for testing update mechanism).
            allReleases = [.. allReleases.Where(r =>
                !r.TagName.Equals("v9.1.2.3", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("Fake", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("Testing Update", StringComparison.OrdinalIgnoreCase))];

            Release[] releases =
                [.. allReleases.Where(r => !r.Prerelease).OrderByDescending(r => new Version(r.TagName.TrimStart('v')))];
            if (releases.Length > 0) {
                Release latest = releases[0];
                LatestStableVersion = new Version(latest.TagName.TrimStart('v'));
                ReleasePageUri = new Uri(latest.HtmlUrl);

                // SECURITY (#146): pick the pinned installer asset by name — never a blind Assets[0],
                // which could be an attacker-added asset that sorts first (and throws on an empty list).
                string? assetUrl = SelectInstallerAssetUrl(
                    latest.Assets.Select(a => (a.Name, a.BrowserDownloadUrl)), InstallerFileName);
                if (assetUrl is null) {
                    ErrorMessage = $"Release {latest.TagName} has no '{InstallerFileName}' asset";
                    Logger.Instance.Log4.Warn($"{GetType().Name}: {ErrorMessage}");
                    DownloadUri = null!;
                }
                else {
                    Logger.Instance.Log4.Debug(
                        $"The latest release is tagged at {latest.TagName} and is named '{latest.Name}'. Download Url: {assetUrl}");
                    DownloadUri = new Uri(assetUrl);
                }
            }
            else {
                ErrorMessage = "No release found";
            }
        }
        catch (Exception e) {
            ErrorMessage = $"({ReleasePageUri}) {e.Message}";
            Logger.Instance.Log4.Info(ErrorMessage);
            TelemetryService.Instance.TrackException(e);
        }

        OnGotLatestVersion(LatestStableVersion);
    }

    // > 0 - Current version is newer
    // = 0 - Same version
    // < 0 - A newer version available
    public int CompareVersions() {
        return CurrentVersion.CompareTo(LatestStableVersion);
    }

    /// <summary>
    /// Returns the download URL of the asset whose name exactly matches <paramref name="installerFileName"/>
    /// (case-insensitive), or null if no such asset exists. Pins the asset by name instead of trusting
    /// position (#146).
    /// </summary>
    internal static string? SelectInstallerAssetUrl(IEnumerable<(string Name, string Url)> assets, string installerFileName) {
        foreach ((string name, string url) in assets) {
            if (string.Equals(name, installerFileName, StringComparison.OrdinalIgnoreCase)) {
                return url;
            }
        }
        return null;
    }

    /// <summary>
    /// True only for an absolute <c>https</c> URL whose host is <c>github.com</c> or a
    /// <c>*.githubusercontent.com</c> asset host. Pins scheme + host so the updater never downloads over
    /// plain HTTP or from an attacker-influenced host (#146).
    /// </summary>
    internal static bool IsTrustedDownloadUri(Uri? uri) {
        if (uri is null || !uri.IsAbsoluteUri) {
            return false;
        }
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        // Explicit host allowlist (not a "*.githubusercontent.com" suffix): the release page lives on
        // github.com and release-asset downloads redirect to the object hosts below. This deliberately
        // excludes raw.githubusercontent.com and any other subdomain that serves arbitrary user content.
        foreach (string trusted in TrustedDownloadHosts) {
            if (string.Equals(uri.Host, trusted, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    private static readonly string[] TrustedDownloadHosts = [
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    internal async void StartUpgrade() {
        if (DownloadUri is null) {
            Logger.Instance.Log4.Error($"{GetType().Name}: no installer download URL available — aborting upgrade.");
            return;
        }
        // SECURITY (#146): only ever download over https from a GitHub host.
        if (!IsTrustedDownloadUri(DownloadUri)) {
            Logger.Instance.Log4.Error(
                $"{GetType().Name}: refusing to download from untrusted URL '{DownloadUri.AbsoluteUri}' " +
                "(must be https from github.com / githubusercontent.com).");
            return;
        }

        // Download into a fresh, app-private directory (not a predictable %TEMP%\tmpXXXX.tmp.exe that a
        // local attacker could pre-create or swap). Verify, then launch; clean up on any failure.
        string dir = Path.Combine(Path.GetTempPath(), "mcec-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempFilename = Path.Combine(dir, InstallerFileName);
        Logger.Instance.Log4.Info($"{GetType().Name}: Downloading {DownloadUri.AbsoluteUri} to {_tempFilename}...");

        try {
            using (HttpClient httpClient = new HttpClient()) {
                HttpResponseMessage response = await httpClient.GetAsync(DownloadUri);
                response.EnsureSuccessStatusCode();
                byte[] data = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(_tempFilename, data);
            }
            Logger.Instance.Log4.Info($"{GetType().Name}: Download complete");

            // SECURITY (#146): verify the file carries a valid Authenticode signature from the expected
            // publisher BEFORE launching it. A compromised/wrong asset or any tampering fails here.
            if (!AuthenticodeVerifier.Verify(_tempFilename, ExpectedPublisher, out string reason)) {
                Logger.Instance.Log4.Error(
                    $"{GetType().Name}: signature verification FAILED ({reason}); refusing to run {_tempFilename}.");
                TryDeleteDirectory(dir);
                return;
            }

            Logger.Instance.Log4.Info($"{GetType().Name}: signature verified; exiting and running installer ({_tempFilename})...");
            Process p = new Process { StartInfo = { FileName = _tempFilename, UseShellExecute = true } };
            try {
                p.Start();
            }
            catch (Win32Exception we) {
                Logger.Instance.Log4.Error($"{GetType().Name}: {_tempFilename} failed to run with error: {we.Message}");
                TryDeleteDirectory(dir);
                return;
            }

            MainWindow.Instance.BeginInvoke((Action)(() => { MainWindow.Instance.ShutDown(); }));
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Download failed: {ex.Message}");
            TryDeleteDirectory(dir);
        }
    }

    private static void TryDeleteDirectory(string dir) {
        try {
            if (Directory.Exists(dir)) {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"UpdateService: could not clean up '{dir}': {e.Message}");
        }
    }
}
