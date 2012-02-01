//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the BSD License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Security.AccessControl;

namespace MCEControl
{
    /// <summary>
    /// Settings dialog box
    /// </summary>
    public class SettingsDialog : System.Windows.Forms.Form
    {
        public AppSettings Settings = null;

        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage General;
        private System.Windows.Forms.TabPage tabClient;
        private System.Windows.Forms.TabPage tabServer;
        private System.Windows.Forms.CheckBox checkBoxHideOnStartup;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox editServerPort;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox editWakeupServer;
        private System.Windows.Forms.CheckBox checkBoxEnableServer;
        private System.Windows.Forms.GroupBox ServerGroup;
        private System.Windows.Forms.TextBox editWakeupCommand;
        private System.Windows.Forms.TextBox editClosingCommand;
        private System.Windows.Forms.TextBox editWakeupPort;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox WakeupGroup;
        private System.Windows.Forms.CheckBox checkBoxEnableWakeup;
        private System.Windows.Forms.TextBox editClientPort;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox ClientGroup;
        private System.Windows.Forms.TextBox editClientHost;
        private System.Windows.Forms.CheckBox checkBoxEnableClient;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox editClientDelayTime;
        private System.Windows.Forms.CheckBox checkBoxAutoStart;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        private SettingsDialog()
        {
        }

        public SettingsDialog(AppSettings settings)
        {

            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // Clone the settings object
            Settings = (AppSettings)settings.Clone();

            // Handle General tab setup
            checkBoxHideOnStartup.Checked = Settings.HideOnStartup;
            checkBoxAutoStart.Checked = Settings.AutoStart;

            // Client tab setup
            checkBoxEnableClient.Checked = Settings.ActAsClient;
            editClientPort.Text = Settings.ClientPort.ToString();
            editClientHost.Text = Settings.ClientHost;
            editClientDelayTime.Text = Settings.ClientDelayTime.ToString();

            // Server tab setup
            checkBoxEnableServer.Checked = Settings.ActAsServer;
            editServerPort.Text = Settings.ServerPort.ToString();
            checkBoxEnableWakeup.Checked = Settings.WakeupEnabled;
            editWakeupServer.Text = Settings.WakeupHost;
            editWakeupPort.Text = Settings.WakeupPort.ToString();
            editWakeupCommand.Text = Settings.WakeupCommand;
            editClosingCommand.Text = Settings.ClosingCommand;

            WakeupGroup.Enabled = checkBoxEnableWakeup.Checked;
            ServerGroup.Enabled = checkBoxEnableServer.Checked;

            buttonOK.Enabled = false;
        }

