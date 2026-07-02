// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class SerialSettingsTab {
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
            this._checkBoxEnableSerialServer = new System.Windows.Forms.CheckBox();
            this._serialServerGroup = new System.Windows.Forms.GroupBox();
            this._comboBoxHandshake = new System.Windows.Forms.ComboBox();
            this._labelHandshake = new System.Windows.Forms.Label();
            this._comboBoxStopBits = new System.Windows.Forms.ComboBox();
            this._labelStopBits = new System.Windows.Forms.Label();
            this._comboBoxParity = new System.Windows.Forms.ComboBox();
            this._labelParity = new System.Windows.Forms.Label();
            this._comboBoxDataBits = new System.Windows.Forms.ComboBox();
            this._labelDataBits = new System.Windows.Forms.Label();
            this._comboBoxBaudRate = new System.Windows.Forms.ComboBox();
            this._labelBuadRate = new System.Windows.Forms.Label();
            this._comboBoxSerialPort = new System.Windows.Forms.ComboBox();
            this._labelSerialPort = new System.Windows.Forms.Label();
            this._serialServerGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // _checkBoxEnableSerialServer
            //
            this._checkBoxEnableSerialServer.AutoSize = true;
            this._checkBoxEnableSerialServer.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableSerialServer.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableSerialServer.Name = "_checkBoxEnableSerialServer";
            this._checkBoxEnableSerialServer.Size = new System.Drawing.Size(129, 21);
            this._checkBoxEnableSerialServer.TabIndex = 1;
            this._checkBoxEnableSerialServer.Text = "Enable Serial Server";
            this._checkBoxEnableSerialServer.UseVisualStyleBackColor = true;
            this._checkBoxEnableSerialServer.CheckedChanged += new System.EventHandler(this.CheckBoxEnableSerialServerCheckedChanged);
            //
            // _serialServerGroup
            //
            this._serialServerGroup.BackColor = System.Drawing.SystemColors.Window;
            this._serialServerGroup.Controls.Add(this._comboBoxHandshake);
            this._serialServerGroup.Controls.Add(this._labelHandshake);
            this._serialServerGroup.Controls.Add(this._comboBoxStopBits);
            this._serialServerGroup.Controls.Add(this._labelStopBits);
            this._serialServerGroup.Controls.Add(this._comboBoxParity);
            this._serialServerGroup.Controls.Add(this._labelParity);
            this._serialServerGroup.Controls.Add(this._comboBoxDataBits);
            this._serialServerGroup.Controls.Add(this._labelDataBits);
            this._serialServerGroup.Controls.Add(this._comboBoxBaudRate);
            this._serialServerGroup.Controls.Add(this._labelBuadRate);
            this._serialServerGroup.Controls.Add(this._comboBoxSerialPort);
            this._serialServerGroup.Controls.Add(this._labelSerialPort);
            this._serialServerGroup.Location = new System.Drawing.Point(12, 11);
            this._serialServerGroup.Margin = new System.Windows.Forms.Padding(1);
            this._serialServerGroup.Name = "_serialServerGroup";
            this._serialServerGroup.Padding = new System.Windows.Forms.Padding(1);
            this._serialServerGroup.Size = new System.Drawing.Size(412, 221);
            this._serialServerGroup.TabIndex = 0;
            this._serialServerGroup.TabStop = false;
            //
            // _comboBoxHandshake
            //
            this._comboBoxHandshake.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxHandshake.FormattingEnabled = true;
            this._comboBoxHandshake.Items.AddRange([
            "None",
            "Xon / Xoff",
            "Hardware",
            "Both"]);
            this._comboBoxHandshake.Location = new System.Drawing.Point(95, 155);
            this._comboBoxHandshake.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxHandshake.Name = "_comboBoxHandshake";
            this._comboBoxHandshake.Size = new System.Drawing.Size(118, 21);
            this._comboBoxHandshake.TabIndex = 11;
            this._comboBoxHandshake.SelectedIndexChanged += new System.EventHandler(this.ComboBoxHandshakeSelectedIndexChanged);
            //
            // _labelHandshake
            //
            this._labelHandshake.AutoSize = true;
            this._labelHandshake.Location = new System.Drawing.Point(16, 159);
            this._labelHandshake.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelHandshake.Name = "_labelHandshake";
            this._labelHandshake.Size = new System.Drawing.Size(65, 13);
            this._labelHandshake.TabIndex = 10;
            this._labelHandshake.Text = "&Handshake:";
            //
            // _comboBoxStopBits
            //
            this._comboBoxStopBits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxStopBits.FormattingEnabled = true;
            this._comboBoxStopBits.Items.AddRange([
            "1",
            "2",
            "1.5"]);
            this._comboBoxStopBits.Location = new System.Drawing.Point(95, 129);
            this._comboBoxStopBits.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxStopBits.Name = "_comboBoxStopBits";
            this._comboBoxStopBits.Size = new System.Drawing.Size(118, 21);
            this._comboBoxStopBits.TabIndex = 9;
            this._comboBoxStopBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStopBitsSelectedIndexChanged);
            //
            // _labelStopBits
            //
            this._labelStopBits.AutoSize = true;
            this._labelStopBits.Location = new System.Drawing.Point(16, 133);
            this._labelStopBits.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelStopBits.Name = "_labelStopBits";
            this._labelStopBits.Size = new System.Drawing.Size(52, 13);
            this._labelStopBits.TabIndex = 8;
            this._labelStopBits.Text = "&Stop Bits:";
            //
            // _comboBoxParity
            //
            this._comboBoxParity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxParity.FormattingEnabled = true;
            this._comboBoxParity.Items.AddRange([
            "None",
            "Odd",
            "Even",
            "Mark",
            "Space"]);
            this._comboBoxParity.Location = new System.Drawing.Point(95, 103);
            this._comboBoxParity.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxParity.Name = "_comboBoxParity";
            this._comboBoxParity.Size = new System.Drawing.Size(118, 21);
            this._comboBoxParity.TabIndex = 7;
            this._comboBoxParity.SelectedIndexChanged += new System.EventHandler(this.ComboBoxParitySelectedIndexChanged);
            //
            // _labelParity
            //
            this._labelParity.AutoSize = true;
            this._labelParity.Location = new System.Drawing.Point(16, 107);
            this._labelParity.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelParity.Name = "_labelParity";
            this._labelParity.Size = new System.Drawing.Size(36, 13);
            this._labelParity.TabIndex = 6;
            this._labelParity.Text = "&Parity:";
            //
            // _comboBoxDataBits
            //
            this._comboBoxDataBits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxDataBits.FormattingEnabled = true;
            this._comboBoxDataBits.Items.AddRange([
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"]);
            this._comboBoxDataBits.Location = new System.Drawing.Point(95, 78);
            this._comboBoxDataBits.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxDataBits.Name = "_comboBoxDataBits";
            this._comboBoxDataBits.Size = new System.Drawing.Size(118, 21);
            this._comboBoxDataBits.TabIndex = 5;
            this._comboBoxDataBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxDataBitsSelectedIndexChanged);
            //
            // _labelDataBits
            //
            this._labelDataBits.AutoSize = true;
            this._labelDataBits.Location = new System.Drawing.Point(16, 81);
            this._labelDataBits.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelDataBits.Name = "_labelDataBits";
            this._labelDataBits.Size = new System.Drawing.Size(53, 13);
            this._labelDataBits.TabIndex = 4;
            this._labelDataBits.Text = "&Data Bits:";
            //
            // _comboBoxBaudRate
            //
            this._comboBoxBaudRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxBaudRate.FormattingEnabled = true;
            this._comboBoxBaudRate.Items.AddRange([
            "2400",
            "4800",
            "9600",
            "19200",
            "38400",
            "57600",
            "115200"]);
            this._comboBoxBaudRate.Location = new System.Drawing.Point(95, 52);
            this._comboBoxBaudRate.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxBaudRate.Name = "_comboBoxBaudRate";
            this._comboBoxBaudRate.Size = new System.Drawing.Size(118, 21);
            this._comboBoxBaudRate.TabIndex = 3;
            this._comboBoxBaudRate.SelectedIndexChanged += new System.EventHandler(this.ComboBoxBaudRateSelectedIndexChanged);
            //
            // _labelBuadRate
            //
            this._labelBuadRate.AutoSize = true;
            this._labelBuadRate.Location = new System.Drawing.Point(16, 55);
            this._labelBuadRate.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelBuadRate.Name = "_labelBuadRate";
            this._labelBuadRate.Size = new System.Drawing.Size(61, 13);
            this._labelBuadRate.TabIndex = 2;
            this._labelBuadRate.Text = "&Baud Rate:";
            //
            // _comboBoxSerialPort
            //
            this._comboBoxSerialPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxSerialPort.FormattingEnabled = true;
            this._comboBoxSerialPort.Items.AddRange([
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8"]);
            this._comboBoxSerialPort.Location = new System.Drawing.Point(95, 26);
            this._comboBoxSerialPort.Margin = new System.Windows.Forms.Padding(1);
            this._comboBoxSerialPort.Name = "_comboBoxSerialPort";
            this._comboBoxSerialPort.Size = new System.Drawing.Size(118, 21);
            this._comboBoxSerialPort.TabIndex = 1;
            this._comboBoxSerialPort.SelectedIndexChanged += new System.EventHandler(this.ComboBoxSerialPortSelectedIndexChanged);
            //
            // _labelSerialPort
            //
            this._labelSerialPort.AutoSize = true;
            this._labelSerialPort.Location = new System.Drawing.Point(16, 32);
            this._labelSerialPort.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelSerialPort.Name = "_labelSerialPort";
            this._labelSerialPort.Size = new System.Drawing.Size(29, 13);
            this._labelSerialPort.TabIndex = 0;
            this._labelSerialPort.Text = "&Port:";
            //
            // SerialSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this._checkBoxEnableSerialServer);
            this.Controls.Add(this._serialServerGroup);
            this.Name = "SerialSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this._serialServerGroup.ResumeLayout(false);
            this._serialServerGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _checkBoxEnableSerialServer;
        private System.Windows.Forms.GroupBox _serialServerGroup;
        private System.Windows.Forms.ComboBox _comboBoxHandshake;
        private System.Windows.Forms.Label _labelHandshake;
        private System.Windows.Forms.ComboBox _comboBoxStopBits;
        private System.Windows.Forms.Label _labelStopBits;
        private System.Windows.Forms.ComboBox _comboBoxParity;
        private System.Windows.Forms.Label _labelParity;
        private System.Windows.Forms.ComboBox _comboBoxDataBits;
        private System.Windows.Forms.Label _labelDataBits;
        private System.Windows.Forms.ComboBox _comboBoxBaudRate;
        private System.Windows.Forms.Label _labelBuadRate;
        private System.Windows.Forms.ComboBox _comboBoxSerialPort;
        private System.Windows.Forms.Label _labelSerialPort;
    }
}
