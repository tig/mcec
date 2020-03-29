//-------------------------------------------------------------------
// Copyright � 2018 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows.Forms;
using MCEControl.Properties;

namespace MCEControl {
    /// <summary>
    /// About box
    /// </summary>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1501", Justification = "WinForms generated", Scope = "namespace")]
    partial class About : Form {
        private Label _labelTitle;
        private Button _buttonOk;
        private LinkLabel _linkLabelMceController;
        private LinkLabel _linkLabelKindelSystems;
        private Label _labelSummary;
        private PictureBox _iconMcec;
        private Label _labelVersion;
        private Label _label1;

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
            //_linkLabelKindelSystems.Font = Font;
            //_linkLabelMceController.Font = System.Drawing.SystemFonts.;

            _labelVersion.Text = $"{Resources.MCE_Controller_Version_label} {Application.ProductVersion}";
        }

        private void ButtonOkClick(object sender, EventArgs e) => Close();

        private void LinkLabelMceControllerLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            TelemetryService.Instance.TrackEvent("About Box License Link Clicked");
            Process.Start(_linkLabelMceController.Tag.ToString());
        }

        private void LinkLabelCharlieLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            TelemetryService.Instance.TrackEvent("About Box Kindel Systems Page Link Clicked");
            Process.Start(_linkLabelKindelSystems.Tag.ToString());
        }
    }
}
