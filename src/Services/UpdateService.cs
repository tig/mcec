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
        get { return new Version(Application.ProductVersion); }
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

#if DEBUG
            var releases =
allReleases.Where(r => r.Prerelease).OrderByDescending(r => new Version(r.TagName.Trim('v'))).ToArray();
#else
            Release[] releases = allReleases.Where(r => !r.Prerelease)
                .OrderByDescending(r => new Version(r.TagName.Trim('v'))).ToArray();
#endif
            if (releases.Length > 0) {
#if DEBUG
                Logger.Instance.Log4.Info($"The latest PRE-RELEASE is tagged at {releases[0].TagName} and is named '{releases[0].Name}' Download Url: {releases[0].Assets[0].BrowserDownloadUrl}");
#else
                Logger.Instance.Log4.Debug(
                    $"The latest release is tagged at {releases[0].TagName} and is named '{releases[0].Name}'. Download Url: {releases[0].Assets[0].BrowserDownloadUrl}");
#endif

                LatestStableVersion = new Version(releases[0].TagName.Replace('v', ' '));
                ReleasePageUri = new Uri(releases[0].HtmlUrl);
                DownloadUri = new Uri(releases[0].Assets[0].BrowserDownloadUrl);
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

    internal async void StartUpgrade() {
        _tempFilename = Path.GetTempFileName() + ".exe";
        Logger.Instance.Log4.Info($"{GetType().Name}: Downloading {DownloadUri.AbsoluteUri} to {_tempFilename}...");

        try {
            using (HttpClient httpClient = new HttpClient()) {
                HttpResponseMessage response = await httpClient.GetAsync(DownloadUri);
                response.EnsureSuccessStatusCode();
                byte[] data = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(_tempFilename, data);
            }

            Logger.Instance.Log4.Info($"{GetType().Name}: Download complete");
            Logger.Instance.Log4.Info($"{GetType().Name}: Exiting and running installer ({_tempFilename})...");
            Process p = new Process { StartInfo = { FileName = _tempFilename, UseShellExecute = true } };
            try {
                p.Start();
            }
            catch (Win32Exception we) {
                Logger.Instance.Log4.Error($"{GetType().Name}: {_tempFilename} failed to run with error: {we.Message}");
            }

            MainWindow.Instance.BeginInvoke((Action)(() => { MainWindow.Instance.ShutDown(); }));
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Download failed: {ex.Message}");
        }
    }
}
