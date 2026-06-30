//-------------------------------------------------------------------
// Copyright © 2018 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows.Forms;
using MCEControl.Properties;

namespace MCEControl; 
/// <summary>
/// About box
/// </summary>

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1501", Justification = "WinForms generated", Scope = "namespace")]
partial class About : Form {
    private Label _labelTitle = null!;
    private Button _buttonOk = null!;
    private LinkLabel _linkLabelMceController = null!;
    private LinkLabel _linkLabelKindel = null!;
    private Label _labelSummary = null!;
    private PictureBox _iconMcec = null!;
    private Label _labelVersion;
    private Label _label1 = null!;

    /// <summary>
    /// Required designer variable.
    /// </summary>
    public About() {
        //
        // Required for Windows Form Designer support
        //
        InitializeComponent();
        // https://www.sgrottel.de/?p=1581&lang=en
        //Font = System.Drawing.SystemFonts.DialogFont;
        //_label1.Font =
        //_labelSummary.Font = _labelTitle.Font = Font;
        //_linkLabelKindel.Font = Font;
        //_linkLabelMceController.Font = System.Drawing.SystemFonts.;

        _labelVersion!.Text = $"Model Context Environment Controller\r\n{Resources.MCE_Controller_Version_label} {Application.ProductVersion}";

        UpdateService.Instance.CheckVersion();
    }

    private void ButtonOkClick(object sender, EventArgs e) => Close();

    private void LinkLabelMceControllerLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
        TelemetryService.Instance.TrackEvent("About Box License Link Clicked");
        Process.Start(_linkLabelMceController.Tag!.ToString()!);
    }

    private void LinkLabelCharlieLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
        TelemetryService.Instance.TrackEvent("About Box Kindel Page Link Clicked");
        Process.Start(_linkLabelKindel.Tag!.ToString()!);
    }
}
