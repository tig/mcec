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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Settings dialog box
    /// </summary>
    public class SettingsDialog : Form {
        /// <summary>
        /// Required designer variable.
        /// </summary>

        private GroupBox _clientGroup;
        private TabPage _general;
        private GroupBox _serverGroup;

        public AppSettings Settings;
        private GroupBox _wakeupGroup;

        private Button _buttonCancel;
        private Button _buttonOk;
        private CheckBox _checkBoxAutoStart;
        private CheckBox _checkBoxEnableClient;
        private CheckBox _checkBoxEnableServer;
        private CheckBox _checkBoxEnableWakeup;
        private CheckBox _checkBoxHideOnStartup;
        private TextBox _editClientDelayTime;
        private TextBox _editClientHost;
        private TextBox _editClientPort;
        private TextBox _editClosingCommand;
        private TextBox _editServerPort;
        private TextBox _editWakeupCommand;
        private TextBox _editWakeupPort;
        private TextBox _editWakeupServer;
        private Label _label1;
        private Label _label2;
        private Label _label3;
        private Label _label4;
        private Label _label5;
        private Label _label6;
        private Label _label7;
        private Label _label8;
        private TabPage _tabClient;
        private TabControl _tabControl;
        private TabPage _tabSerialServer;
        private GroupBox _serialServerGroup;
        private CheckBox _checkBoxEnableSerialServer;
        private ComboBox _comboBoxHandshake;
        private Label _labelHandshake;
        private ComboBox _comboBoxStopBits;
        private Label _labelStopBits;
        private ComboBox _comboBoxParity;
        private Label _labelParity;
        private ComboBox _comboBoxDataBits;
        private Label _labelDataBits;
        private ComboBox _comboBoxBaudRate;
        private Label _labelBuadRate;
        private ComboBox _comboBoxSerialPort;
        private Label _labelSerialPort;
        private ToolTip toolTipClient;
        private System.ComponentModel.IContainer components;
        private ToolTip _toolTipServer;
        private CheckBox _checkBoxClientCmdUi;
        private TabPage _tabServer;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsDialog));
            this._buttonCancel = new System.Windows.Forms.Button();
            this._buttonOk = new System.Windows.Forms.Button();
            this._tabControl = new System.Windows.Forms.TabControl();
            this._general = new System.Windows.Forms.TabPage();
            this._checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
            this._checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this._tabClient = new System.Windows.Forms.TabPage();
            this._checkBoxEnableClient = new System.Windows.Forms.CheckBox();
            this._clientGroup = new System.Windows.Forms.GroupBox();
            this._checkBoxClientCmdUi = new System.Windows.Forms.CheckBox();
            this._editClientPort = new System.Windows.Forms.TextBox();
            this._label6 = new System.Windows.Forms.Label();
            this._label8 = new System.Windows.Forms.Label();
            this._editClientHost = new System.Windows.Forms.TextBox();
            this._label7 = new System.Windows.Forms.Label();
            this._editClientDelayTime = new System.Windows.Forms.TextBox();
            this._tabServer = new System.Windows.Forms.TabPage();
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
            this._tabSerialServer = new System.Windows.Forms.TabPage();
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
            this.toolTipClient = new System.Windows.Forms.ToolTip(this.components);
            this._toolTipServer = new System.Windows.Forms.ToolTip(this.components);
            this._tabControl.SuspendLayout();
            this._general.SuspendLayout();
            this._tabClient.SuspendLayout();
            this._clientGroup.SuspendLayout();
            this._tabServer.SuspendLayout();
            this._serverGroup.SuspendLayout();
            this._wakeupGroup.SuspendLayout();
            this._tabSerialServer.SuspendLayout();
            this._serialServerGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _buttonCancel
            // 
            this._buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonCancel.Location = new System.Drawing.Point(285, 291);
            this._buttonCancel.Name = "_buttonCancel";
            this._buttonCancel.Size = new System.Drawing.Size(75, 23);
            this._buttonCancel.TabIndex = 1;
            this._buttonCancel.Text = "Cancel";
            this._buttonCancel.Click += new System.EventHandler(this.ButtonCancelClick);
            // 
            // _buttonOk
            // 
            this._buttonOk.Location = new System.Drawing.Point(204, 291);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(75, 23);
            this._buttonOk.TabIndex = 0;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // _tabControl
            // 
            this._tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._tabControl.Controls.Add(this._general);
            this._tabControl.Controls.Add(this._tabClient);
            this._tabControl.Controls.Add(this._tabServer);
            this._tabControl.Controls.Add(this._tabSerialServer);
            this._tabControl.Location = new System.Drawing.Point(12, 12);
            this._tabControl.Name = "_tabControl";
            this._tabControl.SelectedIndex = 0;
            this._tabControl.Size = new System.Drawing.Size(348, 267);
            this._tabControl.TabIndex = 0;
            // 
            // _general
            // 
            this._general.Controls.Add(this._checkBoxHideOnStartup);
            this._general.Controls.Add(this._checkBoxAutoStart);
            this._general.Location = new System.Drawing.Point(4, 22);
            this._general.Name = "_general";
            this._general.Size = new System.Drawing.Size(340, 241);
            this._general.TabIndex = 0;
            this._general.Text = "General";
            // 
            // _checkBoxHideOnStartup
            // 
            this._checkBoxHideOnStartup.Location = new System.Drawing.Point(16, 8);
            this._checkBoxHideOnStartup.Name = "_checkBoxHideOnStartup";
            this._checkBoxHideOnStartup.Size = new System.Drawing.Size(160, 16);
            this._checkBoxHideOnStartup.TabIndex = 0;
            this._checkBoxHideOnStartup.Text = "&Hide window on startup";
            this._checkBoxHideOnStartup.CheckedChanged += new System.EventHandler(this.CheckBoxHideOnStartupCheckedChanged);
            // 
            // _checkBoxAutoStart
            // 
            this._checkBoxAutoStart.Location = new System.Drawing.Point(16, 32);
            this._checkBoxAutoStart.Name = "_checkBoxAutoStart";
            this._checkBoxAutoStart.Size = new System.Drawing.Size(160, 16);
            this._checkBoxAutoStart.TabIndex = 0;
            this._checkBoxAutoStart.Text = "&Automatically start at login";
            this._checkBoxAutoStart.Visible = false;
            this._checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.CheckBoxAutoStartCheckedChanged);
            // 
            // _tabClient
            // 
            this._tabClient.Controls.Add(this._checkBoxEnableClient);
            this._tabClient.Controls.Add(this._clientGroup);
            this._tabClient.Location = new System.Drawing.Point(4, 22);
            this._tabClient.Name = "_tabClient";
            this._tabClient.Size = new System.Drawing.Size(340, 241);
            this._tabClient.TabIndex = 1;
            this._tabClient.Text = "Client";
            // 
            // _checkBoxEnableClient
            // 
            this._checkBoxEnableClient.Location = new System.Drawing.Point(24, 6);
            this._checkBoxEnableClient.Name = "_checkBoxEnableClient";
            this._checkBoxEnableClient.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableClient.TabIndex = 0;
            this._checkBoxEnableClient.Text = "Enable &Client";
            this.toolTipClient.SetToolTip(this._checkBoxEnableClient, "Starts a TCP/IP client connection to the specified address:port. Commands will be" +
        " recieved as replies.");
            this._checkBoxEnableClient.CheckedChanged += new System.EventHandler(this.CheckEnableClientCheckedChanged);
            // 
            // _clientGroup
            // 
            this._clientGroup.Controls.Add(this._checkBoxClientCmdUi);
            this._clientGroup.Controls.Add(this._editClientPort);
            this._clientGroup.Controls.Add(this._label6);
            this._clientGroup.Controls.Add(this._label8);
            this._clientGroup.Controls.Add(this._editClientHost);
            this._clientGroup.Controls.Add(this._label7);
            this._clientGroup.Controls.Add(this._editClientDelayTime);
            this._clientGroup.Location = new System.Drawing.Point(16, 8);
            this._clientGroup.Name = "_clientGroup";
            this._clientGroup.Size = new System.Drawing.Size(307, 220);
            this._clientGroup.TabIndex = 8;
            this._clientGroup.TabStop = false;
            // 
            // _checkBoxClientCmdUi
            // 
            this._checkBoxClientCmdUi.AutoSize = true;
            this._checkBoxClientCmdUi.Location = new System.Drawing.Point(16, 197);
            this._checkBoxClientCmdUi.Name = "_checkBoxClientCmdUi";
            this._checkBoxClientCmdUi.Size = new System.Drawing.Size(177, 17);
            this._checkBoxClientCmdUi.TabIndex = 4;
            this._checkBoxClientCmdUi.Text = "&Show \"send command\" window";
            this._checkBoxClientCmdUi.UseVisualStyleBackColor = true;
            this._checkBoxClientCmdUi.CheckedChanged += new System.EventHandler(this.CheckBoxClientCmdUiCheckedChanged);
            // 
            // _editClientPort
            // 
            this._editClientPort.Location = new System.Drawing.Point(16, 80);
            this._editClientPort.Name = "_editClientPort";
            this._editClientPort.Size = new System.Drawing.Size(56, 20);
            this._editClientPort.TabIndex = 3;
            this._editClientPort.TextChanged += new System.EventHandler(this.EditClientPortTextChanged);
            // 
            // _label6
            // 
            this._label6.Location = new System.Drawing.Point(16, 64);
            this._label6.Name = "_label6";
            this._label6.Size = new System.Drawing.Size(32, 16);
            this._label6.TabIndex = 2;
            this._label6.Text = "&Port:";
            // 
            // _label8
            // 
            this._label8.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label8.Location = new System.Drawing.Point(16, 24);
            this._label8.Name = "_label8";
            this._label8.Size = new System.Drawing.Size(88, 16);
            this._label8.TabIndex = 0;
            this._label8.Text = "&Host:";
            // 
            // _editClientHost
            // 
            this._editClientHost.Location = new System.Drawing.Point(16, 40);
            this._editClientHost.Name = "_editClientHost";
            this._editClientHost.Size = new System.Drawing.Size(160, 20);
            this._editClientHost.TabIndex = 1;
            this._editClientHost.TextChanged += new System.EventHandler(this.EditClientHostTextChanged);
            // 
            // _label7
            // 
            this._label7.Location = new System.Drawing.Point(16, 104);
            this._label7.Name = "_label7";
            this._label7.Size = new System.Drawing.Size(144, 16);
            this._label7.TabIndex = 2;
            this._label7.Text = "&Reconnect Wait Time:";
            // 
            // _editClientDelayTime
            // 
            this._editClientDelayTime.Location = new System.Drawing.Point(16, 120);
            this._editClientDelayTime.Name = "_editClientDelayTime";
            this._editClientDelayTime.Size = new System.Drawing.Size(56, 20);
            this._editClientDelayTime.TabIndex = 3;
            this._editClientDelayTime.TextChanged += new System.EventHandler(this.EditClientDelayTimeTextChanged);
            // 
            // _tabServer
            // 
            this._tabServer.Controls.Add(this._checkBoxEnableServer);
            this._tabServer.Controls.Add(this._serverGroup);
            this._tabServer.Location = new System.Drawing.Point(4, 22);
            this._tabServer.Name = "_tabServer";
            this._tabServer.Size = new System.Drawing.Size(340, 241);
            this._tabServer.TabIndex = 2;
            this._tabServer.Text = "Server";
            // 
            // _checkBoxEnableServer
            // 
            this._checkBoxEnableServer.Location = new System.Drawing.Point(24, 6);
            this._checkBoxEnableServer.Name = "_checkBoxEnableServer";
            this._checkBoxEnableServer.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableServer.TabIndex = 0;
            this._checkBoxEnableServer.Text = "Enable &Server";
            this._toolTipServer.SetToolTip(this._checkBoxEnableServer, "Enables the TCP/IP server. It will listen on the specified port for commands.");
            this._checkBoxEnableServer.CheckedChanged += new System.EventHandler(this.CheckBoxEnableServerCheckedChanged);
            // 
            // _serverGroup
            // 
            this._serverGroup.Controls.Add(this._editServerPort);
            this._serverGroup.Controls.Add(this._label1);
            this._serverGroup.Controls.Add(this._checkBoxEnableWakeup);
            this._serverGroup.Controls.Add(this._wakeupGroup);
            this._serverGroup.Location = new System.Drawing.Point(16, 8);
            this._serverGroup.Name = "_serverGroup";
            this._serverGroup.Size = new System.Drawing.Size(307, 220);
            this._serverGroup.TabIndex = 6;
            this._serverGroup.TabStop = false;
            // 
            // _editServerPort
            // 
            this._editServerPort.Location = new System.Drawing.Point(48, 24);
            this._editServerPort.Name = "_editServerPort";
            this._editServerPort.Size = new System.Drawing.Size(56, 20);
            this._editServerPort.TabIndex = 1;
            this._editServerPort.TextChanged += new System.EventHandler(this.EditServerPortTextChanged);
            // 
            // _label1
            // 
            this._label1.Location = new System.Drawing.Point(16, 26);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(32, 16);
            this._label1.TabIndex = 0;
            this._label1.Text = "&Port:";
            // 
            // _checkBoxEnableWakeup
            // 
            this._checkBoxEnableWakeup.Location = new System.Drawing.Point(26, 54);
            this._checkBoxEnableWakeup.Name = "_checkBoxEnableWakeup";
            this._checkBoxEnableWakeup.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableWakeup.TabIndex = 2;
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
            this._wakeupGroup.Location = new System.Drawing.Point(16, 56);
            this._wakeupGroup.Name = "_wakeupGroup";
            this._wakeupGroup.Size = new System.Drawing.Size(272, 151);
            this._wakeupGroup.TabIndex = 7;
            this._wakeupGroup.TabStop = false;
            // 
            // _editWakeupServer
            // 
            this._editWakeupServer.Location = new System.Drawing.Point(17, 41);
            this._editWakeupServer.Name = "_editWakeupServer";
            this._editWakeupServer.Size = new System.Drawing.Size(160, 20);
            this._editWakeupServer.TabIndex = 1;
            this._editWakeupServer.TextChanged += new System.EventHandler(this.EditWakeupServerTextChanged);
            // 
            // _editWakeupCommand
            // 
            this._editWakeupCommand.Location = new System.Drawing.Point(17, 80);
            this._editWakeupCommand.Name = "_editWakeupCommand";
            this._editWakeupCommand.Size = new System.Drawing.Size(160, 20);
            this._editWakeupCommand.TabIndex = 5;
            this._editWakeupCommand.TextChanged += new System.EventHandler(this.EditWakeupCommandTextChanged);
            // 
            // _editClosingCommand
            // 
            this._editClosingCommand.Location = new System.Drawing.Point(17, 120);
            this._editClosingCommand.Name = "_editClosingCommand";
            this._editClosingCommand.Size = new System.Drawing.Size(160, 20);
            this._editClosingCommand.TabIndex = 7;
            this._editClosingCommand.TextChanged += new System.EventHandler(this.EditClosingCommandTextChanged);
            // 
            // _editWakeupPort
            // 
            this._editWakeupPort.Location = new System.Drawing.Point(192, 41);
            this._editWakeupPort.Name = "_editWakeupPort";
            this._editWakeupPort.Size = new System.Drawing.Size(56, 20);
            this._editWakeupPort.TabIndex = 3;
            this._editWakeupPort.TextChanged += new System.EventHandler(this.EditWakeupPortTextChanged);
            // 
            // _label5
            // 
            this._label5.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label5.Location = new System.Drawing.Point(17, 104);
            this._label5.Name = "_label5";
            this._label5.Size = new System.Drawing.Size(104, 16);
            this._label5.TabIndex = 6;
            this._label5.Text = "Closing Command:";
            // 
            // _label2
            // 
            this._label2.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label2.Location = new System.Drawing.Point(17, 22);
            this._label2.Name = "_label2";
            this._label2.Size = new System.Drawing.Size(88, 16);
            this._label2.TabIndex = 0;
            this._label2.Text = "Wa&keup Host:";
            // 
            // _label4
            // 
            this._label4.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label4.Location = new System.Drawing.Point(17, 64);
            this._label4.Name = "_label4";
            this._label4.Size = new System.Drawing.Size(104, 16);
            this._label4.TabIndex = 4;
            this._label4.Text = "Wakeup Command:";
            // 
            // _label3
            // 
            this._label3.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label3.Location = new System.Drawing.Point(189, 22);
            this._label3.Name = "_label3";
            this._label3.Size = new System.Drawing.Size(48, 16);
            this._label3.TabIndex = 2;
            this._label3.Text = "P&ort:";
            // 
            // _tabSerialServer
            // 
            this._tabSerialServer.BackColor = System.Drawing.SystemColors.Control;
            this._tabSerialServer.Controls.Add(this._checkBoxEnableSerialServer);
            this._tabSerialServer.Controls.Add(this._serialServerGroup);
            this._tabSerialServer.Location = new System.Drawing.Point(4, 22);
            this._tabSerialServer.Name = "_tabSerialServer";
            this._tabSerialServer.Padding = new System.Windows.Forms.Padding(3);
            this._tabSerialServer.Size = new System.Drawing.Size(340, 241);
            this._tabSerialServer.TabIndex = 3;
            this._tabSerialServer.Text = "Serial Server";
            // 
            // _checkBoxEnableSerialServer
            // 
            this._checkBoxEnableSerialServer.Location = new System.Drawing.Point(24, 6);
            this._checkBoxEnableSerialServer.Name = "_checkBoxEnableSerialServer";
            this._checkBoxEnableSerialServer.Size = new System.Drawing.Size(145, 16);
            this._checkBoxEnableSerialServer.TabIndex = 0;
            this._checkBoxEnableSerialServer.Text = "Enable Serial Server";
            this._checkBoxEnableSerialServer.UseVisualStyleBackColor = true;
            this._checkBoxEnableSerialServer.CheckedChanged += new System.EventHandler(this.CheckBoxEnableSerialServerCheckedChanged);
            // 
            // _serialServerGroup
            // 
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
            this._serialServerGroup.Location = new System.Drawing.Point(16, 8);
            this._serialServerGroup.Name = "_serialServerGroup";
            this._serialServerGroup.Size = new System.Drawing.Size(307, 220);
            this._serialServerGroup.TabIndex = 0;
            this._serialServerGroup.TabStop = false;
            // 
            // _comboBoxHandshake
            // 
            this._comboBoxHandshake.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxHandshake.FormattingEnabled = true;
            this._comboBoxHandshake.Items.AddRange(new object[] {
            "None",
            "Xon / Xoff",
            "Hardware",
            "Both"});
            this._comboBoxHandshake.Location = new System.Drawing.Point(95, 158);
            this._comboBoxHandshake.Name = "_comboBoxHandshake";
            this._comboBoxHandshake.Size = new System.Drawing.Size(116, 21);
            this._comboBoxHandshake.TabIndex = 12;
            this._comboBoxHandshake.SelectedIndexChanged += new System.EventHandler(this.ComboBoxHandshakeSelectedIndexChanged);
            // 
            // _labelHandshake
            // 
            this._labelHandshake.AutoSize = true;
            this._labelHandshake.Location = new System.Drawing.Point(16, 161);
            this._labelHandshake.Name = "_labelHandshake";
            this._labelHandshake.Size = new System.Drawing.Size(65, 13);
            this._labelHandshake.TabIndex = 11;
            this._labelHandshake.Text = "&Handshake:";
            // 
            // _comboBoxStopBits
            // 
            this._comboBoxStopBits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxStopBits.FormattingEnabled = true;
            this._comboBoxStopBits.Items.AddRange(new object[] {
            "1",
            "2",
            "1.5"});
            this._comboBoxStopBits.Location = new System.Drawing.Point(95, 131);
            this._comboBoxStopBits.Name = "_comboBoxStopBits";
            this._comboBoxStopBits.Size = new System.Drawing.Size(116, 21);
            this._comboBoxStopBits.TabIndex = 10;
            this._comboBoxStopBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStopBitsSelectedIndexChanged);
            // 
            // _labelStopBits
            // 
            this._labelStopBits.AutoSize = true;
            this._labelStopBits.Location = new System.Drawing.Point(16, 134);
            this._labelStopBits.Name = "_labelStopBits";
            this._labelStopBits.Size = new System.Drawing.Size(52, 13);
            this._labelStopBits.TabIndex = 9;
            this._labelStopBits.Text = "&Stop Bits:";
            // 
            // _comboBoxParity
            // 
            this._comboBoxParity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxParity.FormattingEnabled = true;
            this._comboBoxParity.Items.AddRange(new object[] {
            "None",
            "Odd",
            "Even",
            "Mark",
            "Space"});
            this._comboBoxParity.Location = new System.Drawing.Point(95, 104);
            this._comboBoxParity.Name = "_comboBoxParity";
            this._comboBoxParity.Size = new System.Drawing.Size(116, 21);
            this._comboBoxParity.TabIndex = 8;
            this._comboBoxParity.SelectedIndexChanged += new System.EventHandler(this.ComboBoxParitySelectedIndexChanged);
            // 
            // _labelParity
            // 
            this._labelParity.AutoSize = true;
            this._labelParity.Location = new System.Drawing.Point(16, 107);
            this._labelParity.Name = "_labelParity";
            this._labelParity.Size = new System.Drawing.Size(36, 13);
            this._labelParity.TabIndex = 7;
            this._labelParity.Text = "&Parity:";
            // 
            // _comboBoxDataBits
            // 
            this._comboBoxDataBits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxDataBits.FormattingEnabled = true;
            this._comboBoxDataBits.Items.AddRange(new object[] {
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
            this._comboBoxDataBits.Location = new System.Drawing.Point(95, 77);
            this._comboBoxDataBits.Name = "_comboBoxDataBits";
            this._comboBoxDataBits.Size = new System.Drawing.Size(116, 21);
            this._comboBoxDataBits.TabIndex = 6;
            this._comboBoxDataBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxDataBitsSelectedIndexChanged);
            // 
            // _labelDataBits
            // 
            this._labelDataBits.AutoSize = true;
            this._labelDataBits.Location = new System.Drawing.Point(16, 80);
            this._labelDataBits.Name = "_labelDataBits";
            this._labelDataBits.Size = new System.Drawing.Size(53, 13);
            this._labelDataBits.TabIndex = 5;
            this._labelDataBits.Text = "&Data Bits:";
            // 
            // _comboBoxBaudRate
            // 
            this._comboBoxBaudRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxBaudRate.FormattingEnabled = true;
            this._comboBoxBaudRate.Items.AddRange(new object[] {
            "2400",
            "4800",
            "9600",
            "19200",
            "38400",
            "57600",
            "115200"});
            this._comboBoxBaudRate.Location = new System.Drawing.Point(95, 50);
            this._comboBoxBaudRate.Name = "_comboBoxBaudRate";
            this._comboBoxBaudRate.Size = new System.Drawing.Size(116, 21);
            this._comboBoxBaudRate.TabIndex = 4;
            this._comboBoxBaudRate.SelectedIndexChanged += new System.EventHandler(this.ComboBoxBaudRateSelectedIndexChanged);
            // 
            // _labelBuadRate
            // 
            this._labelBuadRate.AutoSize = true;
            this._labelBuadRate.Location = new System.Drawing.Point(16, 53);
            this._labelBuadRate.Name = "_labelBuadRate";
            this._labelBuadRate.Size = new System.Drawing.Size(61, 13);
            this._labelBuadRate.TabIndex = 3;
            this._labelBuadRate.Text = "&Baud Rate:";
            // 
            // _comboBoxSerialPort
            // 
            this._comboBoxSerialPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxSerialPort.FormattingEnabled = true;
            this._comboBoxSerialPort.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8"});
            this._comboBoxSerialPort.Location = new System.Drawing.Point(95, 23);
            this._comboBoxSerialPort.Name = "_comboBoxSerialPort";
            this._comboBoxSerialPort.Size = new System.Drawing.Size(116, 21);
            this._comboBoxSerialPort.TabIndex = 2;
            this._comboBoxSerialPort.SelectedIndexChanged += new System.EventHandler(this.ComboBoxSerialPortSelectedIndexChanged);
            // 
            // _labelSerialPort
            // 
            this._labelSerialPort.AutoSize = true;
            this._labelSerialPort.Location = new System.Drawing.Point(16, 26);
            this._labelSerialPort.Name = "_labelSerialPort";
            this._labelSerialPort.Size = new System.Drawing.Size(29, 13);
            this._labelSerialPort.TabIndex = 1;
            this._labelSerialPort.Text = "&Port:";
            // 
            // toolTipClient
            // 
            this.toolTipClient.ToolTipTitle = "Client";
            // 
            // _toolTipServer
            // 
            this._toolTipServer.ToolTipTitle = "Server";
            // 
            // SettingsDialog
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this._buttonCancel;
            this.ClientSize = new System.Drawing.Size(372, 326);
            this.ControlBox = false;
            this.Controls.Add(this._tabControl);
            this.Controls.Add(this._buttonCancel);
            this.Controls.Add(this._buttonOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this._tabControl.ResumeLayout(false);
            this._general.ResumeLayout(false);
            this._tabClient.ResumeLayout(false);
            this._clientGroup.ResumeLayout(false);
            this._clientGroup.PerformLayout();
            this._tabServer.ResumeLayout(false);
            this._serverGroup.ResumeLayout(false);
            this._serverGroup.PerformLayout();
            this._wakeupGroup.ResumeLayout(false);
            this._wakeupGroup.PerformLayout();
            this._tabSerialServer.ResumeLayout(false);
            this._serialServerGroup.ResumeLayout(false);
            this._serialServerGroup.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        public SettingsDialog(AppSettings settings) {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // Clone the settings object
            Settings = (AppSettings) settings.Clone();

            // Handle General tab setup
            _checkBoxHideOnStartup.Checked = Settings.HideOnStartup;
            _checkBoxAutoStart.Checked = Settings.AutoStart;

            // Client tab setup
            _checkBoxEnableClient.Checked = Settings.ActAsClient;
            _editClientPort.Text = Settings.ClientPort.ToString(CultureInfo.InvariantCulture);
            _editClientHost.Text = Settings.ClientHost;
            _editClientDelayTime.Text = Settings.ClientDelayTime.ToString(CultureInfo.InvariantCulture);
            _checkBoxClientCmdUi.Checked = Settings.ShowCommandWindow;

            // Server tab setup
            _checkBoxEnableServer.Checked = Settings.ActAsServer;
            _editServerPort.Text = Settings.ServerPort.ToString(CultureInfo.InvariantCulture);
            _checkBoxEnableWakeup.Checked = Settings.WakeupEnabled;
            _editWakeupServer.Text = Settings.WakeupHost;
            _editWakeupPort.Text = Settings.WakeupPort.ToString(CultureInfo.InvariantCulture);
            _editWakeupCommand.Text = Settings.WakeupCommand;
            _editClosingCommand.Text = Settings.ClosingCommand;

            // Serial Server tab setup
            _checkBoxEnableSerialServer.Checked = Settings.ActAsSerialServer;
            _comboBoxSerialPort.SelectedItem = Settings.SerialServerPortName;
            _comboBoxBaudRate.SelectedItem = Settings.SerialServerBaudRate.ToString();
            _comboBoxDataBits.SelectedItem = Settings.SerialServerDataBits.ToString();
            // For the enum types, we cheat and rely on knowledge of what the enum 
            // values are. The combo boxes are pre-filled with in-order strings.
            _comboBoxParity.SelectedIndex = (int)Settings.SerialServerParity; 
            _comboBoxStopBits.SelectedIndex = (int) Settings.SerialServerStopBits - 1; // None (0) is not allowed
            _comboBoxHandshake.SelectedIndex = (int) Settings.SerialServerHandshake;

            _clientGroup.Enabled = _checkBoxEnableClient.Checked;
            _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
            _serverGroup.Enabled = _checkBoxEnableServer.Checked;
            _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;

            _buttonOk.Enabled = false;
        }

        private void SettingsChanged() {
            if (_checkBoxEnableServer.Checked && _checkBoxEnableWakeup.Checked)
            {
                UInt32 port = 0;
                UInt32.TryParse(_editWakeupPort.Text, out port);
                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editWakeupServer.Text) ||
                                      String.IsNullOrEmpty(_editWakeupCommand.Text) ||
                                      String.IsNullOrEmpty(_editClosingCommand.Text) ||
                                      (port == 0));
                return;
            }

            if (_checkBoxEnableClient.Checked)
            {
                UInt32 port = 0;
                UInt32.TryParse(_editClientPort.Text, out port);
                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editClientHost.Text) ||
                                      (port == 0));
                return;
            }

            _buttonOk.Enabled = true;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
        }

        private void ButtonCancelClick(object sender, EventArgs e) {
            Close();
        }

        private void ButtonOkClick(object sender, EventArgs e) {
            Settings.Serialize();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CheckBoxHideOnStartupCheckedChanged(object sender, EventArgs e) {
            Settings.HideOnStartup = _checkBoxHideOnStartup.Checked;
            SettingsChanged();
        }

        private void CheckBoxAutoStartCheckedChanged(object sender, EventArgs e) {
            Settings.AutoStart = _checkBoxAutoStart.Checked;
            SettingsChanged();
        }

        private void CheckBoxEnableServerCheckedChanged(object sender, EventArgs e) {
            Settings.ActAsServer = _checkBoxEnableServer.Checked;

            _serverGroup.Enabled = _checkBoxEnableServer.Checked;
            SettingsChanged();
        }

        private void EditServerPortTextChanged(object sender, EventArgs e) {
            UInt32 port = 0;
            if (UInt32.TryParse(_editServerPort.Text, out port))
                Settings.ServerPort = (int)port;
            SettingsChanged(); 
        }

        private void CheckBoxEnableWakeupCheckedChanged(object sender, EventArgs e) {
            Settings.WakeupEnabled = _checkBoxEnableWakeup.Checked;
            _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
            SettingsChanged();
        }

        private void EditWakeupServerTextChanged(object sender, EventArgs e) {
            Settings.WakeupHost = _editWakeupServer.Text;
            SettingsChanged();
        }

        private void EditWakeupPortTextChanged(object sender, EventArgs e) {
            UInt32 port = 0;
            if (UInt32.TryParse(_editWakeupPort.Text, out port))
                Settings.WakeupPort = (int)port;
            SettingsChanged();
        }

        private void EditWakeupCommandTextChanged(object sender, EventArgs e) {
            Settings.WakeupCommand = _editWakeupCommand.Text;
            SettingsChanged();
        }

        private void EditClosingCommandTextChanged(object sender, EventArgs e) {
            Settings.ClosingCommand = _editClosingCommand.Text;
            SettingsChanged();
        }

        private void CheckEnableClientCheckedChanged(object sender, EventArgs e) {
            Settings.ActAsClient = _checkBoxEnableClient.Checked;

            _clientGroup.Enabled = _checkBoxEnableClient.Checked;
            SettingsChanged();
        }

        private void EditClientPortTextChanged(object sender, EventArgs e) {
            UInt32 port = 0;
            if (UInt32.TryParse(_editClientPort.Text, out port))
                Settings.ClientPort = (int)port;
            SettingsChanged();
        }

        private void EditClientHostTextChanged(object sender, EventArgs e) {
            Settings.ClientHost = _editClientHost.Text;
            SettingsChanged();
        }

        private void EditClientDelayTimeTextChanged(object sender, EventArgs e) {
            if (_editClientDelayTime.Text.Length > 0)
                Settings.ClientDelayTime = Convert.ToInt32(_editClientDelayTime.Text);
            SettingsChanged();
        }

        // Serial Server handlers
        private void CheckBoxEnableSerialServerCheckedChanged(object sender, EventArgs e)
        {
            Settings.ActAsSerialServer = _checkBoxEnableSerialServer.Checked;

            _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;
            SettingsChanged();
        }

        private void ComboBoxSerialPortSelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxBaudRate.SelectedItem != null) {
                Settings.SerialServerPortName = _comboBoxSerialPort.SelectedItem.ToString();
                SettingsChanged();
            }
        }

        private void ComboBoxBaudRateSelectedIndexChanged(object sender, EventArgs e)
        {
            int baud  = 0;
            if (int.TryParse(_comboBoxBaudRate.SelectedItem.ToString(), out baud))
                Settings.SerialServerBaudRate = baud;
            SettingsChanged();
        }

        private void ComboBoxParitySelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxParity.SelectedItem != null) {
                Settings.SerialServerParity = (Parity) _comboBoxParity.SelectedIndex;
                SettingsChanged();
            }
        }

        private void ComboBoxDataBitsSelectedIndexChanged(object sender, EventArgs e)
        {
            int bits = 0;
            if (int.TryParse(_comboBoxDataBits.SelectedItem.ToString(), out bits))
                Settings.SerialServerDataBits = bits;
            SettingsChanged();
        }

        private void ComboBoxStopBitsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_comboBoxStopBits.SelectedItem != null) {
                // Add one because None is invalid and is not included in the combo box
                Settings.SerialServerStopBits = (StopBits)_comboBoxStopBits.SelectedIndex + 1;
                SettingsChanged();
            }
        }

        private void ComboBoxHandshakeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_comboBoxHandshake.SelectedItem != null) {
                Settings.SerialServerHandshake = (Handshake) _comboBoxHandshake.SelectedIndex;
                SettingsChanged();
            }
        }
        
        private void CheckBoxClientCmdUiCheckedChanged(object sender, EventArgs e)
        {
            Settings.ShowCommandWindow = _checkBoxClientCmdUi.Checked;
            SettingsChanged();
        }
    }

    public class AppSettings : ICloneable {
        private const string SettingsFileName = "MCEContol.settings";

        // TODO: If I were a good programmer these public members would all
        // be properties and I'd keep track of whether something changed
        // within this class.

        // General

        // Client
        public bool ActAsClient;

        // Server
        public bool ActAsServer = true;
        public bool AutoStart;
        public int ClientDelayTime = 30000;
        public String ClientHost;
        public int ClientPort;
        public String ClosingCommand;
        public bool HideOnStartup;
        public int Opacity = 100;
        public int ServerPort = 5150;
        public String WakeupCommand;
        public bool WakeupEnabled;
        public String WakeupHost;
        public int WakeupPort;
        public bool ActAsSerialServer = false;
        public String SerialServerPortName;
        public int SerialServerBaudRate;
        public Parity SerialServerParity;
        public int SerialServerDataBits;
        public StopBits SerialServerStopBits;
        public Handshake SerialServerHandshake;
        public Point WindowLocation = new Point(120, 50);
        public Size WindowSize = new Size(640, 400);
        public bool ShowCommandWindow;

        #region ICloneable Members

        public object Clone() {
            return MemberwiseClone();
        }

        #endregion

        // Must have a default public constructor so XMLSerialization will work
        // This class is NOT supposed to be creatable (use Deserialize to construct).
        public AppSettings() {
            SerialPort defaultPort = new SerialPort();
            SerialServerPortName = defaultPort.PortName;
            SerialServerBaudRate = defaultPort.BaudRate;
            SerialServerParity = defaultPort.Parity;
            SerialServerDataBits = defaultPort.DataBits;
            SerialServerStopBits = defaultPort.StopBits;
            SerialServerHandshake = defaultPort.Handshake;
        }

        // By default we want the settings file stored with the EXE
        // This allows the app to be run with multiple instances with a settings
        // file for each instance (each being in different directory).
        // However, typical installs get put into to %PROGRAMFILES% which 
        // is ACLd to allow only admin writes on Win7. 
        public static String GetSettingsPath() {
            String path = Application.StartupPath;
            // If app was started from within ProgramFiles then use UserAppDataPath.
            if (path.Contains(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))) {
                // Strip off the trailing version ("\\0.0.0.xxxx")
                path = Application.UserAppDataPath.Substring(0,
                                                             Application.UserAppDataPath.Length -
                                                             (Application.ProductVersion.Length + 1));
            }

            return path;
        }

        public void Serialize() {
            var settingsPath = GetSettingsPath();
            try {
                var filePath = settingsPath + "\\" + SettingsFileName;
                var ser = new XmlSerializer(typeof (AppSettings));
                var sw = new StreamWriter(filePath);
                ser.Serialize(sw, this);
                sw.Close();
                MainWindow.AddLogEntry("MCEC: Wrote settings to " + filePath);
            }
            catch (Exception e) {
                MessageBox.Show(String.Format("Settings file could not be written. {0}", e.Message));
            }
        }

        public static AppSettings Deserialize(String settingsPath) {
            AppSettings settings = null;

            var serializer = new XmlSerializer(typeof (AppSettings));
            // A FileStream is needed to read the XML document.
            try {
                var filePath = settingsPath + "\\" + SettingsFileName;
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                XmlReader reader = new XmlTextReader(fs);
                settings = (AppSettings) serializer.Deserialize(reader);
                fs.Close();
                MainWindow.AddLogEntry("MCEC: Read settings from " + filePath);
            }
            catch (FileNotFoundException) {
                // First time through, so create file with defaults
                MainWindow.AddLogEntry("MCEC: Creating default settings file.");
                settings = new AppSettings();
                settings.Serialize();
            }
            catch (UnauthorizedAccessException e) {
                MessageBox.Show(String.Format("Settings file could not be loaded. {0}", e.Message));
            }
            return settings;
        }
    }
}