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
        private Button _buttonOk;
        private LinkLabel _linkLabelMceController;
        private LinkLabel _linkLabelKindelSystems;
        private Label _labelSummary;
        private PictureBox _pictureBox1;
        private LinkLabel _linkLabelGuillen;
        private Label _label1;
        private LinkLabel _linkLabelHomePage;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        public AboutBox() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            _labelTitle.Text = Resources.MCE_Controller_Version_label + LatestVersion.CurrentVersion;
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutBox));
            this._labelTitle = new System.Windows.Forms.Label();
            this._linkLabelMceController = new System.Windows.Forms.LinkLabel();
            this._buttonOk = new System.Windows.Forms.Button();
            this._linkLabelKindelSystems = new System.Windows.Forms.LinkLabel();
            this._labelSummary = new System.Windows.Forms.Label();
            this._linkLabelHomePage = new System.Windows.Forms.LinkLabel();
            this._pictureBox1 = new System.Windows.Forms.PictureBox();
            this._linkLabelGuillen = new System.Windows.Forms.LinkLabel();
            this._label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // _labelTitle
            // 
            this._labelTitle.Font = new System.Drawing.Font("Lucida Sans", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._labelTitle.Location = new System.Drawing.Point(130, 12);
            this._labelTitle.Name = "_labelTitle";
            this._labelTitle.Size = new System.Drawing.Size(321, 21);
            this._labelTitle.TabIndex = 0;
            this._labelTitle.Text = "MCE Controller";
            // 
            // _linkLabelMceController
            // 
            this._linkLabelMceController.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelMceController.Location = new System.Drawing.Point(130, 76);
            this._linkLabelMceController.Name = "_linkLabelMceController";
            this._linkLabelMceController.Size = new System.Drawing.Size(321, 16);
            this._linkLabelMceController.TabIndex = 3;
            this._linkLabelMceController.TabStop = true;
            this._linkLabelMceController.Tag = "http://mcec.codeplex.com/license";
            this._linkLabelMceController.Text = "License Agreement";
            this._linkLabelMceController.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelMceControllerLinkClicked);
            // 
            // _buttonOk
            // 
            this._buttonOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonOk.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._buttonOk.Location = new System.Drawing.Point(376, 118);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(75, 23);
            this._buttonOk.TabIndex = 0;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // _linkLabelKindelSystems
            // 
            this._linkLabelKindelSystems.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelKindelSystems.Location = new System.Drawing.Point(130, 33);
            this._linkLabelKindelSystems.Name = "_linkLabelKindelSystems";
            this._linkLabelKindelSystems.Size = new System.Drawing.Size(321, 16);
            this._linkLabelKindelSystems.TabIndex = 1;
            this._linkLabelKindelSystems.TabStop = true;
            this._linkLabelKindelSystems.Tag = "http://www.kindel.com";
            this._linkLabelKindelSystems.Text = "© 2013 Kindel Systems, LLC.";
            this._linkLabelKindelSystems.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelCharlieLinkClicked);
            // 
            // _labelSummary
            // 
            this._labelSummary.Location = new System.Drawing.Point(130, 49);
            this._labelSummary.Name = "_labelSummary";
            this._labelSummary.Size = new System.Drawing.Size(274, 27);
            this._labelSummary.TabIndex = 2;
            this._labelSummary.Text = "MCE Controller is distributed as freeware and published as open source under the " +
    "MIT License.";
            // 
            // _linkLabelHomePage
            // 
            this._linkLabelHomePage.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelHomePage.Location = new System.Drawing.Point(130, 92);
            this._linkLabelHomePage.Name = "_linkLabelHomePage";
            this._linkLabelHomePage.Size = new System.Drawing.Size(321, 16);
            this._linkLabelHomePage.TabIndex = 4;
            this._linkLabelHomePage.TabStop = true;
            this._linkLabelHomePage.Tag = "http://mcec.codeplex.com";
            this._linkLabelHomePage.Text = "MCE Controller website";
            this._linkLabelHomePage.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelHomePage_LinkClicked);
            // 
            // _pictureBox1
            // 
            this._pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("_pictureBox1.Image")));
            this._pictureBox1.InitialImage = null;
            this._pictureBox1.Location = new System.Drawing.Point(12, 12);
            this._pictureBox1.Name = "_pictureBox1";
            this._pictureBox1.Size = new System.Drawing.Size(96, 96);
            this._pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._pictureBox1.TabIndex = 5;
            this._pictureBox1.TabStop = false;
            // 
            // _linkLabelGuillen
            // 
            this._linkLabelGuillen.AutoSize = true;
            this._linkLabelGuillen.Location = new System.Drawing.Point(24, 128);
            this._linkLabelGuillen.Name = "_linkLabelGuillen";
            this._linkLabelGuillen.Size = new System.Drawing.Size(72, 13);
            this._linkLabelGuillen.TabIndex = 6;
            this._linkLabelGuillen.TabStop = true;
            this._linkLabelGuillen.Text = "GuillenDesign";
            this._linkLabelGuillen.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelGuillen_LinkClicked);
            // 
            // _label1
            // 
            this._label1.AutoSize = true;
            this._label1.Location = new System.Drawing.Point(16, 111);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(88, 13);
            this._label1.TabIndex = 7;
            this._label1.Text = "Icon designed by";
            // 
            // AboutBox
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this._buttonOk;
            this.ClientSize = new System.Drawing.Size(463, 153);
            this.ControlBox = false;
            this.Controls.Add(this._label1);
            this.Controls.Add(this._linkLabelGuillen);
            this.Controls.Add(this._pictureBox1);
            this.Controls.Add(this._linkLabelHomePage);
            this.Controls.Add(this._labelSummary);
            this.Controls.Add(this._linkLabelKindelSystems);
            this.Controls.Add(this._buttonOk);
            this.Controls.Add(this._linkLabelMceController);
            this.Controls.Add(this._labelTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutBox";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Tag = "http://guillendesign.deviantart.com/";
            this.Text = "About";
            ((System.ComponentModel.ISupportInitialize)(this._pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

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

        private void linkLabelHomePage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(_linkLabelHomePage.Tag.ToString());
        }

        private void linkLabelGuillen_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(_linkLabelGuillen.Tag.ToString());
        }
    }
}
