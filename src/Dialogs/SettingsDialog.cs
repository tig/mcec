// Copyright © Kindel Systems, LLC - http://www.kindel.com - charlie@kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Forms;
using log4net;

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

        private TabPage _tabGeneral;
        private GroupBox _serverGroup;

        public AppSettings Settings { get; set; }
        public string DefaultTab { get; set; }

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
        private TabControl _tabcontrol;
        private TabPage _tabSerial;
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
        private ToolTip _toolTipClient;
        private System.ComponentModel.IContainer _components;
        private ToolTip _toolTipServer;
        private TabPage _tabPageActivityMonitor;
        private GroupBox _groupBoxActivityMonitor;
        private CheckBox _checkBoxEnableActivityMonitor;
        private System.Windows.Forms.TextBox _textBoxActivityCommand;
        private Label _labelActivityCommand;
        private Label _labelActivityDebounceTime;
        private System.Windows.Forms.TextBox _textBoxDebounceTime;
        private EventLog _eventLog;
        private ComboBox _comboBoxLogThresholds;
        private Label _labelLogLevel;
        private TextBox _textBoxPacing;
        private Label _labelPacing;
        private CheckBox _unlockDetection;
        private CheckBox _inputDetection;
        private TabPage _tabServer;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._components = new System.ComponentModel.Container();
            this._buttonCancel = new System.Windows.Forms.Button();
            this._buttonOk = new System.Windows.Forms.Button();
            this._tabcontrol = new System.Windows.Forms.TabControl();
            this._tabGeneral = new System.Windows.Forms.TabPage();
            this._textBoxPacing = new System.Windows.Forms.TextBox();
            this._labelPacing = new System.Windows.Forms.Label();
            this._comboBoxLogThresholds = new System.Windows.Forms.ComboBox();
            this._labelLogLevel = new System.Windows.Forms.Label();
            this._checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
            this._checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this._tabClient = new System.Windows.Forms.TabPage();
            this._checkBoxEnableClient = new System.Windows.Forms.CheckBox();
            this._clientGroup = new System.Windows.Forms.GroupBox();
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
            this._tabSerial = new System.Windows.Forms.TabPage();
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
            this._checkBoxEnableActivityMonitor = new System.Windows.Forms.CheckBox();
            this._groupBoxActivityMonitor = new System.Windows.Forms.GroupBox();
            this._unlockDetection = new System.Windows.Forms.CheckBox();
            this._inputDetection = new System.Windows.Forms.CheckBox();
            this._labelActivityDebounceTime = new System.Windows.Forms.Label();
            this._textBoxDebounceTime = new System.Windows.Forms.TextBox();
            this._textBoxActivityCommand = new System.Windows.Forms.TextBox();
            this._labelActivityCommand = new System.Windows.Forms.Label();
            this._toolTipClient = new System.Windows.Forms.ToolTip(this._components);
            this._toolTipServer = new System.Windows.Forms.ToolTip(this._components);
            this._eventLog = new System.Diagnostics.EventLog();
            this._tabcontrol.SuspendLayout();
            this._tabGeneral.SuspendLayout();
            this._tabClient.SuspendLayout();
            this._clientGroup.SuspendLayout();
            this._tabServer.SuspendLayout();
            this._serverGroup.SuspendLayout();
            this._wakeupGroup.SuspendLayout();
            this._tabSerial.SuspendLayout();
            this._serialServerGroup.SuspendLayout();
            this._tabPageActivityMonitor.SuspendLayout();
            this._groupBoxActivityMonitor.SuspendLayout();
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
            // tabcontrol
            // 
            this._tabcontrol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._tabcontrol.Controls.Add(this._tabGeneral);
            this._tabcontrol.Controls.Add(this._tabClient);
            this._tabcontrol.Controls.Add(this._tabServer);
            this._tabcontrol.Controls.Add(this._tabSerial);
            this._tabcontrol.Controls.Add(this._tabPageActivityMonitor);
            this._tabcontrol.Location = new System.Drawing.Point(16, 16);
            this._tabcontrol.Margin = new System.Windows.Forms.Padding(1);
            this._tabcontrol.Name = "tabcontrol";
            this._tabcontrol.SelectedIndex = 0;
            this._tabcontrol.Size = new System.Drawing.Size(448, 264);
            this._tabcontrol.TabIndex = 0;
            // 
            // tabGeneral
            // 
            this._tabGeneral.BackColor = System.Drawing.SystemColors.Window;
            this._tabGeneral.Controls.Add(this._textBoxPacing);
            this._tabGeneral.Controls.Add(this._labelPacing);
            this._tabGeneral.Controls.Add(this._comboBoxLogThresholds);
            this._tabGeneral.Controls.Add(this._labelLogLevel);
            this._tabGeneral.Controls.Add(this._checkBoxHideOnStartup);
            this._tabGeneral.Controls.Add(this._checkBoxAutoStart);
            this._tabGeneral.Location = new System.Drawing.Point(4, 22);
            this._tabGeneral.Margin = new System.Windows.Forms.Padding(1);
            this._tabGeneral.Name = "tabGeneral";
            this._tabGeneral.Size = new System.Drawing.Size(440, 238);
            this._tabGeneral.TabIndex = 0;
            this._tabGeneral.Text = "General";
            // 
            // textBoxPacing
            // 
            this._textBoxPacing.Location = new System.Drawing.Point(168, 96);
            this._textBoxPacing.Margin = new System.Windows.Forms.Padding(2);
            this._textBoxPacing.Name = "textBoxPacing";
            this._textBoxPacing.Size = new System.Drawing.Size(74, 20);
            this._textBoxPacing.TabIndex = 4;
            this._textBoxPacing.TextChanged += new System.EventHandler(this.textBoxPacing_TextChanged);
            // 
            // labelPacing
            // 
            this._labelPacing.AutoSize = true;
            this._labelPacing.Location = new System.Drawing.Point(16, 96);
            this._labelPacing.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this._labelPacing.Name = "labelPacing";
            this._labelPacing.Size = new System.Drawing.Size(150, 13);
            this._labelPacing.TabIndex = 3;
            this._labelPacing.Text = "Default command &pacing (ms):";
            // 
            // comboBoxLogThresholds
            // 
            this._comboBoxLogThresholds.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._comboBoxLogThresholds.FormattingEnabled = true;
            this._comboBoxLogThresholds.Location = new System.Drawing.Point(16, 51);
            this._comboBoxLogThresholds.Name = "comboBoxLogThresholds";
            this._comboBoxLogThresholds.Size = new System.Drawing.Size(121, 21);
            this._comboBoxLogThresholds.TabIndex = 2;
            this._comboBoxLogThresholds.SelectedIndexChanged += new System.EventHandler(this.comboBoxLogThresholds_SelectedIndexChanged);
            // 
            // labelLogLevel
            // 
            this._labelLogLevel.AutoSize = true;
            this._labelLogLevel.Location = new System.Drawing.Point(13, 35);
            this._labelLogLevel.Name = "labelLogLevel";
            this._labelLogLevel.Size = new System.Drawing.Size(78, 13);
            this._labelLogLevel.TabIndex = 1;
            this._labelLogLevel.Text = "Log Threshold:";
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
            // _checkBoxAutoStart
            // 
            this._checkBoxAutoStart.Location = new System.Drawing.Point(32, 208);
            this._checkBoxAutoStart.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxAutoStart.Name = "_checkBoxAutoStart";
            this._checkBoxAutoStart.Size = new System.Drawing.Size(160, 16);
            this._checkBoxAutoStart.TabIndex = 5;
            this._checkBoxAutoStart.Text = "&Automatically start at login";
            this._checkBoxAutoStart.Visible = false;
            this._checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.CheckBoxAutoStartCheckedChanged);
            // 
            // tabClient
            // 
            this._tabClient.BackColor = System.Drawing.SystemColors.Window;
            this._tabClient.Controls.Add(this._checkBoxEnableClient);
            this._tabClient.Controls.Add(this._clientGroup);
            this._tabClient.Location = new System.Drawing.Point(4, 22);
            this._tabClient.Margin = new System.Windows.Forms.Padding(1);
            this._tabClient.Name = "tabClient";
            this._tabClient.Size = new System.Drawing.Size(440, 238);
            this._tabClient.TabIndex = 1;
            this._tabClient.Text = "Client";
            // 
            // _checkBoxEnableClient
            // 
            this._checkBoxEnableClient.AutoSize = true;
            this._checkBoxEnableClient.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableClient.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableClient.Name = "_checkBoxEnableClient";
            this._checkBoxEnableClient.Size = new System.Drawing.Size(88, 17);
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
            // tabServer
            // 
            this._tabServer.BackColor = System.Drawing.SystemColors.Window;
            this._tabServer.Controls.Add(this._checkBoxEnableServer);
            this._tabServer.Controls.Add(this._serverGroup);
            this._tabServer.Location = new System.Drawing.Point(4, 22);
            this._tabServer.Margin = new System.Windows.Forms.Padding(1);
            this._tabServer.Name = "tabServer";
            this._tabServer.Size = new System.Drawing.Size(440, 238);
            this._tabServer.TabIndex = 2;
            this._tabServer.Text = "Server";
            // 
            // _checkBoxEnableServer
            // 
            this._checkBoxEnableServer.AutoSize = true;
            this._checkBoxEnableServer.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableServer.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableServer.Name = "_checkBoxEnableServer";
            this._checkBoxEnableServer.Size = new System.Drawing.Size(93, 17);
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
            // tabSerial
            // 
            this._tabSerial.BackColor = System.Drawing.SystemColors.Window;
            this._tabSerial.Controls.Add(this._checkBoxEnableSerialServer);
            this._tabSerial.Controls.Add(this._serialServerGroup);
            this._tabSerial.Location = new System.Drawing.Point(4, 22);
            this._tabSerial.Margin = new System.Windows.Forms.Padding(1);
            this._tabSerial.Name = "tabSerial";
            this._tabSerial.Padding = new System.Windows.Forms.Padding(1);
            this._tabSerial.Size = new System.Drawing.Size(440, 238);
            this._tabSerial.TabIndex = 3;
            this._tabSerial.Text = "Serial Server";
            // 
            // _checkBoxEnableSerialServer
            // 
            this._checkBoxEnableSerialServer.AutoSize = true;
            this._checkBoxEnableSerialServer.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableSerialServer.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableSerialServer.Name = "_checkBoxEnableSerialServer";
            this._checkBoxEnableSerialServer.Size = new System.Drawing.Size(122, 17);
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
            this._comboBoxHandshake.Items.AddRange(new object[] {
            "None",
            "Xon / Xoff",
            "Hardware",
            "Both"});
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
            this._comboBoxStopBits.Items.AddRange(new object[] {
            "1",
            "2",
            "1.5"});
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
            this._comboBoxParity.Items.AddRange(new object[] {
            "None",
            "Odd",
            "Even",
            "Mark",
            "Space"});
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
            this._comboBoxDataBits.Items.AddRange(new object[] {
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
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
            this._comboBoxBaudRate.Items.AddRange(new object[] {
            "2400",
            "4800",
            "9600",
            "19200",
            "38400",
            "57600",
            "115200"});
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
            this._comboBoxSerialPort.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8"});
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
            // _tabPageActivityMonitor
            // 
            this._tabPageActivityMonitor.Controls.Add(this._checkBoxEnableActivityMonitor);
            this._tabPageActivityMonitor.Controls.Add(this._groupBoxActivityMonitor);
            this._tabPageActivityMonitor.Location = new System.Drawing.Point(4, 22);
            this._tabPageActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._tabPageActivityMonitor.Name = "_tabPageActivityMonitor";
            this._tabPageActivityMonitor.Size = new System.Drawing.Size(440, 238);
            this._tabPageActivityMonitor.TabIndex = 4;
            this._tabPageActivityMonitor.Text = "Activity Monitor";
            this._tabPageActivityMonitor.UseVisualStyleBackColor = true;
            // 
            // checkBoxEnableActivityMonitor
            // 
            this._checkBoxEnableActivityMonitor.AutoSize = true;
            this._checkBoxEnableActivityMonitor.Location = new System.Drawing.Point(20, 10);
            this._checkBoxEnableActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxEnableActivityMonitor.Name = "checkBoxEnableActivityMonitor";
            this._checkBoxEnableActivityMonitor.Size = new System.Drawing.Size(159, 17);
            this._checkBoxEnableActivityMonitor.TabIndex = 0;
            this._checkBoxEnableActivityMonitor.Text = "Enable &User Activity Monitor";
            this._checkBoxEnableActivityMonitor.UseVisualStyleBackColor = true;
            this._checkBoxEnableActivityMonitor.CheckedChanged += new System.EventHandler(this.checkBoxEnableActivityMonitor_CheckedChanged);
            // 
            // groupBoxActivityMonitor
            // 
            this._groupBoxActivityMonitor.Controls.Add(this._unlockDetection);
            this._groupBoxActivityMonitor.Controls.Add(this._inputDetection);
            this._groupBoxActivityMonitor.Controls.Add(this._labelActivityDebounceTime);
            this._groupBoxActivityMonitor.Controls.Add(this._textBoxDebounceTime);
            this._groupBoxActivityMonitor.Controls.Add(this._textBoxActivityCommand);
            this._groupBoxActivityMonitor.Controls.Add(this._labelActivityCommand);
            this._groupBoxActivityMonitor.Location = new System.Drawing.Point(12, 11);
            this._groupBoxActivityMonitor.Margin = new System.Windows.Forms.Padding(1);
            this._groupBoxActivityMonitor.Name = "groupBoxActivityMonitor";
            this._groupBoxActivityMonitor.Padding = new System.Windows.Forms.Padding(1);
            this._groupBoxActivityMonitor.Size = new System.Drawing.Size(412, 221);
            this._groupBoxActivityMonitor.TabIndex = 0;
            this._groupBoxActivityMonitor.TabStop = false;
            // 
            // unlockDetectionRadio
            // 
            this._unlockDetection.AutoSize = true;
            this._unlockDetection.Location = new System.Drawing.Point(17, 53);
            this._unlockDetection.Name = "unlockDetectionRadio";
            this._unlockDetection.Size = new System.Drawing.Size(211, 17);
            this._unlockDetection.TabIndex = 1;
            this._unlockDetection.TabStop = true;
            this._unlockDetection.Text = "Detect activity via desktop lock/unlock";
            this._unlockDetection.UseVisualStyleBackColor = true;
            this._unlockDetection.CheckedChanged += new System.EventHandler(this.unlockDetectionRadio_CheckedChanged);
            // 
            // inputDetectionRadio
            // 
            this._inputDetection.AutoSize = true;
            this._inputDetection.Location = new System.Drawing.Point(17, 30);
            this._inputDetection.Name = "inputDetectionRadio";
            this._inputDetection.Size = new System.Drawing.Size(238, 17);
            this._inputDetection.TabIndex = 0;
            this._inputDetection.TabStop = true;
            this._inputDetection.Text = "Detect activity via keyboard and mouse input";
            this._inputDetection.UseVisualStyleBackColor = true;
            this._inputDetection.CheckedChanged += new System.EventHandler(this.inputDetectionRadio_CheckedChanged);
            // 
            // labelActivityDebounceTime
            // 
            this._labelActivityDebounceTime.AutoSize = true;
            this._labelActivityDebounceTime.Location = new System.Drawing.Point(17, 112);
            this._labelActivityDebounceTime.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelActivityDebounceTime.Name = "labelActivityDebounceTime";
            this._labelActivityDebounceTime.Size = new System.Drawing.Size(283, 13);
            this._labelActivityDebounceTime.TabIndex = 4;
            this._labelActivityDebounceTime.Text = "Send activity command no more frequently than (seconds):";
            // 
            // textBoxDebounceTime
            // 
            this._textBoxDebounceTime.Location = new System.Drawing.Point(302, 109);
            this._textBoxDebounceTime.Margin = new System.Windows.Forms.Padding(1);
            this._textBoxDebounceTime.Name = "textBoxDebounceTime";
            this._textBoxDebounceTime.Size = new System.Drawing.Size(51, 20);
            this._textBoxDebounceTime.TabIndex = 5;
            this._textBoxDebounceTime.TextChanged += new System.EventHandler(this.textBoxDebounceTime_TextChanged);
            // 
            // textBoxActivityCommand
            // 
            this._textBoxActivityCommand.Location = new System.Drawing.Point(114, 80);
            this._textBoxActivityCommand.Margin = new System.Windows.Forms.Padding(1);
            this._textBoxActivityCommand.Name = "textBoxActivityCommand";
            this._textBoxActivityCommand.Size = new System.Drawing.Size(149, 20);
            this._textBoxActivityCommand.TabIndex = 3;
            this._textBoxActivityCommand.TextChanged += new System.EventHandler(this.textBoxActivityCommand_TextChanged);
            // 
            // labelActivityCommand
            // 
            this._labelActivityCommand.AutoSize = true;
            this._labelActivityCommand.Location = new System.Drawing.Point(17, 83);
            this._labelActivityCommand.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelActivityCommand.Name = "labelActivityCommand";
            this._labelActivityCommand.Size = new System.Drawing.Size(95, 13);
            this._labelActivityCommand.TabIndex = 2;
            this._labelActivityCommand.Text = "Command to send:";
            // 
            // toolTipClient
            // 
            this._toolTipClient.ToolTipTitle = "Client";
            // 
            // _toolTipServer
            // 
            this._toolTipServer.ToolTipTitle = "Server";
            // 
            // eventLog1
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
            this._tabGeneral.ResumeLayout(false);
            this._tabGeneral.PerformLayout();
            this._tabClient.ResumeLayout(false);
            this._tabClient.PerformLayout();
            this._clientGroup.ResumeLayout(false);
            this._clientGroup.PerformLayout();
            this._tabServer.ResumeLayout(false);
            this._tabServer.PerformLayout();
            this._serverGroup.ResumeLayout(false);
            this._serverGroup.PerformLayout();
            this._wakeupGroup.ResumeLayout(false);
            this._wakeupGroup.PerformLayout();
            this._tabSerial.ResumeLayout(false);
            this._tabSerial.PerformLayout();
            this._serialServerGroup.ResumeLayout(false);
            this._serialServerGroup.PerformLayout();
            this._tabPageActivityMonitor.ResumeLayout(false);
            this._tabPageActivityMonitor.PerformLayout();
            this._groupBoxActivityMonitor.ResumeLayout(false);
            this._groupBoxActivityMonitor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._eventLog)).EndInit();
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
            Settings = (AppSettings)settings.Clone();

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
            _comboBoxParity.SelectedIndex = (int)Settings.SerialServerParity;
            _comboBoxStopBits.SelectedIndex = (int)Settings.SerialServerStopBits - 1; // None (0) is not allowed
            _comboBoxHandshake.SelectedIndex = (int)Settings.SerialServerHandshake;

            _clientGroup.Enabled = _checkBoxEnableClient.Checked;
            _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
            _serverGroup.Enabled = _checkBoxEnableServer.Checked;
            _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;


            _groupBoxActivityMonitor.Enabled = _checkBoxEnableActivityMonitor.Checked = Settings.ActivityMonitorEnabled;
            _unlockDetection.Checked = Settings.UnlockDetection;
            _inputDetection.Checked = Settings.InputDetection;
            _textBoxActivityCommand.Text = Settings.ActivityMonitorCommand;
            _textBoxDebounceTime.Text = $"{Settings.ActivityMonitorDebounceTime}";

            _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["ALL"]);
            _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["INFO"]);
            _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["DEBUG"]);

            switch (Settings.TextBoxLogThreshold) {
                case "ALL":
                    _comboBoxLogThresholds.SelectedIndex = 0;
                    break;

                case "INFO":
                    _comboBoxLogThresholds.SelectedIndex = 1;
                    break;

                case "DEBUG":
                    _comboBoxLogThresholds.SelectedIndex = 2;
                    break;
            }

            _textBoxPacing.Text = $"{Settings.CommandPacing}";

            //comboBoxLogThresholds.SelectedIndex = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap["ALL"].Value;

            _buttonOk.Enabled = false;
        }

        private void SettingsChanged() {
            if (_checkBoxEnableServer.Checked && _checkBoxEnableWakeup.Checked) {
                if (!int.TryParse(_editWakeupPort.Text, out var port)) {
                    port = 0;
                }

                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editWakeupServer.Text) ||
                                      String.IsNullOrEmpty(_editWakeupCommand.Text) ||
                                      String.IsNullOrEmpty(_editClosingCommand.Text) ||
                                      (port == 0));
                return;
            }

            if (_checkBoxEnableClient.Checked) {
                if (!int.TryParse(_editClientPort.Text, out var port)) {
                    port = 0;
                }

                _buttonOk.Enabled = !(String.IsNullOrEmpty(_editClientHost.Text) ||
                                      (port == 0));
                return;
            }

            if (_checkBoxEnableActivityMonitor.Checked) {
                if (!int.TryParse(_textBoxDebounceTime.Text, out var t)) {
                    t = 0;
                }

                _buttonOk.Enabled = !(String.IsNullOrEmpty(_textBoxActivityCommand.Text) ||
                                    String.IsNullOrEmpty(_textBoxDebounceTime.Text) ||
                                    (t == 0));
                return;
            }

            _buttonOk.Enabled = true;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (_components != null) {
                _components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ButtonCancelClick(object sender, EventArgs e) {
            Close();
        }

        private void ButtonOkClick(object sender, EventArgs e) {
            Settings.Serialize($@"{Program.ConfigPath}{AppSettings.SettingsFileName}");
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
            if (int.TryParse(_editServerPort.Text, out var port)) {
                Settings.ServerPort = port;
            }

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
            if (int.TryParse(_editWakeupPort.Text, out var port)) {
                Settings.WakeupPort = port;
            }

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
            if (int.TryParse(_editClientPort.Text, out var port)) {
                Settings.ClientPort = port;
            }

            SettingsChanged();
        }

        private void EditClientHostTextChanged(object sender, EventArgs e) {
            Settings.ClientHost = _editClientHost.Text;
            SettingsChanged();
        }

        private void EditClientDelayTimeTextChanged(object sender, EventArgs e) {
            if (_editClientDelayTime.Text.Length > 0) {
                Settings.ClientDelayTime = Convert.ToInt32(_editClientDelayTime.Text, new NumberFormatInfo());
            }

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
            if (int.TryParse(_comboBoxBaudRate.SelectedItem.ToString(), out var baud)) {
                Settings.SerialServerBaudRate = baud;
            }

            SettingsChanged();
        }

        private void ComboBoxParitySelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxParity.SelectedItem != null) {
                Settings.SerialServerParity = (Parity)_comboBoxParity.SelectedIndex;
                SettingsChanged();
            }
        }

        private void ComboBoxDataBitsSelectedIndexChanged(object sender, EventArgs e) {
            if (int.TryParse(_comboBoxDataBits.SelectedItem.ToString(), out var bits)) {
                Settings.SerialServerDataBits = bits;
            }

            SettingsChanged();
        }

        private void ComboBoxStopBitsSelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxStopBits.SelectedItem != null) {
                // Add one because None is invalid and is not included in the combo box
                Settings.SerialServerStopBits = (StopBits)_comboBoxStopBits.SelectedIndex + 1;
                SettingsChanged();
            }
        }

        private void ComboBoxHandshakeSelectedIndexChanged(object sender, EventArgs e) {
            if (_comboBoxHandshake.SelectedItem != null) {
                Settings.SerialServerHandshake = (Handshake)_comboBoxHandshake.SelectedIndex;
                SettingsChanged();
            }
        }

        private void checkBoxEnableActivityMonitor_CheckedChanged(object sender, EventArgs e) {
            Settings.ActivityMonitorEnabled = _checkBoxEnableActivityMonitor.Checked;
            _groupBoxActivityMonitor.Enabled = _checkBoxEnableActivityMonitor.Checked;
            SettingsChanged();
        }

        private void textBoxActivityCommand_TextChanged(object sender, EventArgs e) {
            if (_textBoxActivityCommand.Text.Length > 0) {
                Settings.ActivityMonitorCommand = _textBoxActivityCommand.Text;
            }

            SettingsChanged();
        }

        private void textBoxDebounceTime_TextChanged(object sender, EventArgs e) {
            if (int.TryParse(_textBoxDebounceTime.Text, out var t)) {
                Settings.ActivityMonitorDebounceTime = t;
            }

            SettingsChanged();
        }
        private void SettingsDialog_Load(object sender, EventArgs e) {
            switch (DefaultTab) {
                case "General":
                    _tabcontrol.SelectedTab = _tabGeneral;
                    break;

                case "Client":
                    _tabcontrol.SelectedTab = _tabClient;
                    break;

                case "Server":
                    _tabcontrol.SelectedTab = _tabServer;
                    break;

                case "Serial":
                    _tabcontrol.SelectedTab = _tabSerial;
                    break;

            }
        }

        private void comboBoxLogThresholds_SelectedIndexChanged(object sender, EventArgs e) {
            Settings.TextBoxLogThreshold = _comboBoxLogThresholds.SelectedItem.ToString();
            SettingsChanged();
        }

        private void textBoxPacing_TextChanged(object sender, EventArgs e) {
            if (int.TryParse(_textBoxPacing.Text, out var t)) {
                Settings.CommandPacing = t;
            }

            SettingsChanged();
        }

        private void inputDetectionRadio_CheckedChanged(object sender, EventArgs e) {
            Settings.InputDetection = _inputDetection.Checked;
            SettingsChanged();
        }

        private void unlockDetectionRadio_CheckedChanged(object sender, EventArgs e) {
            Settings.UnlockDetection = _unlockDetection.Checked;
            SettingsChanged();
        }
    }
}
