using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace MCEControl
{
    class LatestVersion
    {
        public delegate void GotVersionInfo(object sender, string version);
        public LatestVersion() {
        }

        public static Version CurrentVersion
        {
            get { return new Version(Application.ProductVersion); }
            set { }
        }


        public String ErrorMessage { get; set; }
        public String LatestStableRelease { get; set; }

        public async void GetLatestStableVersion(GotVersionInfo callback) {

            var client = new WebClient();

            try {
                byte[] bytes =
                    await
                    client.DownloadDataTaskAsync(
                        "http://mcec.codeplex.com/wikipage?title=Latest%20Stable%20Version%20Number");

                Stream s = new MemoryStream(bytes);

                var htmlDoc = new HtmlDocument();
                htmlDoc.Load(s);

                var div = htmlDoc.DocumentNode.SelectSingleNode("//*/div[@class='wikidoc']");
                if (div != null)
                    LatestStableRelease = div.InnerText.Trim();
                else {
                    ErrorMessage = "Could not parse version data.";
                }
            }
            catch (Exception e) {
                ErrorMessage = e.Message;
                Debug.Write(e.Message);
            }
            callback(this, LatestStableRelease);
        }

        /// <summary>
        /// > 0 - Newer version available
        /// = 0 - Same version
        /// < 0 - Current version is newer.
        /// </summary>
        /// <returns></returns>
        public int CompareVersions() {
            var latest = new Version(LatestStableRelease);

            return CurrentVersion.CompareTo(latest);
        }

    }
}
