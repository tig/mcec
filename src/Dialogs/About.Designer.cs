namespace MCEControl {
    partial class About {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
            this._labelTitle = new System.Windows.Forms.Label();
            this._linkLabelMceController = new System.Windows.Forms.LinkLabel();
            this._buttonOk = new System.Windows.Forms.Button();
            this._linkLabelKindelSystems = new System.Windows.Forms.LinkLabel();
            this._labelSummary = new System.Windows.Forms.Label();
            this._iconMcec = new System.Windows.Forms.PictureBox();
            this._label1 = new System.Windows.Forms.Label();
            this._labelVersion = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._iconMcec)).BeginInit();
            this.SuspendLayout();
            // 
            // _labelTitle
            // 
            this._labelTitle.Font = new System.Drawing.Font("Consolas", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._labelTitle.Location = new System.Drawing.Point(309, 25);
            this._labelTitle.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._labelTitle.Name = "_labelTitle";
            this._labelTitle.Size = new System.Drawing.Size(410, 63);
            this._labelTitle.TabIndex = 0;
            this._labelTitle.Text = "MCE Controller";
            this._labelTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _linkLabelMceController
            // 
            this._linkLabelMceController.LinkColor = System.Drawing.Color.Moccasin;
            this._linkLabelMceController.Location = new System.Drawing.Point(330, 222);
            this._linkLabelMceController.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._linkLabelMceController.Name = "_linkLabelMceController";
            this._linkLabelMceController.Size = new System.Drawing.Size(408, 25);
            this._linkLabelMceController.TabIndex = 3;
            this._linkLabelMceController.TabStop = true;
            this._linkLabelMceController.Tag = "https://github.com/tig/mcec/blob/master/license.md";
            this._linkLabelMceController.Text = "License Agreement";
            this._linkLabelMceController.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelMceControllerLinkClicked);
            // 
            // _buttonOk
            // 
            this._buttonOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonOk.Location = new System.Drawing.Point(624, 271);
            this._buttonOk.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(120, 42);
            this._buttonOk.TabIndex = 0;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // _linkLabelKindelSystems
            // 
            this._linkLabelKindelSystems.AutoSize = true;
            this._linkLabelKindelSystems.LinkColor = System.Drawing.Color.Moccasin;
            this._linkLabelKindelSystems.Location = new System.Drawing.Point(330, 135);
            this._linkLabelKindelSystems.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._linkLabelKindelSystems.Name = "_linkLabelKindelSystems";
            this._linkLabelKindelSystems.Size = new System.Drawing.Size(213, 20);
            this._linkLabelKindelSystems.TabIndex = 1;
            this._linkLabelKindelSystems.TabStop = true;
            this._linkLabelKindelSystems.Tag = "http://www.kindel.com";
            this._linkLabelKindelSystems.Text = "© 2020 Kindel Systems, LLC.";
            this._linkLabelKindelSystems.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabelCharlieLinkClicked);
            // 
            // _labelSummary
            // 
            this._labelSummary.Location = new System.Drawing.Point(330, 172);
            this._labelSummary.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._labelSummary.Name = "_labelSummary";
            this._labelSummary.Size = new System.Drawing.Size(408, 49);
            this._labelSummary.TabIndex = 2;
            this._labelSummary.Text = "MCE Controller is distributed as freeware and published as open source under the " +
    "MIT License.";
            // 
            // _iconMcec
            // 
            this._iconMcec.Image = ((System.Drawing.Image)(resources.GetObject("_iconMcec.Image")));
            this._iconMcec.InitialImage = null;
            this._iconMcec.Location = new System.Drawing.Point(12, 25);
            this._iconMcec.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this._iconMcec.Name = "_iconMcec";
            this._iconMcec.Size = new System.Drawing.Size(288, 246);
            this._iconMcec.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._iconMcec.TabIndex = 5;
            this._iconMcec.TabStop = false;
            // 
            // _label1
            // 
            this._label1.AutoSize = true;
            this._label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this._label1.ForeColor = System.Drawing.Color.Brown;
            this._label1.Location = new System.Drawing.Point(95, 288);
            this._label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(112, 13);
            this._label1.TabIndex = 7;
            this._label1.Text = "Icon by Guillen Design";
            this._label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _labelVersion
            // 
            this._labelVersion.AutoSize = true;
            this._labelVersion.Location = new System.Drawing.Point(330, 98);
            this._labelVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._labelVersion.Name = "_labelVersion";
            this._labelVersion.Size = new System.Drawing.Size(114, 20);
            this._labelVersion.TabIndex = 7;
            this._labelVersion.Text = "Version a.b.c.d";
            // 
            // About
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(90)))), ((int)(((byte)(36)))));
            this.CancelButton = this._buttonOk;
            this.ClientSize = new System.Drawing.Size(760, 329);
            this.ControlBox = false;
            this.Controls.Add(this._labelVersion);
            this.Controls.Add(this._label1);
            this.Controls.Add(this._iconMcec);
            this.Controls.Add(this._labelSummary);
            this.Controls.Add(this._linkLabelKindelSystems);
            this.Controls.Add(this._buttonOk);
            this.Controls.Add(this._linkLabelMceController);
            this.Controls.Add(this._labelTitle);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "About";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About";
            ((System.ComponentModel.ISupportInitialize)(this._iconMcec)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }

}
