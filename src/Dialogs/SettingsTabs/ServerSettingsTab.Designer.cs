// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class ServerSettingsTab {
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
            this.components = new System.ComponentModel.Container();
            this._checkBoxEnableServer = new System.Windows.Forms.CheckBox();
            this._serverGroup = new System.Windows.Forms.GroupBox();
            this._editServerPort = new System.Windows.Forms.TextBox();
            this._label1 = new System.Windows.Forms.Label();
            this._checkBoxEnableWakeup = new System.Windows.Forms.CheckBox();
            this._wakeupGroup = new System.Windows.Forms.GroupBox();
            this._editWakeupServer = new System.Windows.Forms.TextBox();
            this._editWakeupCommand = new System.Windows.Forms.TextBox();
            this._editClosingCommand = new System.Windows.Forms.TextBox();
            this._editWakeupPort = new System.Windows.Forms.TextBox();
            this._label5 = new System.Windows.Forms.Label();
            this._label2 = new System.Windows.Forms.Label();
            this._label4 = new System.Windows.Forms.Label();
            this._label3 = new System.Windows.Forms.Label();
            this._toolTipServer = new System.Windows.Forms.ToolTip(this.components);
            this._serverGroup.SuspendLayout();
            this._wakeupGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // _checkBoxEnableServer
            //
            this._checkBoxEnableServer.AutoSize = true;
            this._checkBoxEnableServer.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableServer.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableServer.Name = "_checkBoxEnableServer";
            this._checkBoxEnableServer.Size = new System.Drawing.Size(100, 21);
            this._checkBoxEnableServer.TabIndex = 0;
            this._checkBoxEnableServer.Text = "Enable &Server";
            this._toolTipServer.SetToolTip(this._checkBoxEnableServer, "Enables the TCP/IP server. It will listen on the specified port for commands.");
            this._checkBoxEnableServer.CheckedChanged += new System.EventHandler(this.CheckBoxEnableServerCheckedChanged);
            //
            // _serverGroup
            //
            this._serverGroup.BackColor = System.Drawing.SystemColors.Window;
            this._serverGroup.Controls.Add(this._editServerPort);
            this._serverGroup.Controls.Add(this._label1);
            this._serverGroup.Controls.Add(this._checkBoxEnableWakeup);
            this._serverGroup.Controls.Add(this._wakeupGroup);
            this._serverGroup.Location = new System.Drawing.Point(12, 11);
            this._serverGroup.Margin = new System.Windows.Forms.Padding(1);
            this._serverGroup.Name = "_serverGroup";
            this._serverGroup.Padding = new System.Windows.Forms.Padding(1);
            this._serverGroup.Size = new System.Drawing.Size(412, 221);
            this._serverGroup.TabIndex = 1;
            this._serverGroup.TabStop = false;
            //
            // _editServerPort
            //
            this._editServerPort.Location = new System.Drawing.Point(48, 23);
            this._editServerPort.Margin = new System.Windows.Forms.Padding(1);
            this._editServerPort.Name = "_editServerPort";
            this._editServerPort.Size = new System.Drawing.Size(58, 20);
            this._editServerPort.TabIndex = 1;
            this._editServerPort.TextChanged += new System.EventHandler(this.EditServerPortTextChanged);
            //
            // _label1
            //
            this._label1.Location = new System.Drawing.Point(13, 26);
            this._label1.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(32, 16);
            this._label1.TabIndex = 0;
            this._label1.Text = "&Port:";
            //
            // _checkBoxEnableWakeup
            //
            this._checkBoxEnableWakeup.Location = new System.Drawing.Point(26, 52);
            this._checkBoxEnableWakeup.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableWakeup.Name = "_checkBoxEnableWakeup";
            this._checkBoxEnableWakeup.Size = new System.Drawing.Size(104, 15);
            this._checkBoxEnableWakeup.TabIndex = 3;
            this._checkBoxEnableWakeup.Text = "Enable &Wakeup";
            this._checkBoxEnableWakeup.CheckedChanged += new System.EventHandler(this.CheckBoxEnableWakeupCheckedChanged);
            //
            // _wakeupGroup
            //
            this._wakeupGroup.Controls.Add(this._editWakeupServer);
            this._wakeupGroup.Controls.Add(this._editWakeupCommand);
            this._wakeupGroup.Controls.Add(this._editClosingCommand);
            this._wakeupGroup.Controls.Add(this._editWakeupPort);
            this._wakeupGroup.Controls.Add(this._label5);
            this._wakeupGroup.Controls.Add(this._label2);
            this._wakeupGroup.Controls.Add(this._label4);
            this._wakeupGroup.Controls.Add(this._label3);
            this._wakeupGroup.Location = new System.Drawing.Point(16, 53);
            this._wakeupGroup.Margin = new System.Windows.Forms.Padding(1);
            this._wakeupGroup.Name = "_wakeupGroup";
            this._wakeupGroup.Padding = new System.Windows.Forms.Padding(1);
            this._wakeupGroup.Size = new System.Drawing.Size(384, 155);
            this._wakeupGroup.TabIndex = 2;
            this._wakeupGroup.TabStop = false;
            //
            // _editWakeupServer
            //
            this._editWakeupServer.Location = new System.Drawing.Point(17, 40);
            this._editWakeupServer.Margin = new System.Windows.Forms.Padding(1);
            this._editWakeupServer.Name = "_editWakeupServer";
            this._editWakeupServer.Size = new System.Drawing.Size(162, 20);
            this._editWakeupServer.TabIndex = 1;
            this._editWakeupServer.TextChanged += new System.EventHandler(this.EditWakeupServerTextChanged);
            //
            // _editWakeupCommand
            //
            this._editWakeupCommand.Location = new System.Drawing.Point(16, 80);
            this._editWakeupCommand.Margin = new System.Windows.Forms.Padding(1);
            this._editWakeupCommand.Name = "_editWakeupCommand";
            this._editWakeupCommand.Size = new System.Drawing.Size(162, 20);
            this._editWakeupCommand.TabIndex = 3;
            this._editWakeupCommand.TextChanged += new System.EventHandler(this.EditWakeupCommandTextChanged);
            //
            // _editClosingCommand
            //
            this._editClosingCommand.Location = new System.Drawing.Point(16, 120);
            this._editClosingCommand.Margin = new System.Windows.Forms.Padding(1);
            this._editClosingCommand.Name = "_editClosingCommand";
            this._editClosingCommand.Size = new System.Drawing.Size(162, 20);
            this._editClosingCommand.TabIndex = 5;
            this._editClosingCommand.TextChanged += new System.EventHandler(this.EditClosingCommandTextChanged);
            //
            // _editWakeupPort
            //
            this._editWakeupPort.Location = new System.Drawing.Point(192, 40);
            this._editWakeupPort.Margin = new System.Windows.Forms.Padding(1);
            this._editWakeupPort.Name = "_editWakeupPort";
            this._editWakeupPort.Size = new System.Drawing.Size(58, 20);
            this._editWakeupPort.TabIndex = 7;
            this._editWakeupPort.TextChanged += new System.EventHandler(this.EditWakeupPortTextChanged);
            //
            // _label5
            //
            this._label5.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label5.Location = new System.Drawing.Point(16, 104);
            this._label5.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label5.Name = "_label5";
            this._label5.Size = new System.Drawing.Size(104, 16);
            this._label5.TabIndex = 4;
            this._label5.Text = "Closing Command:";
            //
            // _label2
            //
            this._label2.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label2.Location = new System.Drawing.Point(16, 24);
            this._label2.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label2.Name = "_label2";
            this._label2.Size = new System.Drawing.Size(88, 15);
            this._label2.TabIndex = 0;
            this._label2.Text = "Wa&keup Host:";
            //
            // _label4
            //
            this._label4.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label4.Location = new System.Drawing.Point(16, 64);
            this._label4.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label4.Name = "_label4";
            this._label4.Size = new System.Drawing.Size(104, 16);
            this._label4.TabIndex = 2;
            this._label4.Text = "Wakeup Command:";
            //
            // _label3
            //
            this._label3.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label3.Location = new System.Drawing.Point(189, 22);
            this._label3.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label3.Name = "_label3";
            this._label3.Size = new System.Drawing.Size(48, 15);
            this._label3.TabIndex = 6;
            this._label3.Text = "P&ort:";
            //
            // _toolTipServer
            //
            this._toolTipServer.ToolTipTitle = "Server";
            //
            // ServerSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this._checkBoxEnableServer);
            this.Controls.Add(this._serverGroup);
            this.Name = "ServerSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this._serverGroup.ResumeLayout(false);
            this._serverGroup.PerformLayout();
            this._wakeupGroup.ResumeLayout(false);
            this._wakeupGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _checkBoxEnableServer;
        private System.Windows.Forms.GroupBox _serverGroup;
        private System.Windows.Forms.TextBox _editServerPort;
        private System.Windows.Forms.Label _label1;
        private System.Windows.Forms.CheckBox _checkBoxEnableWakeup;
        private System.Windows.Forms.GroupBox _wakeupGroup;
        private System.Windows.Forms.TextBox _editWakeupServer;
        private System.Windows.Forms.TextBox _editWakeupCommand;
        private System.Windows.Forms.TextBox _editClosingCommand;
        private System.Windows.Forms.TextBox _editWakeupPort;
        private System.Windows.Forms.Label _label5;
        private System.Windows.Forms.Label _label2;
        private System.Windows.Forms.Label _label4;
        private System.Windows.Forms.Label _label3;
        private System.Windows.Forms.ToolTip _toolTipServer;
    }
}
