// Copyright © Kindel Systems, LLC - http://www.kindel.com - charlie@kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Octokit;

namespace MCEControl {
    public class UpdateService {
        private static readonly Lazy<UpdateService> _lazy = new Lazy<UpdateService>(() => new UpdateService());

        public UpdateService() {
            LatestStableVersion = new Version(0, 0);

            // Every ~24 hours check for a new version again
            Timer t = new Timer();
            t.Interval = 24 * 60 * 60 * 1000;
            t.Tick += (sender, args) => OnCheckForUpdates();
            t.Start();
        }

        public static UpdateService Instance => _lazy.Value;

        // FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LogService)).Location).FileVersion;
        public event EventHandler<Version> GotLatestVersion;
        protected void OnGotLatestVersion(Version v) => GotLatestVersion?.Invoke(this, v);

        public event EventHandler CheckForUpdates;
        protected void OnCheckForUpdates() => CheckForUpdates?.Invoke(this, null);

        public string ErrorMessage { get; private set; }
        public static Version CurrentVersion {
            get { return new Version(System.Windows.Forms.Application.ProductVersion); }
        }
        public Version LatestStableVersion { get; private set; }

        public System.Uri ReleasePageUri { get; set; }
        public System.Uri DownloadUri { get; private set; }

        private string _tempFilename;

        public void CheckVersion() {
            Task.Run(() => {
                GetLatestStableVersionAsync().ConfigureAwait(false);
            }); ;
        }

        private async Task GetLatestStableVersionAsync() {
            ReleasePageUri = new Uri("https://github.com/tig/mcec/releases");
            Logger.Instance.Log4.Debug("Checking for new release...");
            using (var client = new WebClient()) {
                try {
                    var github = new GitHubClient(new Octokit.ProductHeaderValue("tig-mcec"));
                    var allReleases = await github.Repository.Release.GetAll("tig", "mcec").ConfigureAwait(false);
                    //Logger.Instance.Log4.Debug($"allReleases {JsonSerializer.Serialize(allReleases, options: new JsonSerializerOptions() { WriteIndented = true })}");

                    // Get all releases and pre-releases
#if DEBUG
                    var releases = allReleases.Where(r => r.Prerelease).OrderByDescending(r => new Version(r.TagName.Trim('v'))).ToArray();
#else
                    var releases = allReleases.Where(r => !r.Prerelease).OrderByDescending(r => new Version(r.TagName.Trim('v'))).ToArray();
#endif
                    //Logger.Instance.Log4.Debug($"Releases {JsonSerializer.Serialize(releases, options: new JsonSerializerOptions() { WriteIndented = true })}");
                    if (releases.Length > 0) {
                        ///Logger.Instance.Log4.Info("The latest release is tagged at {releases[0].TagName} and is named {releases[0].Name}. Download Url: {releases[0].Assets[0].BrowserDownloadUrl}");

                        LatestStableVersion = new Version(releases[0].TagName.Replace('v', ' '));
                        ReleasePageUri = new Uri(releases[0].HtmlUrl);
                        DownloadUri = new Uri(releases[0].Assets[0].BrowserDownloadUrl);
                    }
                    else {
                        ErrorMessage = "No release found.";
                    }
                }
                catch (Exception e) {
                    ErrorMessage = $"({ReleasePageUri}) {e.Message}";
                    Logger.Instance.Log4.Debug(ErrorMessage);
                    TelemetryService.Instance.TrackException(e);
                }
            }
            OnGotLatestVersion(LatestStableVersion);
        }

        // > 0 - Current version is newer
        // = 0 - Same version
        // < 0 - A newer version available
        public int CompareVersions() {
            return CurrentVersion.CompareTo(LatestStableVersion);
        }

        internal void StartUpgrade() {
            // Download file
            _tempFilename = Path.GetTempFileName() + ".exe";
            Logger.Instance.Log4.Info($"{this.GetType().Name}: Downloading {DownloadUri.AbsoluteUri} to {_tempFilename}...");

            Task.Run(() => {
                var client = new WebClient();
                client.DownloadDataCompleted += Client_DownloadDataCompleted;
                client.DownloadProgressChanged += Client_DownloadProgressChanged;
                client.DownloadDataAsync(DownloadUri);
            }); ;

        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e) {
            if (e.ProgressPercentage % 33 == 0) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Download progress...");
            }
        }

        private void Client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e) {
            try {
                // If the request was not canceled and did not throw
                // an exception, display the resource.
                if (!e.Cancelled && e.Error == null) {
                    File.WriteAllBytes(_tempFilename, (byte[])e.Result);
                }
            }
            finally {

            }
            Logger.Instance.Log4.Info($"{this.GetType().Name}: Download complete");
            Logger.Instance.Log4.Info($"{this.GetType().Name}: Exiting and running installer ({_tempFilename})...");
            var p = new Process {
                StartInfo = {
                        FileName = _tempFilename,
                        UseShellExecute = true
                    },
            };
            try {
                p.Start();
                //p.WaitForInputIdle(1000);
            }
            catch (Win32Exception we) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: {_tempFilename} failed to run with error: {we.Message}");
            }
            MainWindow.Instance.BeginInvoke((Action)(() => { MainWindow.Instance.ShutDown(); }));
        }
    }
}
