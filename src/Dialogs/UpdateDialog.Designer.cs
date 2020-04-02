namespace MCEControl.Dialogs {
    partial class UpdateDialog {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.labelNewVersion = new System.Windows.Forms.Label();
            this.downloadButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.linkReleasePage = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // labelNewVersion
            // 
            this.labelNewVersion.AutoSize = true;
            this.labelNewVersion.Location = new System.Drawing.Point(12, 19);
            this.labelNewVersion.Name = "labelNewVersion";
            this.labelNewVersion.Size = new System.Drawing.Size(83, 13);
            this.labelNewVersion.TabIndex = 2;
            this.labelNewVersion.Text = "A new version...";
            // 
            // downloadButton
            // 
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.downloadButton.Location = new System.Drawing.Point(120, 68);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(189, 26);
            this.downloadButton.TabIndex = 0;
            this.downloadButton.Text = "&Download && install new version...";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(315, 68);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(64, 26);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "&Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // linkReleasePage
            // 
            this.linkReleasePage.AutoSize = true;
            this.linkReleasePage.Location = new System.Drawing.Point(12, 43);
            this.linkReleasePage.Name = "linkReleasePage";
            this.linkReleasePage.Size = new System.Drawing.Size(74, 13);
            this.linkReleasePage.TabIndex = 3;
            this.linkReleasePage.TabStop = true;
            this.linkReleasePage.Text = "Release Page";
            this.linkReleasePage.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkReleasePage_LinkClicked);
            // 
            // UpdateDialog
            // 
            this.AcceptButton = this.downloadButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(391, 106);
            this.Controls.Add(this.linkReleasePage);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.labelNewVersion);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "UpdateDialog";
            this.Text = "Update MCE Controller";
            this.VisibleChanged += new System.EventHandler(this.UpdateDialog_VisibleChanged);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelNewVersion;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.LinkLabel linkReleasePage;
    }
}

