// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class ClientSettingsTab {
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
            this._checkBoxEnableClient = new System.Windows.Forms.CheckBox();
            this._clientGroup = new System.Windows.Forms.GroupBox();
            this._editClientPort = new System.Windows.Forms.TextBox();
            this._label6 = new System.Windows.Forms.Label();
            this._label8 = new System.Windows.Forms.Label();
            this._editClientHost = new System.Windows.Forms.TextBox();
            this._label7 = new System.Windows.Forms.Label();
            this._editClientDelayTime = new System.Windows.Forms.TextBox();
            this._toolTipClient = new System.Windows.Forms.ToolTip(this.components);
            this._clientGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // _checkBoxEnableClient
            //
            this._checkBoxEnableClient.AutoSize = true;
            this._checkBoxEnableClient.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableClient.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableClient.Name = "_checkBoxEnableClient";
            this._checkBoxEnableClient.Size = new System.Drawing.Size(95, 21);
            this._checkBoxEnableClient.TabIndex = 1;
            this._checkBoxEnableClient.Text = "Enable &Client";
            this._toolTipClient.SetToolTip(this._checkBoxEnableClient, "Starts a TCP/IP client connection to the specified address:port. Commands will be" +
        " recieved as replies.");
            this._checkBoxEnableClient.CheckedChanged += new System.EventHandler(this.CheckEnableClientCheckedChanged);
            //
            // _clientGroup
            //
            this._clientGroup.BackColor = System.Drawing.SystemColors.Window;
            this._clientGroup.Controls.Add(this._editClientPort);
            this._clientGroup.Controls.Add(this._label6);
            this._clientGroup.Controls.Add(this._label8);
            this._clientGroup.Controls.Add(this._editClientHost);
            this._clientGroup.Controls.Add(this._label7);
            this._clientGroup.Controls.Add(this._editClientDelayTime);
            this._clientGroup.Location = new System.Drawing.Point(12, 11);
            this._clientGroup.Margin = new System.Windows.Forms.Padding(1);
            this._clientGroup.Name = "_clientGroup";
            this._clientGroup.Padding = new System.Windows.Forms.Padding(1);
            this._clientGroup.Size = new System.Drawing.Size(412, 221);
            this._clientGroup.TabIndex = 0;
            this._clientGroup.TabStop = false;
            //
            // _editClientPort
            //
            this._editClientPort.Location = new System.Drawing.Point(16, 88);
            this._editClientPort.Margin = new System.Windows.Forms.Padding(1);
            this._editClientPort.Name = "_editClientPort";
            this._editClientPort.Size = new System.Drawing.Size(58, 20);
            this._editClientPort.TabIndex = 4;
            this._editClientPort.TextChanged += new System.EventHandler(this.EditClientPortTextChanged);
            //
            // _label6
            //
            this._label6.Location = new System.Drawing.Point(16, 72);
            this._label6.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label6.Name = "_label6";
            this._label6.Size = new System.Drawing.Size(32, 16);
            this._label6.TabIndex = 3;
            this._label6.Text = "&Port:";
            //
            // _label8
            //
            this._label8.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label8.Location = new System.Drawing.Point(16, 31);
            this._label8.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label8.Name = "_label8";
            this._label8.Size = new System.Drawing.Size(88, 16);
            this._label8.TabIndex = 1;
            this._label8.Text = "&Host:";
            //
            // _editClientHost
            //
            this._editClientHost.Location = new System.Drawing.Point(16, 48);
            this._editClientHost.Margin = new System.Windows.Forms.Padding(1);
            this._editClientHost.Name = "_editClientHost";
            this._editClientHost.Size = new System.Drawing.Size(162, 20);
            this._editClientHost.TabIndex = 2;
            this._editClientHost.TextChanged += new System.EventHandler(this.EditClientHostTextChanged);
            //
            // _label7
            //
            this._label7.Location = new System.Drawing.Point(16, 112);
            this._label7.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._label7.Name = "_label7";
            this._label7.Size = new System.Drawing.Size(144, 16);
            this._label7.TabIndex = 5;
            this._label7.Text = "&Reconnect Wait Time (ms):";
            //
            // _editClientDelayTime
            //
            this._editClientDelayTime.Location = new System.Drawing.Point(16, 128);
            this._editClientDelayTime.Margin = new System.Windows.Forms.Padding(1);
            this._editClientDelayTime.Name = "_editClientDelayTime";
            this._editClientDelayTime.Size = new System.Drawing.Size(58, 20);
            this._editClientDelayTime.TabIndex = 0;
            this._editClientDelayTime.TextChanged += new System.EventHandler(this.EditClientDelayTimeTextChanged);
            //
            // _toolTipClient
            //
            this._toolTipClient.ToolTipTitle = "Client";
            //
            // ClientSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this._checkBoxEnableClient);
            this.Controls.Add(this._clientGroup);
            this.Name = "ClientSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this._clientGroup.ResumeLayout(false);
            this._clientGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _checkBoxEnableClient;
        private System.Windows.Forms.GroupBox _clientGroup;
        private System.Windows.Forms.TextBox _editClientPort;
        private System.Windows.Forms.Label _label6;
        private System.Windows.Forms.Label _label8;
        private System.Windows.Forms.TextBox _editClientHost;
        private System.Windows.Forms.Label _label7;
        private System.Windows.Forms.TextBox _editClientDelayTime;
        private System.Windows.Forms.ToolTip _toolTipClient;
    }
}
