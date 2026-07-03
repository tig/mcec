// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class SettingsDialog {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this._buttonCancel = new System.Windows.Forms.Button();
            this._buttonOk = new System.Windows.Forms.Button();
            this._tabcontrol = new System.Windows.Forms.TabControl();
            this._tabPageGeneral = new System.Windows.Forms.TabPage();
            this._tabGeneral = new MCEControl.GeneralSettingsTab();
            this._tabPageClient = new System.Windows.Forms.TabPage();
            this._tabClient = new MCEControl.ClientSettingsTab();
            this._tabPageServer = new System.Windows.Forms.TabPage();
            this._tabServer = new MCEControl.ServerSettingsTab();
            this._tabPageSerial = new System.Windows.Forms.TabPage();
            this._tabSerial = new MCEControl.SerialSettingsTab();
            this._tabPageActivityMonitor = new System.Windows.Forms.TabPage();
            this._tabActivityMonitor = new MCEControl.ActivityMonitorSettingsTab();
            this._tabPageAgent = new System.Windows.Forms.TabPage();
            this._tabAgent = new MCEControl.AgentSettingsTab();
            this._eventLog = new System.Diagnostics.EventLog();
            this._tabcontrol.SuspendLayout();
            this._tabPageGeneral.SuspendLayout();
            this._tabPageClient.SuspendLayout();
            this._tabPageServer.SuspendLayout();
            this._tabPageSerial.SuspendLayout();
            this._tabPageActivityMonitor.SuspendLayout();
            this._tabPageAgent.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._eventLog)).BeginInit();
            this.SuspendLayout();
            //
            // _buttonCancel
            //
            this._buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonCancel.Location = new System.Drawing.Point(384, 288);
            this._buttonCancel.Margin = new System.Windows.Forms.Padding(1);
            this._buttonCancel.Name = "_buttonCancel";
            this._buttonCancel.Size = new System.Drawing.Size(75, 24);
            this._buttonCancel.TabIndex = 2;
            this._buttonCancel.Text = "Cancel";
            this._buttonCancel.UseVisualStyleBackColor = true;
            this._buttonCancel.Click += new System.EventHandler(this.ButtonCancelClick);
            //
            // _buttonOk
            //
            this._buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._buttonOk.Location = new System.Drawing.Point(296, 288);
            this._buttonOk.Margin = new System.Windows.Forms.Padding(1);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(75, 24);
            this._buttonOk.TabIndex = 1;
            this._buttonOk.Text = "OK";
            this._buttonOk.UseVisualStyleBackColor = true;
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            //
            // _tabcontrol
            //
            this._tabcontrol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._tabcontrol.Controls.Add(this._tabPageGeneral);
            this._tabcontrol.Controls.Add(this._tabPageClient);
            this._tabcontrol.Controls.Add(this._tabPageServer);
            this._tabcontrol.Controls.Add(this._tabPageSerial);
            this._tabcontrol.Controls.Add(this._tabPageActivityMonitor);
            this._tabcontrol.Controls.Add(this._tabPageAgent);
            this._tabcontrol.Location = new System.Drawing.Point(16, 16);
            this._tabcontrol.Margin = new System.Windows.Forms.Padding(1);
            this._tabcontrol.Name = "_tabcontrol";
            this._tabcontrol.SelectedIndex = 0;
            this._tabcontrol.Size = new System.Drawing.Size(448, 264);
            this._tabcontrol.TabIndex = 0;
            //
            // _tabPageGeneral
            //
            this._tabPageGeneral.BackColor = System.Drawing.SystemColors.Window;
            this._tabPageGeneral.Controls.Add(this._tabGeneral);
            this._tabPageGeneral.Location = new System.Drawing.Point(4, 22);
            this._tabPageGeneral.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageGeneral.Name = "_tabPageGeneral";
            this._tabPageGeneral.Size = new System.Drawing.Size(440, 238);
            this._tabPageGeneral.TabIndex = 0;
            this._tabPageGeneral.Text = "General";
            //
            // _tabGeneral
            //
            this._tabGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabGeneral.Location = new System.Drawing.Point(0, 0);
            this._tabGeneral.Name = "_tabGeneral";
            this._tabGeneral.Size = new System.Drawing.Size(440, 238);
            this._tabGeneral.TabIndex = 0;
            //
            // _tabPageClient
            //
            this._tabPageClient.BackColor = System.Drawing.SystemColors.Window;
            this._tabPageClient.Controls.Add(this._tabClient);
            this._tabPageClient.Location = new System.Drawing.Point(4, 22);
            this._tabPageClient.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageClient.Name = "_tabPageClient";
            this._tabPageClient.Size = new System.Drawing.Size(440, 238);
            this._tabPageClient.TabIndex = 1;
            this._tabPageClient.Text = "Client";
            //
            // _tabClient
            //
            this._tabClient.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabClient.Location = new System.Drawing.Point(0, 0);
            this._tabClient.Name = "_tabClient";
            this._tabClient.Size = new System.Drawing.Size(440, 238);
            this._tabClient.TabIndex = 0;
            //
            // _tabPageServer
            //
            this._tabPageServer.BackColor = System.Drawing.SystemColors.Window;
            this._tabPageServer.Controls.Add(this._tabServer);
            this._tabPageServer.Location = new System.Drawing.Point(4, 22);
            this._tabPageServer.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageServer.Name = "_tabPageServer";
            this._tabPageServer.Size = new System.Drawing.Size(440, 238);
            this._tabPageServer.TabIndex = 2;
            this._tabPageServer.Text = "Server";
            //
            // _tabServer
            //
            this._tabServer.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabServer.Location = new System.Drawing.Point(0, 0);
            this._tabServer.Name = "_tabServer";
            this._tabServer.Size = new System.Drawing.Size(440, 238);
            this._tabServer.TabIndex = 0;
            //
            // _tabPageSerial
            //
            this._tabPageSerial.BackColor = System.Drawing.SystemColors.Window;
            this._tabPageSerial.Controls.Add(this._tabSerial);
            this._tabPageSerial.Location = new System.Drawing.Point(4, 22);
            this._tabPageSerial.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageSerial.Name = "_tabPageSerial";
            this._tabPageSerial.Padding = new System.Windows.Forms.Padding(1);
            this._tabPageSerial.Size = new System.Drawing.Size(440, 238);
            this._tabPageSerial.TabIndex = 3;
            this._tabPageSerial.Text = "Serial Server";
            //
            // _tabSerial
            //
            this._tabSerial.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabSerial.Location = new System.Drawing.Point(1, 1);
            this._tabSerial.Name = "_tabSerial";
            this._tabSerial.Size = new System.Drawing.Size(438, 236);
            this._tabSerial.TabIndex = 0;
            //
            // _tabPageActivityMonitor
            //
            this._tabPageActivityMonitor.Controls.Add(this._tabActivityMonitor);
            this._tabPageActivityMonitor.Location = new System.Drawing.Point(4, 22);
            this._tabPageActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageActivityMonitor.Name = "_tabPageActivityMonitor";
            this._tabPageActivityMonitor.Size = new System.Drawing.Size(440, 238);
            this._tabPageActivityMonitor.TabIndex = 4;
            this._tabPageActivityMonitor.Text = "Activity Monitor";
            this._tabPageActivityMonitor.UseVisualStyleBackColor = true;
            //
            // _tabActivityMonitor
            //
            this._tabActivityMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabActivityMonitor.Location = new System.Drawing.Point(0, 0);
            this._tabActivityMonitor.Name = "_tabActivityMonitor";
            this._tabActivityMonitor.Size = new System.Drawing.Size(440, 238);
            this._tabActivityMonitor.TabIndex = 0;
            //
            // _tabPageAgent
            //
            this._tabPageAgent.BackColor = System.Drawing.SystemColors.Window;
            this._tabPageAgent.Controls.Add(this._tabAgent);
            this._tabPageAgent.Location = new System.Drawing.Point(4, 22);
            this._tabPageAgent.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageAgent.Name = "_tabPageAgent";
            this._tabPageAgent.Size = new System.Drawing.Size(440, 238);
            this._tabPageAgent.TabIndex = 5;
            this._tabPageAgent.Text = "Agent";
            //
            // _tabAgent
            //
            this._tabAgent.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabAgent.Location = new System.Drawing.Point(0, 0);
            this._tabAgent.Name = "_tabAgent";
            this._tabAgent.Size = new System.Drawing.Size(440, 238);
            this._tabAgent.TabIndex = 0;
            //
            // _eventLog
            //
            this._eventLog.SynchronizingObject = this;
            //
            // SettingsDialog
            //
            this.AcceptButton = this._buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.CancelButton = this._buttonCancel;
            this.ClientSize = new System.Drawing.Size(475, 321);
            this.Controls.Add(this._tabcontrol);
            this.Controls.Add(this._buttonCancel);
            this.Controls.Add(this._buttonOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this.Load += new System.EventHandler(this.SettingsDialog_Load);
            this._tabcontrol.ResumeLayout(false);
            this._tabPageGeneral.ResumeLayout(false);
            this._tabPageClient.ResumeLayout(false);
            this._tabPageServer.ResumeLayout(false);
            this._tabPageSerial.ResumeLayout(false);
            this._tabPageActivityMonitor.ResumeLayout(false);
            this._tabPageAgent.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._eventLog)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button _buttonCancel;
        private System.Windows.Forms.Button _buttonOk;
        private System.Windows.Forms.TabControl _tabcontrol;
        private System.Windows.Forms.TabPage _tabPageGeneral;
        private MCEControl.GeneralSettingsTab _tabGeneral;
        private System.Windows.Forms.TabPage _tabPageClient;
        private MCEControl.ClientSettingsTab _tabClient;
        private System.Windows.Forms.TabPage _tabPageServer;
        private MCEControl.ServerSettingsTab _tabServer;
        private System.Windows.Forms.TabPage _tabPageSerial;
        private MCEControl.SerialSettingsTab _tabSerial;
        private System.Windows.Forms.TabPage _tabPageActivityMonitor;
        private MCEControl.ActivityMonitorSettingsTab _tabActivityMonitor;
        private System.Windows.Forms.TabPage _tabPageAgent;
        private MCEControl.AgentSettingsTab _tabAgent;
        private System.Diagnostics.EventLog _eventLog;
    }
}
