using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

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

        public async void GetLatestStableVersionAsync(GotVersionInfo callback) {
            var client = new WebClient();
            try {
                byte[] bytes =
                    await client.DownloadDataTaskAsync(
                        "http://mcec.codeplex.com/wikipage?title=Latest%20Stable%20Version%20Number");
                var htmlDoc = new HtmlDocument();
                htmlDoc.Load(new MemoryStream(bytes));
                var div = htmlDoc.DocumentNode.SelectSingleNode("//*/div[@class='wikidoc']");
                if (div != null)
                    LatestStableRelease = new Version(div.InnerText.Trim());
                else 
                    ErrorMessage = "Could not parse version data.";
            }
            catch (Exception e) {
                ErrorMessage = e.Message;
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