        private void SettingsChanged()
        {
            buttonOK.Enabled = true;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                if(components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.General = new System.Windows.Forms.TabPage();
            this.checkBoxHideOnStartup = new System.Windows.Forms.CheckBox();
            this.tabClient = new System.Windows.Forms.TabPage();
            this.checkBoxEnableClient = new System.Windows.Forms.CheckBox();
            this.ClientGroup = new System.Windows.Forms.GroupBox();
            this.editClientPort = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.editClientHost = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.editClientDelayTime = new System.Windows.Forms.TextBox();
            this.tabServer = new System.Windows.Forms.TabPage();
            this.checkBoxEnableServer = new System.Windows.Forms.CheckBox();
            this.ServerGroup = new System.Windows.Forms.GroupBox();
            this.editServerPort = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBoxEnableWakeup = new System.Windows.Forms.CheckBox();
            this.WakeupGroup = new System.Windows.Forms.GroupBox();
            this.editWakeupServer = new System.Windows.Forms.TextBox();
            this.editWakeupCommand = new System.Windows.Forms.TextBox();
            this.editClosingCommand = new System.Windows.Forms.TextBox();
            this.editWakeupPort = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.checkBoxAutoStart = new System.Windows.Forms.CheckBox();
            this.tabControl.SuspendLayout();
            this.General.SuspendLayout();
            this.tabClient.SuspendLayout();
            this.ClientGroup.SuspendLayout();
            this.tabServer.SuspendLayout();
            this.ServerGroup.SuspendLayout();
            this.WakeupGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(152, 320);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(64, 320);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.TabIndex = 0;
            this.buttonOK.Text = "OK";
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
                | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.General);
            this.tabControl.Controls.Add(this.tabClient);
            this.tabControl.Controls.Add(this.tabServer);
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(248, 312);
            this.tabControl.TabIndex = 0;
            // 
            // General
            // 
            this.General.Controls.Add(this.checkBoxHideOnStartup);
            this.General.Controls.Add(this.checkBoxAutoStart);
            this.General.Location = new System.Drawing.Point(4, 22);
            this.General.Name = "General";
            this.General.Size = new System.Drawing.Size(240, 286);
            this.General.TabIndex = 0;
            this.General.Text = "General";
            // 
            // checkBoxHideOnStartup
            // 
            this.checkBoxHideOnStartup.Location = new System.Drawing.Point(16, 8);
            this.checkBoxHideOnStartup.Name = "checkBoxHideOnStartup";
            this.checkBoxHideOnStartup.Size = new System.Drawing.Size(160, 16);
            this.checkBoxHideOnStartup.TabIndex = 0;
            this.checkBoxHideOnStartup.Text = "&Hide window on startup";
            this.checkBoxHideOnStartup.CheckedChanged += new System.EventHandler(this.checkBoxHideOnStartup_CheckedChanged);
            // 
            // tabClient
            // 
            this.tabClient.Controls.Add(this.checkBoxEnableClient);
            this.tabClient.Controls.Add(this.ClientGroup);
            this.tabClient.Location = new System.Drawing.Point(4, 22);
            this.tabClient.Name = "tabClient";
            this.tabClient.Size = new System.Drawing.Size(240, 286);
            this.tabClient.TabIndex = 1;
            this.tabClient.Text = "Client";
            // 
            // checkBoxEnableClient
            // 
            this.checkBoxEnableClient.Location = new System.Drawing.Point(16, 8);
            this.checkBoxEnableClient.Name = "checkBoxEnableClient";
            this.checkBoxEnableClient.Size = new System.Drawing.Size(104, 16);
            this.checkBoxEnableClient.TabIndex = 0;
            this.checkBoxEnableClient.Text = "Enable &Client";
            this.checkBoxEnableClient.CheckedChanged += new System.EventHandler(this.checkEnableClient_CheckedChanged);
            // 
            // ClientGroup
            // 
            this.ClientGroup.Controls.Add(this.editClientPort);
            this.ClientGroup.Controls.Add(this.label6);
            this.ClientGroup.Controls.Add(this.label8);
            this.ClientGroup.Controls.Add(this.editClientHost);
            this.ClientGroup.Controls.Add(this.label7);
            this.ClientGroup.Controls.Add(this.editClientDelayTime);
            this.ClientGroup.Location = new System.Drawing.Point(8, 8);
            this.ClientGroup.Name = "ClientGroup";
            this.ClientGroup.Size = new System.Drawing.Size(224, 272);
            this.ClientGroup.TabIndex = 8;
            this.ClientGroup.TabStop = false;
            // 
            // editClientPort
            // 
            this.editClientPort.Location = new System.Drawing.Point(16, 80);
            this.editClientPort.Name = "editClientPort";
            this.editClientPort.Size = new System.Drawing.Size(56, 20);
            this.editClientPort.TabIndex = 3;
            this.editClientPort.Text = "";
            this.editClientPort.TextChanged += new System.EventHandler(this.editClientPort_TextChanged);
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(16, 64);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(32, 16);
            this.label6.TabIndex = 2;
            this.label6.Text = "&Port:";
            // 
            // label8
            // 
            this.label8.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label8.Location = new System.Drawing.Point(16, 24);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(88, 16);
            this.label8.TabIndex = 0;
            this.label8.Text = "&Host:";
            // 
            // editClientHost
            // 
            this.editClientHost.Location = new System.Drawing.Point(16, 40);
            this.editClientHost.Name = "editClientHost";
            this.editClientHost.Size = new System.Drawing.Size(160, 20);
            this.editClientHost.TabIndex = 1;
            this.editClientHost.Text = "";
            this.editClientHost.TextChanged += new System.EventHandler(this.editClientHost_TextChanged);
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(16, 104);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(144, 16);
            this.label7.TabIndex = 2;
            this.label7.Text = "&Reconnect Wait Time:";
            // 
            // editClientDelayTime
            // 
            this.editClientDelayTime.Location = new System.Drawing.Point(16, 120);
            this.editClientDelayTime.Name = "editClientDelayTime";
            this.editClientDelayTime.Size = new System.Drawing.Size(56, 20);
            this.editClientDelayTime.TabIndex = 3;
            this.editClientDelayTime.Text = "";
            this.editClientDelayTime.TextChanged += new System.EventHandler(this.editClientDelayTime_TextChanged);
            // 
            // tabServer
            // 
            this.tabServer.Controls.Add(this.checkBoxEnableServer);
            this.tabServer.Controls.Add(this.ServerGroup);
            this.tabServer.Location = new System.Drawing.Point(4, 22);
            this.tabServer.Name = "tabServer";
            this.tabServer.Size = new System.Drawing.Size(240, 286);
            this.tabServer.TabIndex = 2;
            this.tabServer.Text = "Server";
            // 
            // checkBoxEnableServer
            // 
            this.checkBoxEnableServer.Location = new System.Drawing.Point(16, 8);
            this.checkBoxEnableServer.Name = "checkBoxEnableServer";
            this.checkBoxEnableServer.Size = new System.Drawing.Size(104, 16);
            this.checkBoxEnableServer.TabIndex = 1;
            this.checkBoxEnableServer.Text = "Enable &Server";
            this.checkBoxEnableServer.CheckedChanged += new System.EventHandler(this.checkBoxEnableServer_CheckedChanged);
            // 
            // ServerGroup
            // 
            this.ServerGroup.Controls.Add(this.editServerPort);
            this.ServerGroup.Controls.Add(this.label1);
            this.ServerGroup.Controls.Add(this.checkBoxEnableWakeup);
            this.ServerGroup.Controls.Add(this.WakeupGroup);
            this.ServerGroup.Location = new System.Drawing.Point(8, 8);
            this.ServerGroup.Name = "ServerGroup";
            this.ServerGroup.Size = new System.Drawing.Size(224, 264);
            this.ServerGroup.TabIndex = 6;
            this.ServerGroup.TabStop = false;
            // 
            // editServerPort
            // 
            this.editServerPort.Location = new System.Drawing.Point(48, 24);
            this.editServerPort.Name = "editServerPort";
            this.editServerPort.Size = new System.Drawing.Size(56, 20);
            this.editServerPort.TabIndex = 1;
            this.editServerPort.Text = "";
            this.editServerPort.TextChanged += new System.EventHandler(this.editServerPort_TextChanged);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(32, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Port:";
            // 
            // checkBoxEnableWakeup
            // 
            this.checkBoxEnableWakeup.Location = new System.Drawing.Point(24, 56);
            this.checkBoxEnableWakeup.Name = "checkBoxEnableWakeup";
            this.checkBoxEnableWakeup.Size = new System.Drawing.Size(104, 16);
            this.checkBoxEnableWakeup.TabIndex = 2;
            this.checkBoxEnableWakeup.Text = "Enable &Wakeup";
            this.checkBoxEnableWakeup.CheckedChanged += new System.EventHandler(this.checkBoxEnableWakeup_CheckedChanged);
            // 
            // WakeupGroup
            // 
            this.WakeupGroup.Controls.Add(this.editWakeupServer);
            this.WakeupGroup.Controls.Add(this.editWakeupCommand);
            this.WakeupGroup.Controls.Add(this.editClosingCommand);
            this.WakeupGroup.Controls.Add(this.editWakeupPort);
            this.WakeupGroup.Controls.Add(this.label5);
            this.WakeupGroup.Controls.Add(this.label2);
            this.WakeupGroup.Controls.Add(this.label4);
            this.WakeupGroup.Controls.Add(this.label3);
            this.WakeupGroup.Location = new System.Drawing.Point(16, 56);
            this.WakeupGroup.Name = "WakeupGroup";
            this.WakeupGroup.Size = new System.Drawing.Size(192, 192);
            this.WakeupGroup.TabIndex = 7;
            this.WakeupGroup.TabStop = false;
            // 
            // editWakeupServer
            // 
            this.editWakeupServer.Location = new System.Drawing.Point(16, 41);
            this.editWakeupServer.Name = "editWakeupServer";
            this.editWakeupServer.Size = new System.Drawing.Size(160, 20);
            this.editWakeupServer.TabIndex = 1;
            this.editWakeupServer.Text = "";
            this.editWakeupServer.TextChanged += new System.EventHandler(this.editWakeupServer_TextChanged);
            // 
            // editWakeupCommand
            // 
            this.editWakeupCommand.Location = new System.Drawing.Point(16, 120);
            this.editWakeupCommand.Name = "editWakeupCommand";
            this.editWakeupCommand.Size = new System.Drawing.Size(160, 20);
            this.editWakeupCommand.TabIndex = 5;
            this.editWakeupCommand.Text = "";
            this.editWakeupCommand.TextChanged += new System.EventHandler(this.editWakeupCommand_TextChanged);
            // 
            // editClosingCommand
            // 
            this.editClosingCommand.Location = new System.Drawing.Point(16, 160);
            this.editClosingCommand.Name = "editClosingCommand";
            this.editClosingCommand.Size = new System.Drawing.Size(160, 20);
            this.editClosingCommand.TabIndex = 7;
            this.editClosingCommand.Text = "";
            this.editClosingCommand.TextChanged += new System.EventHandler(this.editClosingCommand_TextChanged);
            // 
            // editWakeupPort
            // 
            this.editWakeupPort.Location = new System.Drawing.Point(16, 80);
            this.editWakeupPort.Name = "editWakeupPort";
            this.editWakeupPort.Size = new System.Drawing.Size(56, 20);
            this.editWakeupPort.TabIndex = 3;
            this.editWakeupPort.Text = "";
            this.editWakeupPort.TextChanged += new System.EventHandler(this.editWakeupPort_TextChanged);
            // 
            // label5
            // 
            this.label5.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label5.Location = new System.Drawing.Point(16, 144);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(104, 16);
            this.label5.TabIndex = 6;
            this.label5.Text = "Closing Command:";
            // 
            // label2
            // 
            this.label2.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label2.Location = new System.Drawing.Point(16, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(88, 16);
            this.label2.TabIndex = 0;
            this.label2.Text = "Wa&keup Host:";
            // 
            // label4
            // 
            this.label4.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label4.Location = new System.Drawing.Point(16, 104);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(104, 16);
            this.label4.TabIndex = 4;
            this.label4.Text = "Wakeup Command:";
            // 
            // label3
            // 
            this.label3.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label3.Location = new System.Drawing.Point(16, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(48, 16);
            this.label3.TabIndex = 2;
            this.label3.Text = "P&ort:";
            // 
            // checkBoxAutoStart
            // 
            this.checkBoxAutoStart.Location = new System.Drawing.Point(16, 32);
            this.checkBoxAutoStart.Name = "checkBoxAutoStart";
            this.checkBoxAutoStart.Size = new System.Drawing.Size(160, 16);
            this.checkBoxAutoStart.TabIndex = 0;
            this.checkBoxAutoStart.Text = "&Automatically start at login";
            this.checkBoxAutoStart.Visible = false;
            this.checkBoxAutoStart.CheckedChanged += new System.EventHandler(this.checkBoxAutoStart_CheckedChanged);
            // 
            // SettingsDialog
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(242, 352);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "MCE Controller Settings";
            this.tabControl.ResumeLayout(false);
            this.General.ResumeLayout(false);
            this.tabClient.ResumeLayout(false);
            this.ClientGroup.ResumeLayout(false);
            this.tabServer.ResumeLayout(false);
            this.ServerGroup.ResumeLayout(false);
            this.WakeupGroup.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private void buttonCancel_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        private void buttonOK_Click(object sender, System.EventArgs e)
        {
            if (checkBoxAutoStart.Checked)
            {
            }
            else
            {
            }

            Settings.Serialize();
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void checkBoxHideOnStartup_CheckedChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.HideOnStartup = this.checkBoxHideOnStartup.Checked;
        
        }
        private void checkBoxAutoStart_CheckedChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.AutoStart= this.checkBoxAutoStart.Checked;
        }

        private void checkBoxEnableServer_CheckedChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.ActAsServer = checkBoxEnableServer.Checked;

            ServerGroup.Enabled = checkBoxEnableServer.Checked;

        }

        private void editServerPort_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            if (editServerPort.Text.Length > 0)
                Settings.ServerPort = Convert.ToInt32(editServerPort.Text);
        }

        private void checkBoxEnableWakeup_CheckedChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.WakeupEnabled = checkBoxEnableWakeup.Checked;

            WakeupGroup.Enabled = checkBoxEnableWakeup.Checked;

        }

