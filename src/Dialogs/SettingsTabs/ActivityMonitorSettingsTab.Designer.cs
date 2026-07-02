// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class ActivityMonitorSettingsTab {
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
            this._checkBoxEnableActivityMonitor = new System.Windows.Forms.CheckBox();
            this._groupBoxActivityMonitor = new System.Windows.Forms.GroupBox();
            this._presenceDetection = new System.Windows.Forms.CheckBox();
            this._unlockDetection = new System.Windows.Forms.CheckBox();
            this._inputDetection = new System.Windows.Forms.CheckBox();
            this._labelActivityDebounceTime = new System.Windows.Forms.Label();
            this._textBoxDebounceTime = new System.Windows.Forms.TextBox();
            this._textBoxActivityCommand = new System.Windows.Forms.TextBox();
            this._labelActivityCommand = new System.Windows.Forms.Label();
            this._groupBoxActivityMonitor.SuspendLayout();
            this.SuspendLayout();
            //
            // _checkBoxEnableActivityMonitor
            //
            this._checkBoxEnableActivityMonitor.AutoSize = true;
            this._checkBoxEnableActivityMonitor.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableActivityMonitor.Name = "_checkBoxEnableActivityMonitor";
            this._checkBoxEnableActivityMonitor.Size = new System.Drawing.Size(166, 21);
            this._checkBoxEnableActivityMonitor.TabIndex = 0;
            this._checkBoxEnableActivityMonitor.Text = "Enable &User Activity Monitor";
            this._checkBoxEnableActivityMonitor.UseVisualStyleBackColor = true;
            this._checkBoxEnableActivityMonitor.CheckedChanged += new System.EventHandler(this.checkBoxEnableActivityMonitor_CheckedChanged);
            //
            // _groupBoxActivityMonitor
            //
            this._groupBoxActivityMonitor.Controls.Add(this._presenceDetection);
            this._groupBoxActivityMonitor.Controls.Add(this._unlockDetection);
            this._groupBoxActivityMonitor.Controls.Add(this._inputDetection);
            this._groupBoxActivityMonitor.Controls.Add(this._labelActivityDebounceTime);
            this._groupBoxActivityMonitor.Controls.Add(this._textBoxDebounceTime);
            this._groupBoxActivityMonitor.Controls.Add(this._textBoxActivityCommand);
            this._groupBoxActivityMonitor.Controls.Add(this._labelActivityCommand);
            this._groupBoxActivityMonitor.Location = new System.Drawing.Point(12, 11);
            this._groupBoxActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._groupBoxActivityMonitor.Name = "_groupBoxActivityMonitor";
            this._groupBoxActivityMonitor.Padding = new System.Windows.Forms.Padding(1);
            this._groupBoxActivityMonitor.Size = new System.Drawing.Size(412, 221);
            this._groupBoxActivityMonitor.TabIndex = 0;
            this._groupBoxActivityMonitor.TabStop = false;
            //
            // _presenceDetection
            //
            this._presenceDetection.AutoSize = true;
            this._presenceDetection.Location = new System.Drawing.Point(17, 76);
            this._presenceDetection.Name = "_presenceDetection";
            this._presenceDetection.Size = new System.Drawing.Size(375, 21);
            this._presenceDetection.TabIndex = 3;
            this._presenceDetection.Text = "Don\'t send message if Power Broadcast API indicates user is not present";
            this._presenceDetection.UseVisualStyleBackColor = true;
            this._presenceDetection.CheckedChanged += new System.EventHandler(this.presenceDetectionRadio_CheckedChanged);
            //
            // _unlockDetection
            //
            this._unlockDetection.AutoSize = true;
            this._unlockDetection.Location = new System.Drawing.Point(17, 53);
            this._unlockDetection.Name = "_unlockDetection";
            this._unlockDetection.Size = new System.Drawing.Size(223, 21);
            this._unlockDetection.TabIndex = 1;
            this._unlockDetection.Text = "Don\'t send message if desktop is locked";
            this._unlockDetection.UseVisualStyleBackColor = true;
            this._unlockDetection.CheckedChanged += new System.EventHandler(this.unlockDetectionRadio_CheckedChanged);
            //
            // _inputDetection
            //
            this._inputDetection.AutoSize = true;
            this._inputDetection.Location = new System.Drawing.Point(17, 30);
            this._inputDetection.Name = "_inputDetection";
            this._inputDetection.Size = new System.Drawing.Size(296, 21);
            this._inputDetection.TabIndex = 0;
            this._inputDetection.Text = "Send message when keyboard/mouse input is detected";
            this._inputDetection.UseVisualStyleBackColor = true;
            this._inputDetection.CheckedChanged += new System.EventHandler(this.inputDetectionRadio_CheckedChanged);
            //
            // _labelActivityDebounceTime
            //
            this._labelActivityDebounceTime.AutoSize = true;
            this._labelActivityDebounceTime.Location = new System.Drawing.Point(14, 142);
            this._labelActivityDebounceTime.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelActivityDebounceTime.Name = "_labelActivityDebounceTime";
            this._labelActivityDebounceTime.Size = new System.Drawing.Size(274, 13);
            this._labelActivityDebounceTime.TabIndex = 4;
            this._labelActivityDebounceTime.Text = "Send activity mesage no more frequently than (seconds):";
            //
            // _textBoxDebounceTime
            //
            this._textBoxDebounceTime.Location = new System.Drawing.Point(299, 139);
            this._textBoxDebounceTime.Margin = new System.Windows.Forms.Padding(1);
            this._textBoxDebounceTime.Name = "_textBoxDebounceTime";
            this._textBoxDebounceTime.Size = new System.Drawing.Size(51, 20);
            this._textBoxDebounceTime.TabIndex = 5;
            this._textBoxDebounceTime.TextChanged += new System.EventHandler(this.textBoxDebounceTime_TextChanged);
            //
            // _textBoxActivityCommand
            //
            this._textBoxActivityCommand.Location = new System.Drawing.Point(111, 110);
            this._textBoxActivityCommand.Margin = new System.Windows.Forms.Padding(1);
            this._textBoxActivityCommand.Name = "_textBoxActivityCommand";
            this._textBoxActivityCommand.Size = new System.Drawing.Size(149, 20);
            this._textBoxActivityCommand.TabIndex = 3;
            this._textBoxActivityCommand.TextChanged += new System.EventHandler(this.textBoxActivityCommand_TextChanged);
            //
            // _labelActivityCommand
            //
            this._labelActivityCommand.AutoSize = true;
            this._labelActivityCommand.Location = new System.Drawing.Point(14, 113);
            this._labelActivityCommand.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelActivityCommand.Name = "_labelActivityCommand";
            this._labelActivityCommand.Size = new System.Drawing.Size(91, 13);
            this._labelActivityCommand.TabIndex = 2;
            this._labelActivityCommand.Text = "Message to send:";
            //
            // ActivityMonitorSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this._checkBoxEnableActivityMonitor);
            this.Controls.Add(this._groupBoxActivityMonitor);
            this.Name = "ActivityMonitorSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this._groupBoxActivityMonitor.ResumeLayout(false);
            this._groupBoxActivityMonitor.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _checkBoxEnableActivityMonitor;
        private System.Windows.Forms.GroupBox _groupBoxActivityMonitor;
        private System.Windows.Forms.CheckBox _presenceDetection;
        private System.Windows.Forms.CheckBox _unlockDetection;
        private System.Windows.Forms.CheckBox _inputDetection;
        private System.Windows.Forms.Label _labelActivityDebounceTime;
        private System.Windows.Forms.TextBox _textBoxDebounceTime;
        private System.Windows.Forms.TextBox _textBoxActivityCommand;
        private System.Windows.Forms.Label _labelActivityCommand;
    }
}
