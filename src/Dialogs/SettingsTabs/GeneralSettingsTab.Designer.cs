// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class GeneralSettingsTab {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null!;

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

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._textBoxPacing = new System.Windows.Forms.TextBox();
            this._labelPacing = new System.Windows.Forms.Label();
            this._comboBoxLogThresholds = new System.Windows.Forms.ComboBox();
            this._labelLogLevel = new System.Windows.Forms.Label();
            this._checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
            this._checkBoxDisableUpdatePopup = new System.Windows.Forms.CheckBox();
            this._checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            //
            // _textBoxPacing
            //
            this._textBoxPacing.Location = new System.Drawing.Point(168, 113);
            this._textBoxPacing.Margin = new System.Windows.Forms.Padding(2);
            this._textBoxPacing.Name = "_textBoxPacing";
            this._textBoxPacing.Size = new System.Drawing.Size(74, 20);
            this._textBoxPacing.TabIndex = 5;
            this._textBoxPacing.TextChanged += new System.EventHandler(this.textBoxPacing_TextChanged);
            //
            // _labelPacing
            //
            this._labelPacing.AutoSize = true;
            this._labelPacing.Location = new System.Drawing.Point(16, 113);
            this._labelPacing.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelPacing.Name = "_labelPacing";
            this._labelPacing.Size = new System.Drawing.Size(150, 13);
            this._labelPacing.TabIndex = 4;
            this._labelPacing.Text = "Default command &pacing (ms):";
            //
            // _comboBoxLogThresholds
            //
            this._comboBoxLogThresholds.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxLogThresholds.FormattingEnabled = true;
            this._comboBoxLogThresholds.Location = new System.Drawing.Point(16, 68);
            this._comboBoxLogThresholds.Name = "_comboBoxLogThresholds";
            this._comboBoxLogThresholds.Size = new System.Drawing.Size(121, 21);
            this._comboBoxLogThresholds.TabIndex = 3;
            this._comboBoxLogThresholds.SelectedIndexChanged += new System.EventHandler(this.comboBoxLogThresholds_SelectedIndexChanged);
            //
            // _labelLogLevel
            //
            this._labelLogLevel.AutoSize = true;
            this._labelLogLevel.Location = new System.Drawing.Point(16, 52);
            this._labelLogLevel.Name = "_labelLogLevel";
            this._labelLogLevel.Size = new System.Drawing.Size(277, 13);
            this._labelLogLevel.TabIndex = 2;
            this._labelLogLevel.Text = "Log Threshold (display only; log files always contain ALL):";
            //
            // _checkBoxHideOnStartup
            //
            this._checkBoxHideOnStartup.Location = new System.Drawing.Point(16, 8);
            this._checkBoxHideOnStartup.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxHideOnStartup.Name = "_checkBoxHideOnStartup";
            this._checkBoxHideOnStartup.Size = new System.Drawing.Size(160, 15);
            this._checkBoxHideOnStartup.TabIndex = 0;
            this._checkBoxHideOnStartup.Text = "&Hide window on startup";
            this._checkBoxHideOnStartup.CheckedChanged += new System.EventHandler(this.CheckBoxHideOnStartupCheckedChanged);
            //
            // _checkBoxDisableUpdatePopup
            //
            this._checkBoxDisableUpdatePopup.AutoSize = true;
            this._checkBoxDisableUpdatePopup.Location = new System.Drawing.Point(16, 25);
            this._checkBoxDisableUpdatePopup.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxDisableUpdatePopup.Name = "_checkBoxDisableUpdatePopup";
            this._checkBoxDisableUpdatePopup.Size = new System.Drawing.Size(220, 17);
            this._checkBoxDisableUpdatePopup.TabIndex = 1;
            this._checkBoxDisableUpdatePopup.Text = "&Disable automatic update notification popup";
            this._checkBoxDisableUpdatePopup.CheckedChanged += new System.EventHandler(this.CheckBoxDisableUpdatePopupCheckedChanged);
            //
            // _checkBoxAutoStart
            //
            this._checkBoxAutoStart.Location = new System.Drawing.Point(32, 208);
            this._checkBoxAutoStart.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxAutoStart.Name = "_checkBoxAutoStart";
            this._checkBoxAutoStart.Size = new System.Drawing.Size(160, 16);
            this._checkBoxAutoStart.TabIndex = 6;
            this._checkBoxAutoStart.Text = "&Automatically start at login";
            this._checkBoxAutoStart.Visible = false;
            this._checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.CheckBoxAutoStartCheckedChanged);
            //
            // GeneralSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this._textBoxPacing);
            this.Controls.Add(this._labelPacing);
            this.Controls.Add(this._comboBoxLogThresholds);
            this.Controls.Add(this._labelLogLevel);
            this.Controls.Add(this._checkBoxHideOnStartup);
            this.Controls.Add(this._checkBoxDisableUpdatePopup);
            this.Controls.Add(this._checkBoxAutoStart);
            this.Name = "GeneralSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _textBoxPacing;
        private System.Windows.Forms.Label _labelPacing;
        private System.Windows.Forms.ComboBox _comboBoxLogThresholds;
        private System.Windows.Forms.Label _labelLogLevel;
        private System.Windows.Forms.CheckBox _checkBoxHideOnStartup;
        private System.Windows.Forms.CheckBox _checkBoxAutoStart;
        private System.Windows.Forms.CheckBox _checkBoxDisableUpdatePopup;
    }
}
