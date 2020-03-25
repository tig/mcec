//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using MCEControl.Properties;
using MCEControl.Services;
using Microsoft.Win32;
using System.Text.Json;

namespace MCEControl {
    /// <summary>
    /// Settings dialog box
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "IDE0069")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1501", Justification = "WinForms generated", Scope = "namespace")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1505", Justification = "WinForms generated", Scope = "namespace")]
    public class SettingsDialog : Form {
        /// <summary>
        /// Required designer variable.
        /// </summary>

        private GroupBox _clientGroup;

        private TabPage tabGeneral;
        private GroupBox _serverGroup;

        private AppSettings settings;
        public AppSettings Settings { get => settings; set => settings = value; }

        private string defaultTab;
        public string DefaultTab { get => defaultTab; set => defaultTab = value; }

        private GroupBox _wakeupGroup;
        private Button _buttonCancel;
        private Button _buttonOk;
        private CheckBox _checkBoxAutoStart;
        private CheckBox _checkBoxEnableClient;
        private CheckBox _checkBoxEnableServer;
        private CheckBox _checkBoxEnableWakeup;
        private CheckBox _checkBoxHideOnStartup;
        private System.Windows.Forms.TextBox _editClientDelayTime;
        private System.Windows.Forms.TextBox _editClientHost;
        private System.Windows.Forms.TextBox _editClientPort;
        private System.Windows.Forms.TextBox _editClosingCommand;
        private System.Windows.Forms.TextBox _editServerPort;
        private System.Windows.Forms.TextBox _editWakeupCommand;
        private System.Windows.Forms.TextBox _editWakeupPort;
        private System.Windows.Forms.TextBox _editWakeupServer;
        private Label _label1;
        private Label _label2;
        private Label _label3;
        private Label _label4;
        private Label _label5;
        private Label _label6;
        private Label _label7;
        private Label _label8;
        private TabPage tabClient;
        private TabControl tabcontrol;
        private TabPage tabSerial;
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
        private TabPage _tabPageActivityMonitor;
        private GroupBox groupBoxActivityMonitor;
        private CheckBox checkBoxEnableActivityMonitor;
        private System.Windows.Forms.TextBox textBoxActivityCommand;
        private Label labelActivityCommand;
        private Label labelActivityDebounceTime;
        private System.Windows.Forms.TextBox textBoxDebounceTime;
        private EventLog eventLog1;
        private ComboBox comboBoxLogThresholds;
        private Label labelLogLevel;
        private TextBox textBoxPacing;
        private Label labelPacing;
        private TabPage tabServer;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this._buttonCancel = new System.Windows.Forms.Button();
            this._buttonOk = new System.Windows.Forms.Button();
            this.tabcontrol = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.comboBoxLogThresholds = new System.Windows.Forms.ComboBox();
            this.labelLogLevel = new System.Windows.Forms.Label();
            this._checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
            this._checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this.tabClient = new System.Windows.Forms.TabPage();
            this._checkBoxEnableClient = new System.Windows.Forms.CheckBox();
            this._clientGroup = new System.Windows.Forms.GroupBox();
            this._editClientPort = new System.Windows.Forms.TextBox();
            this._label6 = new System.Windows.Forms.Label();
            this._label8 = new System.Windows.Forms.Label();
            this._editClientHost = new System.Windows.Forms.TextBox();
            this._label7 = new System.Windows.Forms.Label();
            this._editClientDelayTime = new System.Windows.Forms.TextBox();
            this.tabServer = new System.Windows.Forms.TabPage();
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
            this.tabSerial = new System.Windows.Forms.TabPage();
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
            this._tabPageActivityMonitor = new System.Windows.Forms.TabPage();
            this.checkBoxEnableActivityMonitor = new System.Windows.Forms.CheckBox();
            this.groupBoxActivityMonitor = new System.Windows.Forms.GroupBox();
            this.labelActivityDebounceTime = new System.Windows.Forms.Label();
            this.textBoxDebounceTime = new System.Windows.Forms.TextBox();
            this.textBoxActivityCommand = new System.Windows.Forms.TextBox();
            this.labelActivityCommand = new System.Windows.Forms.Label();
            this.toolTipClient = new System.Windows.Forms.ToolTip(this.components);
            this._toolTipServer = new System.Windows.Forms.ToolTip(this.components);
            this.eventLog1 = new System.Diagnostics.EventLog();
            this.labelPacing = new System.Windows.Forms.Label();
            this.textBoxPacing = new System.Windows.Forms.TextBox();
            this.tabcontrol.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabClient.SuspendLayout();
            this._clientGroup.SuspendLayout();
            this.tabServer.SuspendLayout();
            this._serverGroup.SuspendLayout();
            this._wakeupGroup.SuspendLayout();
            this.tabSerial.SuspendLayout();
            this._serialServerGroup.SuspendLayout();
            this._tabPageActivityMonitor.SuspendLayout();
            this.groupBoxActivityMonitor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog1)).BeginInit();
            this.SuspendLayout();
            // 
            // _buttonCancel
            // 
            this._buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonCancel.Location = new System.Drawing.Point(768, 554);
            this._buttonCancel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._buttonCancel.Name = "_buttonCancel";
            this._buttonCancel.Size = new System.Drawing.Size(150, 46);
            this._buttonCancel.TabIndex = 2;
            this._buttonCancel.Text = "Cancel";
            this._buttonCancel.UseVisualStyleBackColor = true;
            this._buttonCancel.Click += new System.EventHandler(this.ButtonCancelClick);
            // 
            // _buttonOk
            // 
            this._buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._buttonOk.Location = new System.Drawing.Point(592, 554);
            this._buttonOk.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.Size = new System.Drawing.Size(150, 46);
            this._buttonOk.TabIndex = 1;
            this._buttonOk.Text = "OK";
            this._buttonOk.UseVisualStyleBackColor = true;
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // tabcontrol
            // 
            this.tabcontrol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabcontrol.Controls.Add(this.tabGeneral);
            this.tabcontrol.Controls.Add(this.tabClient);
            this.tabcontrol.Controls.Add(this.tabServer);
            this.tabcontrol.Controls.Add(this.tabSerial);
            this.tabcontrol.Controls.Add(this._tabPageActivityMonitor);
            this.tabcontrol.Location = new System.Drawing.Point(32, 31);
            this.tabcontrol.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabcontrol.Name = "tabcontrol";
            this.tabcontrol.SelectedIndex = 0;
            this.tabcontrol.Size = new System.Drawing.Size(896, 508);
            this.tabcontrol.TabIndex = 0;
            // 
            // tabGeneral
            // 
            this.tabGeneral.BackColor = System.Drawing.SystemColors.Window;
            this.tabGeneral.Controls.Add(this.textBoxPacing);
            this.tabGeneral.Controls.Add(this.labelPacing);
            this.tabGeneral.Controls.Add(this.comboBoxLogThresholds);
            this.tabGeneral.Controls.Add(this.labelLogLevel);
            this.tabGeneral.Controls.Add(this._checkBoxHideOnStartup);
            this.tabGeneral.Controls.Add(this._checkBoxAutoStart);
            this.tabGeneral.Location = new System.Drawing.Point(8, 39);
            this.tabGeneral.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Size = new System.Drawing.Size(880, 461);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            // 
            // comboBoxLogThresholds
            // 
            this.comboBoxLogThresholds.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxLogThresholds.FormattingEnabled = true;
            this.comboBoxLogThresholds.Location = new System.Drawing.Point(32, 98);
            this.comboBoxLogThresholds.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.comboBoxLogThresholds.Name = "comboBoxLogThresholds";
            this.comboBoxLogThresholds.Size = new System.Drawing.Size(238, 33);
            this.comboBoxLogThresholds.TabIndex = 6;
            this.comboBoxLogThresholds.SelectedIndexChanged += new System.EventHandler(this.comboBoxLogThresholds_SelectedIndexChanged);
            // 
            // labelLogLevel
            // 
            this.labelLogLevel.AutoSize = true;
            this.labelLogLevel.Location = new System.Drawing.Point(26, 67);
            this.labelLogLevel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.labelLogLevel.Name = "labelLogLevel";
            this.labelLogLevel.Size = new System.Drawing.Size(156, 25);
            this.labelLogLevel.TabIndex = 5;
            this.labelLogLevel.Text = "Log Threshold:";
            // 
            // _checkBoxHideOnStartup
            // 
            this._checkBoxHideOnStartup.Location = new System.Drawing.Point(32, 15);
            this._checkBoxHideOnStartup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxHideOnStartup.Name = "_checkBoxHideOnStartup";
            this._checkBoxHideOnStartup.Size = new System.Drawing.Size(320, 29);
            this._checkBoxHideOnStartup.TabIndex = 0;
            this._checkBoxHideOnStartup.Text = "&Hide window on startup";
            this._checkBoxHideOnStartup.CheckedChanged += new System.EventHandler(this.CheckBoxHideOnStartupCheckedChanged);
            // 
            // _checkBoxAutoStart
            // 
            this._checkBoxAutoStart.Location = new System.Drawing.Point(64, 400);
            this._checkBoxAutoStart.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxAutoStart.Name = "_checkBoxAutoStart";
            this._checkBoxAutoStart.Size = new System.Drawing.Size(320, 31);
            this._checkBoxAutoStart.TabIndex = 1;
            this._checkBoxAutoStart.Text = "&Automatically start at login";
            this._checkBoxAutoStart.Visible = false;
            this._checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.CheckBoxAutoStartCheckedChanged);
            // 
            // tabClient
            // 
            this.tabClient.BackColor = System.Drawing.SystemColors.Window;
            this.tabClient.Controls.Add(this._checkBoxEnableClient);
            this.tabClient.Controls.Add(this._clientGroup);
            this.tabClient.Location = new System.Drawing.Point(8, 39);
            this.tabClient.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabClient.Name = "tabClient";
            this.tabClient.Size = new System.Drawing.Size(880, 461);
            this.tabClient.TabIndex = 1;
            this.tabClient.Text = "Client";
            // 
            // _checkBoxEnableClient
            // 
            this._checkBoxEnableClient.AutoSize = true;
            this._checkBoxEnableClient.Location = new System.Drawing.Point(40, 19);
            this._checkBoxEnableClient.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxEnableClient.Name = "_checkBoxEnableClient";
            this._checkBoxEnableClient.Size = new System.Drawing.Size(172, 29);
            this._checkBoxEnableClient.TabIndex = 0;
            this._checkBoxEnableClient.Text = "Enable &Client";
            this.toolTipClient.SetToolTip(this._checkBoxEnableClient, "Starts a TCP/IP client connection to the specified address:port. Commands will be" +
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
            this._clientGroup.Location = new System.Drawing.Point(24, 21);
            this._clientGroup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._clientGroup.Name = "_clientGroup";
            this._clientGroup.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._clientGroup.Size = new System.Drawing.Size(824, 425);
            this._clientGroup.TabIndex = 8;
            this._clientGroup.TabStop = false;
            // 
            // _editClientPort
            // 
            this._editClientPort.Location = new System.Drawing.Point(32, 169);
            this._editClientPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editClientPort.Name = "_editClientPort";
            this._editClientPort.Size = new System.Drawing.Size(112, 31);
            this._editClientPort.TabIndex = 3;
            this._editClientPort.TextChanged += new System.EventHandler(this.EditClientPortTextChanged);
            // 
            // _label6
            // 
            this._label6.Location = new System.Drawing.Point(32, 138);
            this._label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label6.Name = "_label6";
            this._label6.Size = new System.Drawing.Size(64, 31);
            this._label6.TabIndex = 2;
            this._label6.Text = "&Port:";
            // 
            // _label8
            // 
            this._label8.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label8.Location = new System.Drawing.Point(32, 60);
            this._label8.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label8.Name = "_label8";
            this._label8.Size = new System.Drawing.Size(176, 31);
            this._label8.TabIndex = 0;
            this._label8.Text = "&Host:";
            // 
            // _editClientHost
            // 
            this._editClientHost.Location = new System.Drawing.Point(32, 92);
            this._editClientHost.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editClientHost.Name = "_editClientHost";
            this._editClientHost.Size = new System.Drawing.Size(320, 31);
            this._editClientHost.TabIndex = 1;
            this._editClientHost.TextChanged += new System.EventHandler(this.EditClientHostTextChanged);
            // 
            // _label7
            // 
            this._label7.Location = new System.Drawing.Point(32, 215);
            this._label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label7.Name = "_label7";
            this._label7.Size = new System.Drawing.Size(288, 31);
            this._label7.TabIndex = 2;
            this._label7.Text = "&Reconnect Wait Time (ms):";
            // 
            // _editClientDelayTime
            // 
            this._editClientDelayTime.Location = new System.Drawing.Point(32, 246);
            this._editClientDelayTime.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editClientDelayTime.Name = "_editClientDelayTime";
            this._editClientDelayTime.Size = new System.Drawing.Size(112, 31);
            this._editClientDelayTime.TabIndex = 3;
            this._editClientDelayTime.TextChanged += new System.EventHandler(this.EditClientDelayTimeTextChanged);
            // 
            // tabServer
            // 
            this.tabServer.BackColor = System.Drawing.SystemColors.Window;
            this.tabServer.Controls.Add(this._checkBoxEnableServer);
            this.tabServer.Controls.Add(this._serverGroup);
            this.tabServer.Location = new System.Drawing.Point(8, 39);
            this.tabServer.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabServer.Name = "tabServer";
            this.tabServer.Size = new System.Drawing.Size(880, 461);
            this.tabServer.TabIndex = 2;
            this.tabServer.Text = "Server";
            // 
            // _checkBoxEnableServer
            // 
            this._checkBoxEnableServer.AutoSize = true;
            this._checkBoxEnableServer.Location = new System.Drawing.Point(40, 19);
            this._checkBoxEnableServer.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxEnableServer.Name = "_checkBoxEnableServer";
            this._checkBoxEnableServer.Size = new System.Drawing.Size(180, 29);
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
            this._serverGroup.Location = new System.Drawing.Point(24, 21);
            this._serverGroup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._serverGroup.Name = "_serverGroup";
            this._serverGroup.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._serverGroup.Size = new System.Drawing.Size(824, 425);
            this._serverGroup.TabIndex = 6;
            this._serverGroup.TabStop = false;
            // 
            // _editServerPort
            // 
            this._editServerPort.Location = new System.Drawing.Point(96, 44);
            this._editServerPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editServerPort.Name = "_editServerPort";
            this._editServerPort.Size = new System.Drawing.Size(112, 31);
            this._editServerPort.TabIndex = 1;
            this._editServerPort.TextChanged += new System.EventHandler(this.EditServerPortTextChanged);
            // 
            // _label1
            // 
            this._label1.Location = new System.Drawing.Point(26, 50);
            this._label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(64, 31);
            this._label1.TabIndex = 0;
            this._label1.Text = "&Port:";
            // 
            // _checkBoxEnableWakeup
            // 
            this._checkBoxEnableWakeup.Location = new System.Drawing.Point(52, 100);
            this._checkBoxEnableWakeup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxEnableWakeup.Name = "_checkBoxEnableWakeup";
            this._checkBoxEnableWakeup.Size = new System.Drawing.Size(208, 29);
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
            this._wakeupGroup.Location = new System.Drawing.Point(32, 102);
            this._wakeupGroup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._wakeupGroup.Name = "_wakeupGroup";
            this._wakeupGroup.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._wakeupGroup.Size = new System.Drawing.Size(768, 298);
            this._wakeupGroup.TabIndex = 7;
            this._wakeupGroup.TabStop = false;
            // 
            // _editWakeupServer
            // 
            this._editWakeupServer.Location = new System.Drawing.Point(34, 77);
            this._editWakeupServer.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editWakeupServer.Name = "_editWakeupServer";
            this._editWakeupServer.Size = new System.Drawing.Size(320, 31);
            this._editWakeupServer.TabIndex = 1;
            this._editWakeupServer.TextChanged += new System.EventHandler(this.EditWakeupServerTextChanged);
            // 
            // _editWakeupCommand
            // 
            this._editWakeupCommand.Location = new System.Drawing.Point(32, 154);
            this._editWakeupCommand.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editWakeupCommand.Name = "_editWakeupCommand";
            this._editWakeupCommand.Size = new System.Drawing.Size(320, 31);
            this._editWakeupCommand.TabIndex = 5;
            this._editWakeupCommand.TextChanged += new System.EventHandler(this.EditWakeupCommandTextChanged);
            // 
            // _editClosingCommand
            // 
            this._editClosingCommand.Location = new System.Drawing.Point(32, 231);
            this._editClosingCommand.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editClosingCommand.Name = "_editClosingCommand";
            this._editClosingCommand.Size = new System.Drawing.Size(320, 31);
            this._editClosingCommand.TabIndex = 7;
            this._editClosingCommand.TextChanged += new System.EventHandler(this.EditClosingCommandTextChanged);
            // 
            // _editWakeupPort
            // 
            this._editWakeupPort.Location = new System.Drawing.Point(384, 77);
            this._editWakeupPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._editWakeupPort.Name = "_editWakeupPort";
            this._editWakeupPort.Size = new System.Drawing.Size(112, 31);
            this._editWakeupPort.TabIndex = 3;
            this._editWakeupPort.TextChanged += new System.EventHandler(this.EditWakeupPortTextChanged);
            // 
            // _label5
            // 
            this._label5.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label5.Location = new System.Drawing.Point(32, 200);
            this._label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label5.Name = "_label5";
            this._label5.Size = new System.Drawing.Size(208, 31);
            this._label5.TabIndex = 6;
            this._label5.Text = "Closing Command:";
            // 
            // _label2
            // 
            this._label2.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label2.Location = new System.Drawing.Point(32, 46);
            this._label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label2.Name = "_label2";
            this._label2.Size = new System.Drawing.Size(176, 29);
            this._label2.TabIndex = 0;
            this._label2.Text = "Wa&keup Host:";
            // 
            // _label4
            // 
            this._label4.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label4.Location = new System.Drawing.Point(32, 123);
            this._label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label4.Name = "_label4";
            this._label4.Size = new System.Drawing.Size(208, 31);
            this._label4.TabIndex = 4;
            this._label4.Text = "Wakeup Command:";
            // 
            // _label3
            // 
            this._label3.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label3.Location = new System.Drawing.Point(378, 42);
            this._label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._label3.Name = "_label3";
            this._label3.Size = new System.Drawing.Size(96, 29);
            this._label3.TabIndex = 2;
            this._label3.Text = "P&ort:";
            // 
            // tabSerial
            // 
            this.tabSerial.BackColor = System.Drawing.SystemColors.Window;
            this.tabSerial.Controls.Add(this._checkBoxEnableSerialServer);
            this.tabSerial.Controls.Add(this._serialServerGroup);
            this.tabSerial.Location = new System.Drawing.Point(8, 39);
            this.tabSerial.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabSerial.Name = "tabSerial";
            this.tabSerial.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabSerial.Size = new System.Drawing.Size(880, 461);
            this.tabSerial.TabIndex = 3;
            this.tabSerial.Text = "Serial Server";
            // 
            // _checkBoxEnableSerialServer
            // 
            this._checkBoxEnableSerialServer.AutoSize = true;
            this._checkBoxEnableSerialServer.Location = new System.Drawing.Point(40, 19);
            this._checkBoxEnableSerialServer.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._checkBoxEnableSerialServer.Name = "_checkBoxEnableSerialServer";
            this._checkBoxEnableSerialServer.Size = new System.Drawing.Size(241, 29);
            this._checkBoxEnableSerialServer.TabIndex = 0;
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
            this._serialServerGroup.Location = new System.Drawing.Point(24, 21);
            this._serialServerGroup.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._serialServerGroup.Name = "_serialServerGroup";
            this._serialServerGroup.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._serialServerGroup.Size = new System.Drawing.Size(824, 425);
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
            this._comboBoxHandshake.Location = new System.Drawing.Point(190, 298);
            this._comboBoxHandshake.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxHandshake.Name = "_comboBoxHandshake";
            this._comboBoxHandshake.Size = new System.Drawing.Size(232, 33);
            this._comboBoxHandshake.TabIndex = 12;
            this._comboBoxHandshake.SelectedIndexChanged += new System.EventHandler(this.ComboBoxHandshakeSelectedIndexChanged);
            // 
            // _labelHandshake
            // 
            this._labelHandshake.AutoSize = true;
            this._labelHandshake.Location = new System.Drawing.Point(32, 306);
            this._labelHandshake.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelHandshake.Name = "_labelHandshake";
            this._labelHandshake.Size = new System.Drawing.Size(127, 25);
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
            this._comboBoxStopBits.Location = new System.Drawing.Point(190, 248);
            this._comboBoxStopBits.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxStopBits.Name = "_comboBoxStopBits";
            this._comboBoxStopBits.Size = new System.Drawing.Size(232, 33);
            this._comboBoxStopBits.TabIndex = 10;
            this._comboBoxStopBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxStopBitsSelectedIndexChanged);
            // 
            // _labelStopBits
            // 
            this._labelStopBits.AutoSize = true;
            this._labelStopBits.Location = new System.Drawing.Point(32, 256);
            this._labelStopBits.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelStopBits.Name = "_labelStopBits";
            this._labelStopBits.Size = new System.Drawing.Size(104, 25);
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
            this._comboBoxParity.Location = new System.Drawing.Point(190, 198);
            this._comboBoxParity.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxParity.Name = "_comboBoxParity";
            this._comboBoxParity.Size = new System.Drawing.Size(232, 33);
            this._comboBoxParity.TabIndex = 8;
            this._comboBoxParity.SelectedIndexChanged += new System.EventHandler(this.ComboBoxParitySelectedIndexChanged);
            // 
            // _labelParity
            // 
            this._labelParity.AutoSize = true;
            this._labelParity.Location = new System.Drawing.Point(32, 206);
            this._labelParity.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelParity.Name = "_labelParity";
            this._labelParity.Size = new System.Drawing.Size(73, 25);
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
            this._comboBoxDataBits.Location = new System.Drawing.Point(190, 150);
            this._comboBoxDataBits.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxDataBits.Name = "_comboBoxDataBits";
            this._comboBoxDataBits.Size = new System.Drawing.Size(232, 33);
            this._comboBoxDataBits.TabIndex = 6;
            this._comboBoxDataBits.SelectedIndexChanged += new System.EventHandler(this.ComboBoxDataBitsSelectedIndexChanged);
            // 
            // _labelDataBits
            // 
            this._labelDataBits.AutoSize = true;
            this._labelDataBits.Location = new System.Drawing.Point(32, 156);
            this._labelDataBits.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelDataBits.Name = "_labelDataBits";
            this._labelDataBits.Size = new System.Drawing.Size(105, 25);
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
            this._comboBoxBaudRate.Location = new System.Drawing.Point(190, 100);
            this._comboBoxBaudRate.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxBaudRate.Name = "_comboBoxBaudRate";
            this._comboBoxBaudRate.Size = new System.Drawing.Size(232, 33);
            this._comboBoxBaudRate.TabIndex = 4;
            this._comboBoxBaudRate.SelectedIndexChanged += new System.EventHandler(this.ComboBoxBaudRateSelectedIndexChanged);
            // 
            // _labelBuadRate
            // 
            this._labelBuadRate.AutoSize = true;
            this._labelBuadRate.Location = new System.Drawing.Point(32, 106);
            this._labelBuadRate.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelBuadRate.Name = "_labelBuadRate";
            this._labelBuadRate.Size = new System.Drawing.Size(119, 25);
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
            this._comboBoxSerialPort.Location = new System.Drawing.Point(190, 50);
            this._comboBoxSerialPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._comboBoxSerialPort.Name = "_comboBoxSerialPort";
            this._comboBoxSerialPort.Size = new System.Drawing.Size(232, 33);
            this._comboBoxSerialPort.TabIndex = 2;
            this._comboBoxSerialPort.SelectedIndexChanged += new System.EventHandler(this.ComboBoxSerialPortSelectedIndexChanged);
            // 
            // _labelSerialPort
            // 
            this._labelSerialPort.AutoSize = true;
            this._labelSerialPort.Location = new System.Drawing.Point(32, 62);
            this._labelSerialPort.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelSerialPort.Name = "_labelSerialPort";
            this._labelSerialPort.Size = new System.Drawing.Size(57, 25);
            this._labelSerialPort.TabIndex = 1;
            this._labelSerialPort.Text = "&Port:";
            // 
            // _tabPageActivityMonitor
            // 
            this._tabPageActivityMonitor.Controls.Add(this.checkBoxEnableActivityMonitor);
            this._tabPageActivityMonitor.Controls.Add(this.groupBoxActivityMonitor);
            this._tabPageActivityMonitor.Location = new System.Drawing.Point(8, 39);
            this._tabPageActivityMonitor.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this._tabPageActivityMonitor.Name = "_tabPageActivityMonitor";
            this._tabPageActivityMonitor.Size = new System.Drawing.Size(880, 461);
            this._tabPageActivityMonitor.TabIndex = 4;
            this._tabPageActivityMonitor.Text = "Activity Monitor";
            this._tabPageActivityMonitor.UseVisualStyleBackColor = true;
            // 
            // checkBoxEnableActivityMonitor
            // 
            this.checkBoxEnableActivityMonitor.AutoSize = true;
            this.checkBoxEnableActivityMonitor.Location = new System.Drawing.Point(40, 19);
            this.checkBoxEnableActivityMonitor.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.checkBoxEnableActivityMonitor.Name = "checkBoxEnableActivityMonitor";
            this.checkBoxEnableActivityMonitor.Size = new System.Drawing.Size(315, 29);
            this.checkBoxEnableActivityMonitor.TabIndex = 0;
            this.checkBoxEnableActivityMonitor.Text = "Enable &User Activity Monitor";
            this.checkBoxEnableActivityMonitor.UseVisualStyleBackColor = true;
            this.checkBoxEnableActivityMonitor.CheckedChanged += new System.EventHandler(this.checkBoxEnableActivityMonitor_CheckedChanged);
            // 
            // groupBoxActivityMonitor
            // 
            this.groupBoxActivityMonitor.Controls.Add(this.labelActivityDebounceTime);
            this.groupBoxActivityMonitor.Controls.Add(this.textBoxDebounceTime);
            this.groupBoxActivityMonitor.Controls.Add(this.textBoxActivityCommand);
            this.groupBoxActivityMonitor.Controls.Add(this.labelActivityCommand);
            this.groupBoxActivityMonitor.Location = new System.Drawing.Point(24, 21);
            this.groupBoxActivityMonitor.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxActivityMonitor.Name = "groupBoxActivityMonitor";
            this.groupBoxActivityMonitor.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxActivityMonitor.Size = new System.Drawing.Size(824, 425);
            this.groupBoxActivityMonitor.TabIndex = 0;
            this.groupBoxActivityMonitor.TabStop = false;
            // 
            // labelActivityDebounceTime
            // 
            this.labelActivityDebounceTime.AutoSize = true;
            this.labelActivityDebounceTime.Location = new System.Drawing.Point(32, 154);
            this.labelActivityDebounceTime.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelActivityDebounceTime.Name = "labelActivityDebounceTime";
            this.labelActivityDebounceTime.Size = new System.Drawing.Size(263, 25);
            this.labelActivityDebounceTime.TabIndex = 3;
            this.labelActivityDebounceTime.Text = "Debounce time (seconds):";
            // 
            // textBoxDebounceTime
            // 
            this.textBoxDebounceTime.Location = new System.Drawing.Point(32, 185);
            this.textBoxDebounceTime.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxDebounceTime.Name = "textBoxDebounceTime";
            this.textBoxDebounceTime.Size = new System.Drawing.Size(86, 31);
            this.textBoxDebounceTime.TabIndex = 2;
            this.textBoxDebounceTime.TextChanged += new System.EventHandler(this.textBoxDebounceTime_TextChanged);
            // 
            // textBoxActivityCommand
            // 
            this.textBoxActivityCommand.Location = new System.Drawing.Point(32, 92);
            this.textBoxActivityCommand.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxActivityCommand.Name = "textBoxActivityCommand";
            this.textBoxActivityCommand.Size = new System.Drawing.Size(294, 31);
            this.textBoxActivityCommand.TabIndex = 2;
            this.textBoxActivityCommand.TextChanged += new System.EventHandler(this.textBoxActivityCommand_TextChanged);
            // 
            // labelActivityCommand
            // 
            this.labelActivityCommand.AutoSize = true;
            this.labelActivityCommand.Location = new System.Drawing.Point(32, 62);
            this.labelActivityCommand.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelActivityCommand.Name = "labelActivityCommand";
            this.labelActivityCommand.Size = new System.Drawing.Size(192, 25);
            this.labelActivityCommand.TabIndex = 1;
            this.labelActivityCommand.Text = "Command to send:";
            // 
            // toolTipClient
            // 
            this.toolTipClient.ToolTipTitle = "Client";
            // 
            // _toolTipServer
            // 
            this._toolTipServer.ToolTipTitle = "Server";
            // 
            // eventLog1
            // 
            this.eventLog1.SynchronizingObject = this;
            // 
            // labelPacing
            // 
            this.labelPacing.AutoSize = true;
            this.labelPacing.Location = new System.Drawing.Point(32, 184);
            this.labelPacing.Name = "labelPacing";
            this.labelPacing.Size = new System.Drawing.Size(303, 25);
            this.labelPacing.TabIndex = 7;
            this.labelPacing.Text = "Default command &pacing (ms):";
            // 
            // textBoxPacing
            // 
            this.textBoxPacing.Location = new System.Drawing.Point(336, 184);
            this.textBoxPacing.Name = "textBoxPacing";
            this.textBoxPacing.Size = new System.Drawing.Size(144, 31);
            this.textBoxPacing.TabIndex = 8;
            this.textBoxPacing.TextChanged += new System.EventHandler(this.textBoxPacing_TextChanged);
            // 
            // SettingsDialog
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.CancelButton = this._buttonCancel;
            this.ClientSize = new System.Drawing.Size(950, 617);
            this.Controls.Add(this.tabcontrol);
            this.Controls.Add(this._buttonCancel);
            this.Controls.Add(this._buttonOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this.Load += new System.EventHandler(this.SettingsDialog_Load);
            this.tabcontrol.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabGeneral.PerformLayout();
            this.tabClient.ResumeLayout(false);
            this.tabClient.PerformLayout();
            this._clientGroup.ResumeLayout(false);
            this._clientGroup.PerformLayout();
            this.tabServer.ResumeLayout(false);
            this.tabServer.PerformLayout();
            this._serverGroup.ResumeLayout(false);
            this._serverGroup.PerformLayout();
            this._wakeupGroup.ResumeLayout(false);
            this._wakeupGroup.PerformLayout();
            this.tabSerial.ResumeLayout(false);
            this.tabSerial.PerformLayout();
            this._serialServerGroup.ResumeLayout(false);
            this._serialServerGroup.PerformLayout();
            this._tabPageActivityMonitor.ResumeLayout(false);
            this._tabPageActivityMonitor.PerformLayout();
            this.groupBoxActivityMonitor.ResumeLayout(false);
            this.groupBoxActivityMonitor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public SettingsDialog(AppSettings settings) {
            if (settings is null) {
                throw new ArgumentNullException(nameof(settings));
            }
            //
            // Required for Windows Form Designer support
            //
            // https://www.sgrottel.de/?p=1581&lang=en
            Font = SystemFonts.DefaultFont;
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
            _comboBoxBaudRate.SelectedItem = $"{Settings.SerialServerBaudRate}";
            _comboBoxDataBits.SelectedItem = $"{Settings.SerialServerDataBits}";
            // For the enum types, we cheat and rely on knowledge of what the enum 
            // values are. The combo boxes are pre-filled with in-order strings.
            _comboBoxParity.SelectedIndex = (int) Settings.SerialServerParity;
            _comboBoxStopBits.SelectedIndex = (int) Settings.SerialServerStopBits - 1; // None (0) is not allowed
            _comboBoxHandshake.SelectedIndex = (int) Settings.SerialServerHandshake;

            _clientGroup.Enabled = _checkBoxEnableClient.Checked;
            _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
            _serverGroup.Enabled = _checkBoxEnableServer.Checked;
            _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;

            
            groupBoxActivityMonitor.Enabled = checkBoxEnableActivityMonitor.Checked = Settings.ActivityMonitorEnabled;
            textBoxActivityCommand.Text = Settings.ActivityMonitorCommand;
            textBoxDebounceTime.Text = $"{Settings.ActivityMonitorDebounceTime}";

            comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["ALL"]);
            comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["INFO"]);
            comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["DEBUG"]);

            switch (Settings.TextBoxLogThreshold) {
                case "ALL":
                    comboBoxLogThresholds.SelectedIndex = 0;
                    break;

                case "INFO":
                    comboBoxLogThresholds.SelectedIndex = 1;
                    break;

                case "DEBUG":
                    comboBoxLogThresholds.SelectedIndex = 2;
                    break;
            }

            textBoxPacing.Text = $"{Settings.CommandPacing}";

            //comboBoxLogThresholds.SelectedIndex = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["ALL"].Value;

            _buttonOk.Enabled = false;
        }

        private void SettingsChanged() {
            if (_checkBoxEnableServer.Checked && _checkBoxEnableWakeup.Checked) {
                if (!int.TryParse(_editWakeupPort.Text, out var port)) port = 0;
                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editWakeupServer.Text) ||
                                      String.IsNullOrEmpty(_editWakeupCommand.Text) ||
                                      String.IsNullOrEmpty(_editClosingCommand.Text) ||
                                      (port == 0));
                return;
            }

            if (_checkBoxEnableClient.Checked) {
                if (!int.TryParse(_editClientPort.Text, out var port)) port = 0;
                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editClientHost.Text) ||
                                      (port == 0));
                return;
            }

            if (checkBoxEnableActivityMonitor.Checked) {
                if (!int.TryParse(textBoxDebounceTime.Text, out var t)) t = 0;
                _buttonOk.Enabled = !(String.IsNullOrEmpty(textBoxActivityCommand.Text) || 
                                    String.IsNullOrEmpty(textBoxDebounceTime.Text) || 
                                    (t == 0));
                return;
            }

            _buttonOk.Enabled = true;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (components != null) {
                components.Dispose();
            }
            base.Dispose(disposing);
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
            if (int.TryParse(_editServerPort.Text, out var port))
                Settings.ServerPort = port;
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
            if (int.TryParse(_editWakeupPort.Text, out var port))
                Settings.WakeupPort = port;
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
            if (int.TryParse(_editClientPort.Text, out var port))
                Settings.ClientPort = port;
            SettingsChanged();
        }

        private void EditClientHostTextChanged(object sender, EventArgs e) {
            Settings.ClientHost = _editClientHost.Text;
            SettingsChanged();
        }

        private void EditClientDelayTimeTextChanged(object sender, EventArgs e) {
            if (_editClientDelayTime.Text.Length > 0)
                Settings.ClientDelayTime = Convert.ToInt32(_editClientDelayTime.Text, new NumberFormatInfo());
            SettingsChanged();
        }

        // Serial Server handlers
        private void CheckBoxEnableSerialServerCheckedChanged(object sender, EventArgs e) {
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

        private void ComboBoxBaudRateSelectedIndexChanged(object sender, EventArgs e) {
            if (int.TryParse(_comboBoxBaudRate.SelectedItem.ToString(), out var baud))
                Settings.SerialServerBaudRate = baud;
            SettingsChanged();
        }

        private void ComboBoxParitySelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxParity.SelectedItem != null) {
                Settings.SerialServerParity = (Parity) _comboBoxParity.SelectedIndex;
                SettingsChanged();
            }
        }

        private void ComboBoxDataBitsSelectedIndexChanged(object sender, EventArgs e) {
            if (int.TryParse(_comboBoxDataBits.SelectedItem.ToString(), out var bits))
                Settings.SerialServerDataBits = bits;
            SettingsChanged();
        }

        private void ComboBoxStopBitsSelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxStopBits.SelectedItem != null) {
                // Add one because None is invalid and is not included in the combo box
                Settings.SerialServerStopBits = (StopBits) _comboBoxStopBits.SelectedIndex + 1;
                SettingsChanged();
            }
        }

        private void ComboBoxHandshakeSelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxHandshake.SelectedItem != null) {
                Settings.SerialServerHandshake = (Handshake) _comboBoxHandshake.SelectedIndex;
                SettingsChanged();
            }
        }

        private void checkBoxEnableActivityMonitor_CheckedChanged(object sender, EventArgs e) {
            Settings.ActivityMonitorEnabled = checkBoxEnableActivityMonitor.Checked;
            groupBoxActivityMonitor.Enabled = checkBoxEnableActivityMonitor.Checked;
            SettingsChanged();
        }

        private void textBoxActivityCommand_TextChanged(object sender, EventArgs e) {
            if (textBoxActivityCommand.Text.Length > 0)
                Settings.ActivityMonitorCommand = textBoxActivityCommand.Text;
            SettingsChanged();
        }

        private void textBoxDebounceTime_TextChanged(object sender, EventArgs e) {
            if (int.TryParse(textBoxDebounceTime.Text, out var t))
                Settings.ActivityMonitorDebounceTime = t;
            SettingsChanged();
        }
        private void SettingsDialog_Load(object sender, EventArgs e) {
            switch (defaultTab) {
                case "General":
                    tabcontrol.SelectedTab = tabGeneral;
                    break;

                case "Client":
                    tabcontrol.SelectedTab = tabClient;
                    break;

                case "Server":
                    tabcontrol.SelectedTab = tabServer;
                    break;

                case "Serial":
                    tabcontrol.SelectedTab = tabSerial;
                    break;

            }
        }

        private void comboBoxLogThresholds_SelectedIndexChanged(object sender, EventArgs e) {
            Settings.TextBoxLogThreshold = comboBoxLogThresholds.SelectedItem.ToString();
            SettingsChanged();
        }

        private void textBoxPacing_TextChanged(object sender, EventArgs e) {
            if (int.TryParse(textBoxPacing.Text, out var t))
                Settings.CommandPacing = t;
            SettingsChanged();
        }
    }


    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class SafeForTelemetryAttribute : System.Attribute {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This is just settings info.")]
    public class AppSettings : ICloneable {
        public const string SettingsFileName = "MCEControl.settings";

        // General
        private bool autoStart;
        private bool hideOnStartup;
        private string textBoxLogThreshold = "INFO";

        // Global
        [XmlIgnore] public bool DisableInternalCommands;

        // Client
        private bool actAsClient;

        // Server
        private bool actAsServer = true;
        private int clientDelayTime = 30000;
        private String clientHost = "localhost";
        private int clientPort = 5150;
        private String closingCommand;
        private int opacity = 100;
        private int serverPort = 5150;
        private String wakeupCommand;
        private bool wakeupEnabled;
        private String wakeupHost;
        private int wakeupPort;
        private bool actAsSerialServer = false;
        private String serialServerPortName;
        private int serialServerBaudRate;
        private Parity serialServerParity;
        private int serialServerDataBits;
        private StopBits serialServerStopBits;
        private Handshake serialServerHandshake;
        private Point windowLocation;
        private Size windowSize;
        private bool showCommandWindow;
        private bool activityMonitorEnabled = false;
        private string activityMonitorCommand = "activity";
        private Int32 activityMonitorDebounceTime = 10;
        private int commandPacing = 0;

        [SafeForTelemetryAttribute] 
        public bool AutoStart { get => autoStart; set => autoStart = value; }
        [SafeForTelemetryAttribute] 
        public bool HideOnStartup { get => hideOnStartup; set => hideOnStartup = value; }
        [SafeForTelemetryAttribute] 
        public string TextBoxLogThreshold { get => textBoxLogThreshold; set => textBoxLogThreshold = value; }
        [SafeForTelemetryAttribute] 
        public bool ActAsClient { get => actAsClient; set => actAsClient = value; }
        [SafeForTelemetryAttribute] 
        public bool ActAsServer { get => actAsServer; set => actAsServer = value; }
        [SafeForTelemetryAttribute] 
        public int ClientDelayTime { get => clientDelayTime; set => clientDelayTime = value; }
        [SafeForTelemetryAttribute] 
        public string ClientHost { get => clientHost; set => clientHost = value; }
        [SafeForTelemetryAttribute] 
        public int ClientPort { get => clientPort; set => clientPort = value; }
        [SafeForTelemetryAttribute]
        public string ClosingCommand { get => closingCommand; set => closingCommand = value; }
        [SafeForTelemetryAttribute] 
        public int Opacity { get => opacity; set => opacity = value; }
        [SafeForTelemetryAttribute] 
        public int ServerPort { get => serverPort; set => serverPort = value; }
        [SafeForTelemetryAttribute] 
        public string WakeupCommand { get => wakeupCommand; set => wakeupCommand = value; }
        [SafeForTelemetryAttribute] 
        public bool WakeupEnabled { get => wakeupEnabled; set => wakeupEnabled = value; }
        [SafeForTelemetryAttribute] 
        public string WakeupHost { get => wakeupHost; set => wakeupHost = value; }
        [SafeForTelemetryAttribute] 
        public int WakeupPort { get => wakeupPort; set => wakeupPort = value; }
        [SafeForTelemetryAttribute] 
        public bool ActAsSerialServer { get => actAsSerialServer; set => actAsSerialServer = value; }
        [SafeForTelemetryAttribute] 
        public string SerialServerPortName { get => serialServerPortName; set => serialServerPortName = value; }
        [SafeForTelemetryAttribute] 
        public int SerialServerBaudRate { get => serialServerBaudRate; set => serialServerBaudRate = value; }
        [SafeForTelemetryAttribute] 
        public Parity SerialServerParity { get => serialServerParity; set => serialServerParity = value; }
        [SafeForTelemetryAttribute] 
        public int SerialServerDataBits { get => serialServerDataBits; set => serialServerDataBits = value; }
        [SafeForTelemetryAttribute] 
        public StopBits SerialServerStopBits { get => serialServerStopBits; set => serialServerStopBits = value; }
        [SafeForTelemetryAttribute] 
        public Handshake SerialServerHandshake { get => serialServerHandshake; set => serialServerHandshake = value; }
        [SafeForTelemetryAttribute] 
        public Point WindowLocation { get => windowLocation; set => windowLocation = value; }
        [SafeForTelemetryAttribute] 
        public Size WindowSize { get => windowSize; set => windowSize = value; }
        [SafeForTelemetryAttribute] 
        public bool ShowCommandWindow { get => showCommandWindow; set => showCommandWindow = value; }
        [SafeForTelemetryAttribute] 
        public bool ActivityMonitorEnabled { get => activityMonitorEnabled; set => activityMonitorEnabled = value; }
        [SafeForTelemetryAttribute] 
        public string ActivityMonitorCommand { get => activityMonitorCommand; set => activityMonitorCommand = value; }
        [SafeForTelemetryAttribute]
        public int ActivityMonitorDebounceTime { get => activityMonitorDebounceTime; set => activityMonitorDebounceTime = value; }
        [SafeForTelemetryAttribute] 
        public int CommandPacing { get => commandPacing; set => commandPacing = value; }


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
            defaultPort.Dispose();
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
            var filePath = settingsPath + "\\" + SettingsFileName;
            try {
                var ser = new XmlSerializer(typeof (AppSettings));
                var sw = new StreamWriter(filePath);
                ser.Serialize(sw, this);
                sw.Close();

                Logger.Instance.Log4.Info("Settings: Wrote settings to " + filePath);
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"Settings: Settings file could not be written. {filePath} {e.Message}");
                MessageBox.Show($"Settings file could not be written. {filePath} {e.Message}");
            }
        }

        public static AppSettings Deserialize(String settingsFile) {
            AppSettings settings = null;

            var serializer = new XmlSerializer(typeof (AppSettings));
            // A FileStream is needed to read the XML document.
            FileStream fs = null;
            XmlReader reader = null;
            try {
                fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
                reader = new XmlTextReader(fs);
                settings = (AppSettings) serializer.Deserialize(reader);

                settings.DisableInternalCommands = Convert.ToBoolean(
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller",
                                    "DisableInternalCommands", false), new NumberFormatInfo());
                Logger.Instance.Log4.Info("Settings: Loaded settings from " + settingsFile);
            }
            catch (FileNotFoundException) {
                // First time through, so create file with defaults
                Logger.Instance.Log4.Info("Settings: Creating default settings file.");
                settings = new AppSettings();
                settings.Serialize();

                // even if it's first run, read global commands
                settings.DisableInternalCommands = Convert.ToBoolean(
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller",
                                    "DisableInternalCommands", false), new NumberFormatInfo());
            }
            catch (UnauthorizedAccessException e) {
                Logger.Instance.Log4.Info($"Settings: Settings file could not be loaded. {e.Message}");
                MessageBox.Show($"Settings file could not be loaded. {e.Message}");
            }
            finally {
                if (reader != null) reader.Dispose();
                if (fs != null) fs.Dispose();
            }

            TelemetryService.Instance.TrackEvent("Settings", settings.GetTelemetryDictionary());

            return settings;
        }

        public virtual IDictionary<string, string> GetTelemetryDictionary() {
            var dictionary = new Dictionary<string, string>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(this)) {
                if (property.Attributes.Contains(new SafeForTelemetryAttribute())) {
                    object value = property.GetValue(this);
                    if (value != null) {
                        if (property.PropertyType.IsSubclassOf(typeof(AppSettings))) {
                            // Go deep
                            var propDict = ((AppSettings)value).GetTelemetryDictionary();
                            dictionary.Add(property.Name, JsonSerializer.Serialize(propDict, propDict.GetType()));
                        }
                        else
                            dictionary.Add(property.Name, JsonSerializer.Serialize(value, value.GetType()));
                    }
                }
            }
            return dictionary;
        }
    }
}