        private void editWakeupServer_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.WakeupHost = editWakeupServer.Text;
        }

        private void editWakeupPort_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            if (editWakeupPort.Text.Length > 0)
                Settings.WakeupPort = Convert.ToInt32(editWakeupPort.Text);
    
        }

        private void editWakeupCommand_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.WakeupCommand = editWakeupCommand.Text;

        }

        private void editClosingCommand_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.ClosingCommand = editClosingCommand.Text;
        }

        private void checkEnableClient_CheckedChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.ActAsClient = checkBoxEnableClient.Checked;

            ClientGroup.Enabled = checkBoxEnableClient.Checked;
        }

        private void editClientPort_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            if (editClientPort.Text.Length > 0)
                Settings.ClientPort = Convert.ToInt32(editClientPort.Text);
        }

        private void editClientHost_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            Settings.ClientHost = editClientHost.Text;
        }

        private void editClientDelayTime_TextChanged(object sender, System.EventArgs e)
        {
            SettingsChanged();
            if (editClientDelayTime.Text.Length > 0)
                Settings.ClientDelayTime = Convert.ToInt32(editClientDelayTime.Text);
        
        }
    }

    public class AppSettings : ICloneable
    {
        private const string SettingsFileName = "MCEContol.settings";

        // TODO: If I were a good programmer these public members would all
        // be properties and I'd keep track of whether something changed
        // within this class.

        // General
        public bool HideOnStartup = false;
        public bool AutoStart = false;
        public int	Opacity = 100;
        public Point WindowLocation = new Point(120, 50);
        public Size WindowSize = new Size(640,400);

        // Client
        public bool ActAsClient = false;
        public int ClientPort = 0;
        public String ClientHost;
        public int ClientDelayTime = 30000;

        // Server
        public bool ActAsServer = true;
        public int ServerPort = 5150;
        public bool WakeupEnabled = false;
        public int WakeupPort;
        public String WakeupHost;
        public String WakeupCommand;
        public String ClosingCommand;

        // Must have a default public constructor so XMLSerialization will work
        // This class is NOT supposed to be creatable (use Deserialize to construct).
        public AppSettings()
        {
        }

        // By default we want the settings file stored with the EXE
        // This allows the app to be run with multiple instances with a settings
        // file for each instance (each being in different directory).
        // However, typical installs get put into to %PROGRAMFILES% which 
        // is ACLd to allow only admin writes on Win7. 
        public static String GetSettingsPath()
        {
            String path = Application.StartupPath;
            if (HasWritePermissionOnDir(path))
                return path;
            else
            {
                // Strip off the trainling version ("\\0.0.0.xxxx");
                path = Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1));
                if (HasWritePermissionOnDir(path))
                    return path;
            }
            return "";
        }

        public void Serialize()
        {
            String SettingsPath = GetSettingsPath();
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(AppSettings));
                StreamWriter sw = new StreamWriter(SettingsPath + "\\" + SettingsFileName);
                ser.Serialize(sw, this);
                sw.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(String.Format("Settings file could not be written. {0}", e.Message));
            }
        }

        public static AppSettings Deserialize(String SettingsPath)
        {
            XmlSerializer serializer = null;
            XmlReader reader = null;
            AppSettings Settings= null;

            serializer = new XmlSerializer(typeof(AppSettings));
            // A FileStream is needed to read the XML document.
            try
            {
                FileStream fs = new FileStream(SettingsPath + "\\" + SettingsFileName, FileMode.Open, FileAccess.Read);
                reader = new XmlTextReader(fs);
                Settings = (AppSettings) serializer.Deserialize(reader);
                fs.Close();
            }
            catch(System.IO.FileNotFoundException)
            {
                // First time through, so create file with defaults
                Settings = new AppSettings();
                Settings.Serialize();
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show(String.Format("Settings file could not be loaded. {0}", e.Message));
            }
            return Settings;
        }

        public static bool HasWritePermissionOnDir(string path)
        {
            try
            {
                using (StreamWriter sw = File.CreateText(path + "\\" + "~test.tmp")) {}
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                File.Delete(path + "\\" + "~test.tmp");
            }
            return true;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

    }
}
