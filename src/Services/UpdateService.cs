using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace MCEControl.Services {
    public class UpdateService {
        private static readonly Lazy<UpdateService> _lazy = new Lazy<UpdateService>(() => new UpdateService());
        public static UpdateService Instance => _lazy.Value;

        // FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LogService)).Location).FileVersion;
        public event EventHandler<Version> GotLatestVersion;
        protected void OnGotLatestVersion(Version v) => GotLatestVersion?.Invoke(this, v);

        public String ErrorMessage { get; private set; }
        public static Version CurrentVersion {
            get { return new Version(System.Windows.Forms.Application.ProductVersion); }
        }
        public Version LatestStableVersion { get; private set; }

        public System.Uri DownloadUri { get; set; }

        public async Task GetLatestStableVersionAsync() {
            DownloadUri = new Uri("https://github.com/tig/winprint/releases");
            using (var client = new WebClient()) {
                try {
                    var github = new GitHubClient(new Octokit.ProductHeaderValue("tig-mcec"));
                    var release = await github.Repository.Release.GetLatest("tig", "mcec").ConfigureAwait(false);
#if DEBUG
                    Logger.Instance.Log4.Debug($"The latest release is tagged at {release.TagName} and is named {release.Name}. Download Url: {release.Assets[0].BrowserDownloadUrl}");
#endif

                    var v = release.TagName;
                    // Remove leading "v" (v2.0.0.1000.alpha)
                    if (v.StartsWith("v", StringComparison.InvariantCultureIgnoreCase))
                        v = v.Substring(1, v.Length - 1);

                    string[] parts = v.Split('.');

                    // Get 4 elements which excludes any .alpha or .beta
                    string version = string.Join(".", parts, 0, 4);

                    if (version != null) {
                        LatestStableVersion = new Version(version);
                        DownloadUri = new Uri(release.Assets[0].BrowserDownloadUrl);
                    }
                    else
                        ErrorMessage = "Could not parse version data.";

                }
                catch (Exception e) {
                    ErrorMessage = $"({DownloadUri}) {e.Message}";
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
    }

}
