using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Octokit;

namespace MCEControl {
    public class UpdateService {
        private static readonly Lazy<UpdateService> _lazy = new Lazy<UpdateService>(() => new UpdateService());

        public UpdateService() {
            GotLatestVersion += GotLatestVersionHandler;
        }

        public static UpdateService Instance => _lazy.Value;

        // FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LogService)).Location).FileVersion;
        public event EventHandler<Version> GotLatestVersion;
        protected void OnGotLatestVersion(Version v) => GotLatestVersion?.Invoke(this, v);

        public String ErrorMessage { get; private set; }
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
            ReleasePageUri = new Uri("https://github.com/tig/winprint/releases");
            using (var client = new WebClient()) {
                try {
                    var github = new GitHubClient(new Octokit.ProductHeaderValue("tig-mcec"));
                    var release = await github.Repository.Release.GetLatest("tig", "mcec").ConfigureAwait(false);
#if DEBUG
                    Logger.Instance.Log4.Debug($"The latest release is tagged at {release.TagName} and is named {release.Name}. Download Url: {release.Assets[0].BrowserDownloadUrl}");
#endif

                    var v = release.TagName;
                    // Remove leading "v" (v2.0.0.1000.alpha)
                    if (v.StartsWith("v", StringComparison.InvariantCultureIgnoreCase)) {
                        v = v.Substring(1, v.Length - 1);
                    }

                    var parts = v.Split('.');

                    // Get 4 elements which excludes any .alpha or .beta
                    var version = string.Join(".", parts, 0, 4);

                    if (version != null) {
                        LatestStableVersion = new Version(version);
                        ReleasePageUri = new Uri($@"https://github.com/tig/mcec/releases/tag/v{v}");
                        DownloadUri = new Uri(release.Assets[0].BrowserDownloadUrl);
                    }
                    else {
                        ErrorMessage = "Could not parse version data.";
                    }
                }
                catch (Exception e) {
                    ErrorMessage = $"({ReleasePageUri}) {e.Message}";
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


        private void GotLatestVersionHandler(object sender, Version version) {
            if (version == null && !String.IsNullOrWhiteSpace(UpdateService.Instance.ErrorMessage)) {
                Logger.Instance.Log4.Info(
                    $"Could not access tig.github.io/mcec to see if a newer version is available. {UpdateService.Instance.ErrorMessage}");
            }
            else if (CompareVersions() < 0) {
                Logger.Instance.Log4.Info("------------------------------------------------");

                Logger.Instance.Log4.Info($"A newer version of MCE Controller ({version}) is available at");
                Logger.Instance.Log4.Info($"   {UpdateService.Instance.ReleasePageUri}");
                Logger.Instance.Log4.Info($"   Use the \"Help.Install Latest Version\" menu to upgrade");
                Logger.Instance.Log4.Info("------------------------------------------------");
            }
            else if (CompareVersions() > 0) {
                Logger.Instance.Log4.Info(
                    $"You are are running a MORE recent version than can be found at tig.github.io/mcec ({version})");
            }
            else {
                Logger.Instance.Log4.Info("You are running the most recent version of MCE Controller");
            }
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
            Process.Start(ReleasePageUri.AbsoluteUri);
            MainWindow.Instance.BeginInvoke((Action)(() => { MainWindow.Instance.ShutDown(); }));
        }
    }
}
