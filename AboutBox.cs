//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
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
    /// Summary description for Form1.
    /// </summary>
    public class AboutBox : Form {
        private Label _labelTitle;
        private Label _labelCopyright;
        private Button _buttonOk;
        private LinkLabel _linkLabelMceController;
        private LinkLabel _linkLabelKindelSystems;
        private Label _labelLicense;
        private Label _labelMoreInfo;
        private LinkLabel _linkLabelSourceCode;
        private Label _labelSummary;
        private PictureBox _pictureBoxDonate;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        public AboutBox() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            _labelTitle.Text = Resources.MCE_Controller_Version_label + Application.ProductVersion;
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._labelTitle = new System.Windows.Forms.Label();
            this._labelCopyright = new System.Windows.Forms.Label();
            this._linkLabelMceController = new System.Windows.Forms.LinkLabel();
            this._buttonOk = new System.Windows.Forms.Button();
            this._linkLabelKindelSystems = new System.Windows.Forms.LinkLabel();
            this._labelLicense = new System.Windows.Forms.Label();
            this._labelMoreInfo = new System.Windows.Forms.Label();
            this._linkLabelSourceCode = new System.Windows.Forms.LinkLabel();
            this._labelSummary = new System.Windows.Forms.Label();
            this._pictureBoxDonate = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize) (this._pictureBoxDonate)).BeginInit();
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this._labelTitle.Font = new System.Drawing.Font("Lucida Sans", 12F, System.Drawing.FontStyle.Bold,
                                                            System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this._labelTitle.Location = new System.Drawing.Point(12, 9);
            this._labelTitle.Name = "_labelTitle";
            this._labelTitle.Size = new System.Drawing.Size(399, 21);
            this._labelTitle.TabIndex = 0;
            this._labelTitle.Text = "MCE Controller";
            // 
            // labelCopyright
            // 
            this._labelCopyright.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labelCopyright.Location = new System.Drawing.Point(12, 137);
            this._labelCopyright.Name = "_labelCopyright";
            this._labelCopyright.Size = new System.Drawing.Size(53, 16);
            this._labelCopyright.TabIndex = 2;
            this._labelCopyright.Text = "© 2012";
            // 
            // linkLabelMCEController
            // 
            this._linkLabelMceController.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelMceController.Location = new System.Drawing.Point(12, 222);
            this._linkLabelMceController.Name = "_linkLabelMceController";
            this._linkLabelMceController.Size = new System.Drawing.Size(208, 16);
            this._linkLabelMceController.TabIndex = 7;
            this._linkLabelMceController.TabStop = true;
            this._linkLabelMceController.Tag = "http://tig.github.com/mcecontroller/";
            this._linkLabelMceController.Text = "http://tig.github.com/mcecontroller/";
            this._linkLabelMceController.LinkClicked +=
                new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelMceControllerLinkClicked);
            // 
            // buttonOK
            // 
            this._buttonOk.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._buttonOk.Location = new System.Drawing.Point(336, 219);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(75, 23);
            this._buttonOk.TabIndex = 8;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // linkLabelKindelSystems
            // 
            this._linkLabelKindelSystems.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelKindelSystems.Location = new System.Drawing.Point(53, 137);
            this._linkLabelKindelSystems.Name = "_linkLabelKindelSystems";
            this._linkLabelKindelSystems.Size = new System.Drawing.Size(115, 16);
            this._linkLabelKindelSystems.TabIndex = 3;
            this._linkLabelKindelSystems.TabStop = true;
            this._linkLabelKindelSystems.Tag = "http://www.kindel.com";
            this._linkLabelKindelSystems.Text = "Kindel Systems, LLC.";
            this._linkLabelKindelSystems.LinkClicked +=
                new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelCharlieLinkClicked);
            // 
            // labelLicense
            // 
            this._labelLicense.Location = new System.Drawing.Point(12, 164);
            this._labelLicense.Name = "_labelLicense";
            this._labelLicense.Size = new System.Drawing.Size(387, 16);
            this._labelLicense.TabIndex = 4;
            this._labelLicense.Text = "MCE Controller is licensed under the MIT License.  Source code available at";
            // 
            // labelMoreInfo
            // 
            this._labelMoreInfo.Location = new System.Drawing.Point(12, 206);
            this._labelMoreInfo.Name = "_labelMoreInfo";
            this._labelMoreInfo.Size = new System.Drawing.Size(184, 16);
            this._labelMoreInfo.TabIndex = 6;
            this._labelMoreInfo.Text = "For more information:";
            // 
            // linkLabelSourceCode
            // 
            this._linkLabelSourceCode.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelSourceCode.Location = new System.Drawing.Point(12, 180);
            this._linkLabelSourceCode.Name = "_linkLabelSourceCode";
            this._linkLabelSourceCode.Size = new System.Drawing.Size(208, 16);
            this._linkLabelSourceCode.TabIndex = 5;
            this._linkLabelSourceCode.TabStop = true;
            this._linkLabelSourceCode.Tag = "https://github.com/tig/mcecontroller";
            this._linkLabelSourceCode.Text = "github.com/tig/mcecontroller";
            this._linkLabelSourceCode.LinkClicked +=
                new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1LinkClicked);
            // 
            // labelSummary
            // 
            this._labelSummary.Location = new System.Drawing.Point(12, 45);
            this._labelSummary.Name = "_labelSummary";
            this._labelSummary.Size = new System.Drawing.Size(399, 29);
            this._labelSummary.TabIndex = 1;
            this._labelSummary.Text =
                "MCE Controller is distributed as freeware. Donations of any value appreciated. Ma" +
                "ke donations by clicking on the button below.";
            // 
            // pictureBoxDonate
            // 
            this._pictureBoxDonate.ImageLocation = "http://images.sourceforge.net/images/project-support.jpg";
            this._pictureBoxDonate.Location = new System.Drawing.Point(149, 88);
            this._pictureBoxDonate.Name = "_pictureBoxDonate";
            this._pictureBoxDonate.Size = new System.Drawing.Size(93, 35);
            this._pictureBoxDonate.TabIndex = 8;
            this._pictureBoxDonate.TabStop = false;
            this._pictureBoxDonate.Click += new System.EventHandler(this.PictureBoxDonateClick);
            // 
            // AboutBox
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(423, 254);
            this.ControlBox = false;
            this.Controls.Add(this._pictureBoxDonate);
            this.Controls.Add(this._labelSummary);
            this.Controls.Add(this._linkLabelSourceCode);
            this.Controls.Add(this._labelMoreInfo);
            this.Controls.Add(this._labelLicense);
            this.Controls.Add(this._linkLabelKindelSystems);
            this.Controls.Add(this._buttonOk);
            this.Controls.Add(this._linkLabelMceController);
            this.Controls.Add(this._labelTitle);
            this.Controls.Add(this._labelCopyright);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutBox";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About MCE Controller";
            ((System.ComponentModel.ISupportInitialize) (this._pictureBoxDonate)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private void ButtonOkClick(object sender, EventArgs e) {
            Close();
        }

        private void LinkLabelMceControllerLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(_linkLabelMceController.Tag.ToString());
        }

        private void LinkLabelCharlieLinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(_linkLabelKindelSystems.Tag.ToString());
        }

        private void LinkLabel1LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(_linkLabelSourceCode.Tag.ToString());
        }

        private void PictureBoxDonateClick(object sender, EventArgs e) {
            Process.Start("http://sourceforge.net/donate/index.php?group_id=138158");
        }
    }
}
