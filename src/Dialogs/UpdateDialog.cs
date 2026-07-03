// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace MCEControl.Dialogs; 
public partial class UpdateDialog : Form {

    private static readonly Lazy<UpdateDialog> _lazy = new(() => new UpdateDialog());
    public static UpdateDialog Instance { get { return _lazy.Value; } }

    public UpdateDialog() {
        InitializeComponent();

        StartPosition = FormStartPosition.CenterParent;

        this.labelNewVersion.Text = $"A newer version of MCEC ({UpdateService.Instance.LatestStableVersion}) is available.";
        this.linkReleasePage.Links[0].LinkData = UpdateService.Instance.ReleasePageUri.AbsoluteUri;
    }

    private void UpdateService_CheckForUpdates(object? sender, EventArgs e) {
        UpdateService.Instance.CheckVersion();
    }

    private void downloadButton_Click(object sender, EventArgs args) {
        // Fire-and-forget from the click handler: StartUpgrade logs all failures and its task
        // never faults (#214).
        _ = UpdateService.Instance.StartUpgrade();
    }

    private void linkReleasePage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
        Program.LaunchExternal((string)linkReleasePage.Links[0].LinkData!);
    }

    private void UpdateDialog_VisibleChanged(object sender, EventArgs e) {
        if (Visible)
            UpdateService.Instance.CheckForUpdates -= UpdateService_CheckForUpdates;
        else
            UpdateService.Instance.CheckForUpdates += UpdateService_CheckForUpdates;
    }
}
