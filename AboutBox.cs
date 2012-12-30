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
        private Label _labelLicense;
        private Label _labelSummary;
        private PictureBox pictureBox1;
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutBox));
            this._labelTitle = new System.Windows.Forms.Label();
            this._linkLabelMceController = new System.Windows.Forms.LinkLabel();
            this._buttonOk = new System.Windows.Forms.Button();
            this._linkLabelKindelSystems = new System.Windows.Forms.LinkLabel();
            this._labelLicense = new System.Windows.Forms.Label();
            this._labelSummary = new System.Windows.Forms.Label();
            this._pictureBoxDonate = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this._pictureBoxDonate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // _labelTitle
            // 
            this._labelTitle.Font = new System.Drawing.Font("Lucida Sans", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._labelTitle.Location = new System.Drawing.Point(123, 13);
            this._labelTitle.Name = "_labelTitle";
            this._labelTitle.Size = new System.Drawing.Size(296, 21);
            this._labelTitle.TabIndex = 0;
            this._labelTitle.Text = "MCE Controller";
            // 
            // _linkLabelMceController
            // 
            this._linkLabelMceController.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelMceController.Location = new System.Drawing.Point(123, 195);
            this._linkLabelMceController.Name = "_linkLabelMceController";
            this._linkLabelMceController.Size = new System.Drawing.Size(208, 16);
            this._linkLabelMceController.TabIndex = 7;
            this._linkLabelMceController.TabStop = true;
            this._linkLabelMceController.Tag = "http://tig.github.com/mcecontroller/";
            this._linkLabelMceController.Text = "MCE Controller home page";
            this._linkLabelMceController.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelMceControllerLinkClicked);
            // 
            // _buttonOk
            // 
            this._buttonOk.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._buttonOk.Location = new System.Drawing.Point(357, 216);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(75, 23);
            this._buttonOk.TabIndex = 8;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // _linkLabelKindelSystems
            // 
            this._linkLabelKindelSystems.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._linkLabelKindelSystems.Location = new System.Drawing.Point(123, 43);
            this._linkLabelKindelSystems.Name = "_linkLabelKindelSystems";
            this._linkLabelKindelSystems.Size = new System.Drawing.Size(253, 16);
            this._linkLabelKindelSystems.TabIndex = 3;
            this._linkLabelKindelSystems.TabStop = true;
            this._linkLabelKindelSystems.Tag = "http://www.kindel.com";
            this._linkLabelKindelSystems.Text = "© 2013 Kindel Systems, LLC.";
            this._linkLabelKindelSystems.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelCharlieLinkClicked);
            // 
            // _labelLicense
            // 
            this._labelLicense.Location = new System.Drawing.Point(123, 165);
            this._labelLicense.Name = "_labelLicense";
            this._labelLicense.Size = new System.Drawing.Size(287, 30);
            this._labelLicense.TabIndex = 4;
            this._labelLicense.Text = "MCE Controller is open source under the MIT License.  ";
            // 
            // _labelSummary
            // 
            this._labelSummary.Location = new System.Drawing.Point(123, 70);
            this._labelSummary.Name = "_labelSummary";
            this._labelSummary.Size = new System.Drawing.Size(293, 50);
            this._labelSummary.TabIndex = 1;
            this._labelSummary.Text = "MCE Controller is distributed as freeware. Donations of any value appreciated. Ma" +
    "ke donations by clicking on the button below.";
            // 
            // _pictureBoxDonate
            // 
            this._pictureBoxDonate.Image = ((System.Drawing.Image)(resources.GetObject("_pictureBoxDonate.Image")));
            this._pictureBoxDonate.ImageLocation = "";
            this._pictureBoxDonate.Location = new System.Drawing.Point(217, 112);
            this._pictureBoxDonate.Name = "_pictureBoxDonate";
            this._pictureBoxDonate.Size = new System.Drawing.Size(90, 34);
            this._pictureBoxDonate.TabIndex = 8;
            this._pictureBoxDonate.TabStop = false;
            this._pictureBoxDonate.Click += new System.EventHandler(this.PictureBoxDonateClick);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(12, 13);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(96, 96);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 9;
            this.pictureBox1.TabStop = false;
            // 
            // AboutBox
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(444, 251);
            this.ControlBox = false;
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this._pictureBoxDonate);
            this.Controls.Add(this._labelSummary);
            this.Controls.Add(this._labelLicense);
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
            this.Text = "About";
            ((System.ComponentModel.ISupportInitialize)(this._pictureBoxDonate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
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

        private void PictureBoxDonateClick(object sender, EventArgs e) {
            Process.Start("http://sourceforge.net/donate/index.php?group_id=138158");
        }
    }
}
