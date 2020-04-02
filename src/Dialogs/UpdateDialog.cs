// Copyright © Kindel Systems, LLC - http://www.kindel.com - charlie@kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace MCEControl.Dialogs {
    public partial class UpdateDialog : Form {

        private static readonly Lazy<UpdateDialog> _lazy = new Lazy<UpdateDialog>(() => new UpdateDialog());
        public static UpdateDialog Instance { get { return _lazy.Value; } }

        public UpdateDialog() {
            InitializeComponent();

            StartPosition = FormStartPosition.CenterParent;

            this.labelNewVersion.Text = $"A newer version of MCE Controller ({UpdateService.Instance.LatestStableVersion}) is available.";
            this.linkReleasePage.Links[0].LinkData = UpdateService.Instance.ReleasePageUri.AbsoluteUri;
        }

        internal void UpdateService_CheckForUpdates(object sender, EventArgs e) {
            UpdateService.Instance.CheckVersion();
        }

        private void downloadButton_Click(object sender, EventArgs args) {
            UpdateService.Instance.StartUpgrade();
        }

        private void linkReleasePage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start((string)linkReleasePage.Links[0].LinkData);
        }

        private void UpdateDialog_VisibleChanged(object sender, EventArgs e) {
            if (Visible)
                UpdateService.Instance.CheckForUpdates -= UpdateService_CheckForUpdates;
            else
                UpdateService.Instance.CheckForUpdates += UpdateService_CheckForUpdates;
        }
    }
}
