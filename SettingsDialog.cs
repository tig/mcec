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
using System.Drawing;
using System.Globalization;
using System.IO;
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
        private TabPage _tabServer;

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

            // Server tab setup
            _checkBoxEnableServer.Checked = Settings.ActAsServer;
            _editServerPort.Text = Settings.ServerPort.ToString(CultureInfo.InvariantCulture);
            _checkBoxEnableWakeup.Checked = Settings.WakeupEnabled;
            _editWakeupServer.Text = Settings.WakeupHost;
            _editWakeupPort.Text = Settings.WakeupPort.ToString(CultureInfo.InvariantCulture);
            _editWakeupCommand.Text = Settings.WakeupCommand;
            _editClosingCommand.Text = Settings.ClosingCommand;

            _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
            _serverGroup.Enabled = _checkBoxEnableServer.Checked;

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._buttonCancel = new System.Windows.Forms.Button();
            this._buttonOk = new System.Windows.Forms.Button();
            this._tabControl = new System.Windows.Forms.TabControl();
            this._general = new System.Windows.Forms.TabPage();
            this._checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
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
            this._checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this._tabControl.SuspendLayout();
            this._general.SuspendLayout();
            this._tabClient.SuspendLayout();
            this._clientGroup.SuspendLayout();
            this._tabServer.SuspendLayout();
            this._serverGroup.SuspendLayout();
            this._wakeupGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonCancel
            // 
            this._buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._buttonCancel.Location = new System.Drawing.Point(152, 320);
            this._buttonCancel.Name = "_buttonCancel";
            this._buttonCancel.TabIndex = 1;
            this._buttonCancel.Text = "Cancel";
            this._buttonCancel.Click += new System.EventHandler(this.ButtonCancelClick);
            // 
            // buttonOK
            // 
            this._buttonOk.Location = new System.Drawing.Point(64, 320);
            this._buttonOk.Name = "_buttonOk";
            this._buttonOk.TabIndex = 0;
            this._buttonOk.Text = "OK";
            this._buttonOk.Click += new System.EventHandler(this.ButtonOkClick);
            // 
            // tabControl
            // 
            this._tabControl.Anchor =
                ((System.Windows.Forms.AnchorStyles)
                 (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                   | System.Windows.Forms.AnchorStyles.Right)));
            this._tabControl.Controls.Add(this._general);
            this._tabControl.Controls.Add(this._tabClient);
            this._tabControl.Controls.Add(this._tabServer);
            this._tabControl.Location = new System.Drawing.Point(0, 0);
            this._tabControl.Name = "_tabControl";
            this._tabControl.SelectedIndex = 0;
            this._tabControl.Size = new System.Drawing.Size(248, 312);
            this._tabControl.TabIndex = 0;
            // 
            // General
            // 
            this._general.Controls.Add(this._checkBoxHideOnStartup);
            this._general.Controls.Add(this._checkBoxAutoStart);
            this._general.Location = new System.Drawing.Point(4, 22);
            this._general.Name = "_general";
            this._general.Size = new System.Drawing.Size(240, 286);
            this._general.TabIndex = 0;
            this._general.Text = "General";
            // 
            // checkBoxHideOnStartup
            // 
            this._checkBoxHideOnStartup.Location = new System.Drawing.Point(16, 8);
            this._checkBoxHideOnStartup.Name = "_checkBoxHideOnStartup";
            this._checkBoxHideOnStartup.Size = new System.Drawing.Size(160, 16);
            this._checkBoxHideOnStartup.TabIndex = 0;
            this._checkBoxHideOnStartup.Text = "&Hide window on startup";
            this._checkBoxHideOnStartup.CheckedChanged +=
                new System.EventHandler(this.CheckBoxHideOnStartupCheckedChanged);
            // 
            // tabClient
            // 
            this._tabClient.Controls.Add(this._checkBoxEnableClient);
            this._tabClient.Controls.Add(this._clientGroup);
            this._tabClient.Location = new System.Drawing.Point(4, 22);
            this._tabClient.Name = "_tabClient";
            this._tabClient.Size = new System.Drawing.Size(240, 286);
            this._tabClient.TabIndex = 1;
            this._tabClient.Text = "Client";
            // 
            // checkBoxEnableClient
            // 
            this._checkBoxEnableClient.Location = new System.Drawing.Point(16, 8);
            this._checkBoxEnableClient.Name = "_checkBoxEnableClient";
            this._checkBoxEnableClient.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableClient.TabIndex = 0;
            this._checkBoxEnableClient.Text = "Enable &Client";
            this._checkBoxEnableClient.CheckedChanged += new System.EventHandler(this.CheckEnableClientCheckedChanged);
            // 
            // ClientGroup
            // 
            this._clientGroup.Controls.Add(this._editClientPort);
            this._clientGroup.Controls.Add(this._label6);
            this._clientGroup.Controls.Add(this._label8);
            this._clientGroup.Controls.Add(this._editClientHost);
            this._clientGroup.Controls.Add(this._label7);
            this._clientGroup.Controls.Add(this._editClientDelayTime);
            this._clientGroup.Location = new System.Drawing.Point(8, 8);
            this._clientGroup.Name = "_clientGroup";
            this._clientGroup.Size = new System.Drawing.Size(224, 272);
            this._clientGroup.TabIndex = 8;
            this._clientGroup.TabStop = false;
            // 
            // editClientPort
            // 
            this._editClientPort.Location = new System.Drawing.Point(16, 80);
            this._editClientPort.Name = "_editClientPort";
            this._editClientPort.Size = new System.Drawing.Size(56, 20);
            this._editClientPort.TabIndex = 3;
            this._editClientPort.Text = "";
            this._editClientPort.TextChanged += new System.EventHandler(this.EditClientPortTextChanged);
            // 
            // label6
            // 
            this._label6.Location = new System.Drawing.Point(16, 64);
            this._label6.Name = "_label6";
            this._label6.Size = new System.Drawing.Size(32, 16);
            this._label6.TabIndex = 2;
            this._label6.Text = "&Port:";
            // 
            // label8
            // 
            this._label8.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label8.Location = new System.Drawing.Point(16, 24);
            this._label8.Name = "_label8";
            this._label8.Size = new System.Drawing.Size(88, 16);
            this._label8.TabIndex = 0;
            this._label8.Text = "&Host:";
            // 
            // editClientHost
            // 
            this._editClientHost.Location = new System.Drawing.Point(16, 40);
            this._editClientHost.Name = "_editClientHost";
            this._editClientHost.Size = new System.Drawing.Size(160, 20);
            this._editClientHost.TabIndex = 1;
            this._editClientHost.Text = "";
            this._editClientHost.TextChanged += new System.EventHandler(this.EditClientHostTextChanged);
            // 
            // label7
            // 
            this._label7.Location = new System.Drawing.Point(16, 104);
            this._label7.Name = "_label7";
            this._label7.Size = new System.Drawing.Size(144, 16);
            this._label7.TabIndex = 2;
            this._label7.Text = "&Reconnect Wait Time:";
            // 
            // editClientDelayTime
            // 
            this._editClientDelayTime.Location = new System.Drawing.Point(16, 120);
            this._editClientDelayTime.Name = "_editClientDelayTime";
            this._editClientDelayTime.Size = new System.Drawing.Size(56, 20);
            this._editClientDelayTime.TabIndex = 3;
            this._editClientDelayTime.Text = "";
            this._editClientDelayTime.TextChanged += new System.EventHandler(this.EditClientDelayTimeTextChanged);
            // 
            // tabServer
            // 
            this._tabServer.Controls.Add(this._checkBoxEnableServer);
            this._tabServer.Controls.Add(this._serverGroup);
            this._tabServer.Location = new System.Drawing.Point(4, 22);
            this._tabServer.Name = "_tabServer";
            this._tabServer.Size = new System.Drawing.Size(240, 286);
            this._tabServer.TabIndex = 2;
            this._tabServer.Text = "Server";
            // 
            // checkBoxEnableServer
            // 
            this._checkBoxEnableServer.Location = new System.Drawing.Point(16, 8);
            this._checkBoxEnableServer.Name = "_checkBoxEnableServer";
            this._checkBoxEnableServer.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableServer.TabIndex = 1;
            this._checkBoxEnableServer.Text = "Enable &Server";
            this._checkBoxEnableServer.CheckedChanged += new System.EventHandler(this.CheckBoxEnableServerCheckedChanged);
            // 
            // ServerGroup
            // 
            this._serverGroup.Controls.Add(this._editServerPort);
            this._serverGroup.Controls.Add(this._label1);
            this._serverGroup.Controls.Add(this._checkBoxEnableWakeup);
            this._serverGroup.Controls.Add(this._wakeupGroup);
            this._serverGroup.Location = new System.Drawing.Point(8, 8);
            this._serverGroup.Name = "_serverGroup";
            this._serverGroup.Size = new System.Drawing.Size(224, 264);
            this._serverGroup.TabIndex = 6;
            this._serverGroup.TabStop = false;
            // 
            // editServerPort
            // 
            this._editServerPort.Location = new System.Drawing.Point(48, 24);
            this._editServerPort.Name = "_editServerPort";
            this._editServerPort.Size = new System.Drawing.Size(56, 20);
            this._editServerPort.TabIndex = 1;
            this._editServerPort.Text = "";
            this._editServerPort.TextChanged += new System.EventHandler(this.EditServerPortTextChanged);
            // 
            // label1
            // 
            this._label1.Location = new System.Drawing.Point(16, 26);
            this._label1.Name = "_label1";
            this._label1.Size = new System.Drawing.Size(32, 16);
            this._label1.TabIndex = 0;
            this._label1.Text = "&Port:";
            // 
            // checkBoxEnableWakeup
            // 
            this._checkBoxEnableWakeup.Location = new System.Drawing.Point(24, 56);
            this._checkBoxEnableWakeup.Name = "_checkBoxEnableWakeup";
            this._checkBoxEnableWakeup.Size = new System.Drawing.Size(104, 16);
            this._checkBoxEnableWakeup.TabIndex = 2;
            this._checkBoxEnableWakeup.Text = "Enable &Wakeup";
            this._checkBoxEnableWakeup.CheckedChanged += new System.EventHandler(this.CheckBoxEnableWakeupCheckedChanged);
            // 
            // WakeupGroup
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
            this._wakeupGroup.Size = new System.Drawing.Size(192, 192);
            this._wakeupGroup.TabIndex = 7;
            this._wakeupGroup.TabStop = false;
            // 
            // editWakeupServer
            // 
            this._editWakeupServer.Location = new System.Drawing.Point(16, 41);
            this._editWakeupServer.Name = "_editWakeupServer";
            this._editWakeupServer.Size = new System.Drawing.Size(160, 20);
            this._editWakeupServer.TabIndex = 1;
            this._editWakeupServer.Text = "";
            this._editWakeupServer.TextChanged += new System.EventHandler(this.EditWakeupServerTextChanged);
            // 
            // editWakeupCommand
            // 
            this._editWakeupCommand.Location = new System.Drawing.Point(16, 120);
            this._editWakeupCommand.Name = "_editWakeupCommand";
            this._editWakeupCommand.Size = new System.Drawing.Size(160, 20);
            this._editWakeupCommand.TabIndex = 5;
            this._editWakeupCommand.Text = "";
            this._editWakeupCommand.TextChanged += new System.EventHandler(this.EditWakeupCommandTextChanged);
            // 
            // editClosingCommand
            // 
            this._editClosingCommand.Location = new System.Drawing.Point(16, 160);
            this._editClosingCommand.Name = "_editClosingCommand";
            this._editClosingCommand.Size = new System.Drawing.Size(160, 20);
            this._editClosingCommand.TabIndex = 7;
            this._editClosingCommand.Text = "";
            this._editClosingCommand.TextChanged += new System.EventHandler(this.EditClosingCommandTextChanged);
            // 
            // editWakeupPort
            // 
            this._editWakeupPort.Location = new System.Drawing.Point(16, 80);
            this._editWakeupPort.Name = "_editWakeupPort";
            this._editWakeupPort.Size = new System.Drawing.Size(56, 20);
            this._editWakeupPort.TabIndex = 3;
            this._editWakeupPort.Text = "";
            this._editWakeupPort.TextChanged += new System.EventHandler(this.EditWakeupPortTextChanged);
            // 
            // label5
            // 
            this._label5.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label5.Location = new System.Drawing.Point(16, 144);
            this._label5.Name = "_label5";
            this._label5.Size = new System.Drawing.Size(104, 16);
            this._label5.TabIndex = 6;
            this._label5.Text = "Closing Command:";
            // 
            // label2
            // 
            this._label2.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label2.Location = new System.Drawing.Point(16, 24);
            this._label2.Name = "_label2";
            this._label2.Size = new System.Drawing.Size(88, 16);
            this._label2.TabIndex = 0;
            this._label2.Text = "Wa&keup Host:";
            // 
            // label4
            // 
            this._label4.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label4.Location = new System.Drawing.Point(16, 104);
            this._label4.Name = "_label4";
            this._label4.Size = new System.Drawing.Size(104, 16);
            this._label4.TabIndex = 4;
            this._label4.Text = "Wakeup Command:";
            // 
            // label3
            // 
            this._label3.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._label3.Location = new System.Drawing.Point(16, 64);
            this._label3.Name = "_label3";
            this._label3.Size = new System.Drawing.Size(48, 16);
            this._label3.TabIndex = 2;
            this._label3.Text = "P&ort:";
            // 
            // checkBoxAutoStart
            // 
            this._checkBoxAutoStart.Location = new System.Drawing.Point(16, 32);
            this._checkBoxAutoStart.Name = "_checkBoxAutoStart";
            this._checkBoxAutoStart.Size = new System.Drawing.Size(160, 16);
            this._checkBoxAutoStart.TabIndex = 0;
            this._checkBoxAutoStart.Text = "&Automatically start at login";
            this._checkBoxAutoStart.Visible = false;
            this._checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.CheckBoxAutoStartCheckedChanged);
            // 
            // SettingsDialog
            // 
            this.AcceptButton = this._buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this._buttonCancel;
            this.ClientSize = new System.Drawing.Size(242, 352);
            this.Controls.Add(this._tabControl);
            this.Controls.Add(this._buttonCancel);
            this.Controls.Add(this._buttonOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "MCE Controller Settings";
            this._tabControl.ResumeLayout(false);
            this._general.ResumeLayout(false);
            this._tabClient.ResumeLayout(false);
            this._clientGroup.ResumeLayout(false);
            this._tabServer.ResumeLayout(false);
            this._serverGroup.ResumeLayout(false);
            this._wakeupGroup.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion
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
        public Point WindowLocation = new Point(120, 50);
        public Size WindowSize = new Size(640, 400);

        #region ICloneable Members

        public object Clone() {
            return MemberwiseClone();
        }

        #endregion

        // Must have a default public constructor so XMLSerialization will work
        // This class is NOT supposed to be creatable (use Deserialize to construct).

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
                MainWindow.AddLogEntry("Wrote settings to " + filePath);
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
                MainWindow.AddLogEntry("Read settings from " + filePath);
            }
            catch (FileNotFoundException) {
                // First time through, so create file with defaults
                MainWindow.AddLogEntry("Creating default settings file.");
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