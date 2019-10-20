using System;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace MCEControl
{
    class LatestVersion
    {
        public delegate void GotVersionInfo(object sender, Version version);

        public static Version CurrentVersion{
            get { return new Version(Application.ProductVersion); }
        }
        public String ErrorMessage { get; private set; }
        public Version LatestStableRelease { get; private set; }

        public string Url { get; set; }

        public async void GetLatestStableVersionAsync(GotVersionInfo callback) {
            using (var client = new WebClient()) {
                try {
                    string contents =
                        await client.DownloadStringTaskAsync(Url).ConfigureAwait(true);

                    string[] parts = contents.Split('.');

                    string version = string.Join(".", parts);

                    if (version != null)
                        LatestStableRelease = new Version(version);
                    else
                        ErrorMessage = "Could not parse version data.";
                }
                catch (Exception e) {
                    ErrorMessage = $"({Url}) {e.Message}";
                }
            }
            callback(this, LatestStableRelease);
        }

        // > 0 - Newer version available
        // = 0 - Same version
        // < 0 - Current version is newer
        public int CompareVersions() {
            return CurrentVersion.CompareTo(LatestStableRelease);
        }
    }
}
