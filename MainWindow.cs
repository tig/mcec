//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Windows.Forms;
using MCEControl.Properties;
using Microsoft.Win32.Security;
using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;

namespace MCEControl {
    /// <summary>
    /// Summary description for MainWindow.
    /// </summary>
    public class MainWindow : Form {
        public CommandTable CmdTable;
        private log4net.ILog log4;

        // Persisted application settings
        private AppSettings settings;

        // Protocol objects
        private SocketServer _server;
        private SocketClient _client;
        private SerialServer _serialServer;

        // Indicates whether user hit the close box (minimize)
        // or the app is exiting
        private bool _shuttingDown;

        private CommandWindow _cmdWindow;

        // Window controls
        private MainMenu _mainMenu;
        private MenuItem _menuItemFileMenu;
        private MenuItem _menuItemExit;
        private MenuItem _menuItemHelpMenu;
        private MenuItem _menuItemAbout;
        private NotifyIcon _notifyIcon;
        private TextBox _log;
        private ContextMenu _notifyMenu;
        private MenuItem _notifyMenuItemExit;
        private IContainer components;
        private MenuItem _menuItemSendAwake;
        private MenuItem _menuSeparator2;
        private MenuItem _menuSeparator1;
        private MenuItem _menuSettings;
        private MenuItem _menuSeparator5;
        private MenuItem _notifyMenuItemSettings;
        private MenuItem _menuSeparator4;
        private MenuItem _notifyMenuViewStatus;
        private MenuItem _menuItemHelp;
        private MenuItem _menuItemEditCommands;
        private MenuItem menuItem2;
        private MenuItem menuItem1;
        private MenuItem _menuItemCheckVersion;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusStripStatus;
        private ToolStripStatusLabel statusStripClient;
        private ToolStripStatusLabel statusStripServer;
        private ToolStripStatusLabel statusStripSerial;

        public SocketClient Client {
            get { return _client; }
        }

        // MainWindow is a singleton
        private static readonly Lazy<MainWindow> lazy = new Lazy<MainWindow>(() => new MainWindow());
        public static MainWindow Instance { get { return lazy.Value; } }

        public AppSettings Settings { get => settings; set => settings = value; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args) {
            if (!IsNet45OrNewer()) {
                MessageBox.Show(
                    "MCE Controller requires .NET Framework 4.5 or newer.\r\n\r\nDownload and install from http://www.microsoft.com/net/");
                return;
            }

            // AutoScaleMode for highdpi displays
            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load AppSettings
            Instance.Settings = AppSettings.Deserialize(AppSettings.GetSettingsPath());

            Application.Run(Instance);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public static bool IsNet45OrNewer() {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        public MainWindow() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();
            _notifyIcon.Icon = Icon;

            ShowInTaskbar = true;

            SetStatus("");
            _menuItemSendAwake.Enabled = false;
        }


        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (disposing) {
                // When the app exits we need to un-shift any modify keys that might
                // have been pressed or they'll still be stuck after exit
                SendInputCommand.ShiftKey("shift", false);
                SendInputCommand.ShiftKey("ctrl", false);
                SendInputCommand.ShiftKey("alt", false);
                SendInputCommand.ShiftKey("lwin", false);
                SendInputCommand.ShiftKey("rwin", false);

                if (components != null) {
                    components.Dispose();
                }

                if (_server != null) {
                    // remove our notification handler
                    _server.Notifications -= ServerSocketCallbackHandler;
                    _server.Dispose();
                }
                if (_client != null) {
                    // remove our notification handler
                    _client.Notifications -= ClientSocketNotificationHandler;
                    _client.Dispose();
                }

                if (_serialServer != null) {
                    _serialServer.Notifications -= HandleSerialServerNotifications;
                    _serialServer.Dispose();
                }
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this._mainMenu = new System.Windows.Forms.MainMenu(this.components);
            this._menuItemFileMenu = new System.Windows.Forms.MenuItem();
            this._menuItemSendAwake = new System.Windows.Forms.MenuItem();
            this._menuSeparator1 = new System.Windows.Forms.MenuItem();
            this._menuItemEditCommands = new System.Windows.Forms.MenuItem();
            this._menuSeparator2 = new System.Windows.Forms.MenuItem();
            this._menuItemExit = new System.Windows.Forms.MenuItem();
            this._menuSettings = new System.Windows.Forms.MenuItem();
            this._menuItemHelpMenu = new System.Windows.Forms.MenuItem();
            this._menuItemHelp = new System.Windows.Forms.MenuItem();
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this._menuItemCheckVersion = new System.Windows.Forms.MenuItem();
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this._menuItemAbout = new System.Windows.Forms.MenuItem();
            this._notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this._notifyMenu = new System.Windows.Forms.ContextMenu();
            this._notifyMenuViewStatus = new System.Windows.Forms.MenuItem();
            this._menuSeparator4 = new System.Windows.Forms.MenuItem();
            this._notifyMenuItemSettings = new System.Windows.Forms.MenuItem();
            this._menuSeparator5 = new System.Windows.Forms.MenuItem();
            this._notifyMenuItemExit = new System.Windows.Forms.MenuItem();
            this._log = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusStripStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripClient = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripServer = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripSerial = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // _mainMenu
            // 
            this._mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this._menuItemFileMenu,
            this._menuSettings,
            this._menuItemHelpMenu});
            // 
            // _menuItemFileMenu
            // 
            this._menuItemFileMenu.Index = 0;
            this._menuItemFileMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this._menuItemSendAwake,
            this._menuSeparator1,
            this._menuItemEditCommands,
            this._menuSeparator2,
            this._menuItemExit});
            this._menuItemFileMenu.Text = "&File";
            // 
            // _menuItemSendAwake
            // 
            this._menuItemSendAwake.Index = 0;
            this._menuItemSendAwake.Text = "Send &Awake Signal";
            this._menuItemSendAwake.Click += new System.EventHandler(this.MenuItemSendAwakeClick);
            // 
            // _menuSeparator1
            // 
            this._menuSeparator1.Index = 1;
            this._menuSeparator1.Text = "-";
            // 
            // _menuItemEditCommands
            // 
            this._menuItemEditCommands.Index = 2;
            this._menuItemEditCommands.Text = "&Open .commands file location...";
            this._menuItemEditCommands.Click += new System.EventHandler(this.MenuItemEditCommandsClick);
            // 
            // _menuSeparator2
            // 
            this._menuSeparator2.Index = 3;
            this._menuSeparator2.Text = "-";
            // 
            // _menuItemExit
            // 
            this._menuItemExit.Index = 4;
            this._menuItemExit.Text = "E&xit";
            this._menuItemExit.Click += new System.EventHandler(this.MenuItemExitClick);
            // 
            // _menuSettings
            // 
            this._menuSettings.Index = 1;
            this._menuSettings.Text = "&Settings";
            this._menuSettings.Click += new System.EventHandler(this.MenuSettingsClick);
            // 
            // _menuItemHelpMenu
            // 
            this._menuItemHelpMenu.Index = 2;
            this._menuItemHelpMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this._menuItemHelp,
            this.menuItem2,
            this._menuItemCheckVersion,
            this.menuItem1,
            this._menuItemAbout});
            this._menuItemHelpMenu.Text = "&Help";
            // 
            // _menuItemHelp
            // 
            this._menuItemHelp.Index = 0;
            this._menuItemHelp.Shortcut = System.Windows.Forms.Shortcut.F1;
            this._menuItemHelp.Text = "&Wiki";
            this._menuItemHelp.Click += new System.EventHandler(this.MenuItemHelpClick);
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 1;
            this.menuItem2.Text = "-";
            // 
            // _menuItemCheckVersion
            // 
            this._menuItemCheckVersion.Index = 2;
            this._menuItemCheckVersion.Text = "&Check for updates...";
            this._menuItemCheckVersion.Click += new System.EventHandler(this.menuItemCheckVersion_Click);
            // 
            // menuItem1
            // 
            this.menuItem1.Index = 3;
            this.menuItem1.Text = "-";
            // 
            // _menuItemAbout
            // 
            this._menuItemAbout.Index = 4;
            this._menuItemAbout.Text = "&About...";
            this._menuItemAbout.Click += new System.EventHandler(this.MenuItemAboutClick);
            // 
            // _notifyIcon
            // 
            this._notifyIcon.ContextMenu = this._notifyMenu;
            this._notifyIcon.Text = global::MCEControl.Properties.Resources.App_FullName;
            this._notifyIcon.Visible = true;
            this._notifyIcon.DoubleClick += new System.EventHandler(this.NotifyIconDoubleClick);
            // 
            // _notifyMenu
            // 
            this._notifyMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this._notifyMenuViewStatus,
            this._menuSeparator4,
            this._notifyMenuItemSettings,
            this._menuSeparator5,
            this._notifyMenuItemExit});
            // 
            // _notifyMenuViewStatus
            // 
            this._notifyMenuViewStatus.Index = 0;
            this._notifyMenuViewStatus.Text = "&View Status...";
            this._notifyMenuViewStatus.Click += new System.EventHandler(this.NotifyIconDoubleClick);
            // 
            // _menuSeparator4
            // 
            this._menuSeparator4.Index = 1;
            this._menuSeparator4.Text = "-";
            // 
            // _notifyMenuItemSettings
            // 
            this._notifyMenuItemSettings.Index = 2;
            this._notifyMenuItemSettings.Text = "&Settings...";
            this._notifyMenuItemSettings.Click += new System.EventHandler(this.MenuSettingsClick);
            // 
            // _menuSeparator5
            // 
            this._menuSeparator5.Index = 3;
            this._menuSeparator5.Text = "-";
            // 
            // _notifyMenuItemExit
            // 
            this._notifyMenuItemExit.Index = 4;
            this._notifyMenuItemExit.Text = "&Exit";
            this._notifyMenuItemExit.Click += new System.EventHandler(this.MenuItemExitClick);
            // 
            // _log
            // 
            this._log.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._log.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._log.Font = new System.Drawing.Font("Lucida Console", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this._log.Location = new System.Drawing.Point(0, 0);
            this._log.Multiline = true;
            this._log.Name = "_log";
            this._log.Size = new System.Drawing.Size(645, 344);
            this._log.TabIndex = 1;
            this._log.WordWrap = false;
            this._log.TextChanged += new System.EventHandler(this.LogTextChanged);
            this._log.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.LogKeyPress);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusStripStatus,
            this.statusStripClient,
            this.statusStripServer,
            this.statusStripSerial});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 345);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.ShowItemToolTips = true;
            this.statusStrip.Size = new System.Drawing.Size(645, 22);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "MCE Controller";
            // 
            // statusStripStatus
            // 
            this.statusStripStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusStripStatus.DoubleClickEnabled = true;
            this.statusStripStatus.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripStatus.Name = "statusStripStatus";
            this.statusStripStatus.Size = new System.Drawing.Size(123, 17);
            this.statusStripStatus.Text = "MCE Controller Status";
            this.statusStripStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripStatus.Click += new System.EventHandler(this.statusStripStatus_Click);
            // 
            // statusStripClient
            // 
            this.statusStripClient.DoubleClickEnabled = true;
            this.statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripClient.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripClient.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripClient.Name = "statusStripClient";
            this.statusStripClient.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripClient.Size = new System.Drawing.Size(54, 17);
            this.statusStripClient.Text = "Client";
            this.statusStripClient.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripClient.DoubleClick += new System.EventHandler(this.statusStripClient_Click);
            // 
            // statusStripServer
            // 
            this.statusStripServer.DoubleClickEnabled = true;
            this.statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripServer.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripServer.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripServer.Name = "statusStripServer";
            this.statusStripServer.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripServer.Size = new System.Drawing.Size(55, 17);
            this.statusStripServer.Text = "Server";
            this.statusStripServer.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripServer.DoubleClick += new System.EventHandler(this.statusStripServer_Click);
            // 
            // statusStripSerial
            // 
            this.statusStripSerial.DoubleClickEnabled = true;
            this.statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripSerial.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripSerial.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripSerial.Name = "statusStripSerial";
            this.statusStripSerial.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripSerial.Size = new System.Drawing.Size(51, 17);
            this.statusStripSerial.Text = "Serial";
            this.statusStripSerial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripSerial.DoubleClick += new System.EventHandler(this.statusStripSerial_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 15);
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(645, 367);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this._log);
            this.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.HelpButton = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Menu = this._mainMenu;
            this.MinimizeBox = false;
            this.Name = "MainWindow";
            this.Text = "MCE Controller";
            this.HelpButtonClicked += new System.ComponentModel.CancelEventHandler(this.MainWindow_HelpButtonClicked);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.MainWindowClosing);
            this.Load += new System.EventHandler(this.MainWindowLoad);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        protected override void WndProc(ref Message m) {
            // If the session is being logged off, or the machine is shutting
            // down...
            if (m.Msg == 0x11) // WM_QUERYENDSESSION
            {
                // Allow shut down (m.Result may already be non-zero, but I set it
                // just in case)
                m.Result = (IntPtr)1;

                // Indicate to MainWindow_Closing() that we are shutting down;
                // otherwise it will just minimize to the tray
                _shuttingDown = true;
            }
            base.WndProc(ref m);
        }

        private void MainWindowLoad(object sender, EventArgs e) {
            CheckVersion();
            // Location can not be changed in constructor, has to be done here
            Location = Settings.WindowLocation;
            Size = Settings.WindowSize;

            CmdTable = CommandTable.Deserialize(Settings.DisableInternalCommands);
            if (CmdTable == null) {
                MessageBox.Show(this, Resources.MCEController_commands_read_error, Resources.App_FullName);
                _notifyIcon.Visible = false;
                Opacity = 100;
            }
            else {
                AddLogEntry($"MCEC: {CmdTable.NumCommands} commands available.");
                Opacity = (double)Settings.Opacity / 100;

                if (Settings.HideOnStartup) {
                    Opacity = 0;
                    Win32.PostMessage(Handle, (UInt32)WM.SYSCOMMAND, (UInt32)SC.CLOSE, 0);
                }
            }

            if (_cmdWindow == null)
                _cmdWindow = new CommandWindow();
            //_cmdWindow.Visible = Settings.ShowCommandWindow;

            //var t = new System.Timers.Timer() {
            //    AutoReset = false,
            //    Interval = 2000
            //};
            //t.Elapsed += (sender, args) => Start();
            //AddLogEntry("Starting services...");
            //t.Start();
            SetStatus($"MCE Controller version: {Application.ProductVersion}");
            Start();
        }

        private void MainWindowClosing(object sender, CancelEventArgs e) {
            if (!_shuttingDown) {
                // If we're NOT shutting down (the user hit the close button or pressed
                // CTRL-F4) minimize to tray.
                e.Cancel = true;

                // Hide the form and make sure the taskbar icon is visible
                _notifyIcon.Visible = true;
                Hide();
            }
        }

        private void CheckVersion() {
            AddLogEntry($"MCEC: Version: {Application.ProductVersion}");
            var lv = new LatestVersion();
            lv.GetLatestStableVersionAsync((o, version) => {
                if (version == null && !String.IsNullOrWhiteSpace(lv.ErrorMessage)) {
                    AddLogEntry(
                        $"MCEC: Could not access tig.github.io/mcec to see if a newer version is available. {lv.ErrorMessage}");
                }
                else if (lv.CompareVersions() < 0) {
                    AddLogEntry(
                        $"MCEC: A newer version of MCE Controller ({version}) is available at tig.github.io/mcec.");
                }
                else if (lv.CompareVersions() > 0) {
                    AddLogEntry(
                        $"MCEC: You are are running a MORE recent version than can be found at tig.github.io/mcec ({version}).");
                }
                else {
                    AddLogEntry("MCEC: You are running the most recent version of MCE Controller.");
                }
            });
        }

        private void Start() {
            SetServerStatus(ServiceStatus.Stopped);
            if (Settings.ActAsServer)
                StartServer();

            SetSerialStatus(ServiceStatus.Stopped);
            if (Settings.ActAsSerialServer)
                StartSerialServer();

            SetClientStatus(ServiceStatus.Stopped);
            if (Settings.ActAsClient)
                StartClient();

            if (Settings.ActivityMonitorEnabled)
                UserActivityMonitor.Instance.Start(Settings.ActivityMonitorCommand, Settings.ActivityMonitorDebounceTime);
        }

        private delegate void StopCallback();
        private void Stop() {
            if (_cmdWindow != null) {
                if (this.InvokeRequired)
                    this.BeginInvoke((StopCallback)Stop);
                else {
                    UserActivityMonitor.Instance.Stop();
                    StopClient();
                    StopServer();
                    StopSerialServer();
                }
            }
        }

        public void ShutDown() {
            AddLogEntry("ShutDown");
            _shuttingDown = true;

            Stop();

            // hide icon from the systray
            _notifyIcon.Visible = false;

            // Save the window size/location
            Settings.WindowLocation = Location;
            Settings.WindowSize = Size;
            Settings.Serialize();

            Close();
            Application.Exit();
        }

        private void StartServer() {
            if (_server == null) {
                AddLogEntry("Server: Starting...");
                _server = new SocketServer();
                _server.Notifications += ServerSocketCallbackHandler;
                _server.Start(Settings.ServerPort);
                _menuItemSendAwake.Enabled = Settings.WakeupEnabled;
            }
            else
                AddLogEntry("MCEC: Attempt to StartServer() while an instance already exists!");
        }

        private void StopServer() {
            if (_server != null) {
                AddLogEntry("Server: Stopping..");
                // remove our notification handler
                _server.Stop();
                _server = null;
                _menuItemSendAwake.Enabled = false;
            }
        }

        private void ToggleServer() {
            if (_server == null)
                StartServer();
            else
                StopServer();
        }

        private void StartSerialServer() {
            if (_serialServer == null) {
                AddLogEntry("Serial: Starting..");
                _serialServer = new SerialServer();
                _serialServer.Notifications += HandleSerialServerNotifications;
                _serialServer.Start(Settings.SerialServerPortName,
                    Settings.SerialServerBaudRate,
                    Settings.SerialServerParity,
                    Settings.SerialServerDataBits,
                    Settings.SerialServerStopBits,
                    Settings.SerialServerHandshake);
            }
            else
                AddLogEntry("Serial: Attempt to StartSerialServer() while an instance already exists!");
        }

        private void StopSerialServer() {
            if (_serialServer != null) {
                AddLogEntry("Serial: Stopping..");
                // remove our notification handler
                _serialServer.Stop();
                _serialServer = null;
            }
        }

        private void StartClient(bool delay = false) {
            if (_client == null) {
                AddLogEntry("Client: Starting..");
                _client = new SocketClient(Settings);
                _client.Notifications += ClientSocketNotificationHandler;
                _client.Start(delay);
            }
        }

        private void StopClient() {
            if (_client != null) {
                _cmdWindow.Visible = false;
                AddLogEntry("Client: Stopping..");
                _client.Stop();
                _client = null;
            }
        }

        private void ToggleClient() {
            if (_client == null)
                StartClient();
            else
                StopClient();
        }
        private delegate void RestartClientCallback();

        private void RestartClient() {
            if (_cmdWindow != null) {
                if (this.InvokeRequired)
                    this.BeginInvoke((RestartClientCallback)RestartClient);
                else {
                    StopClient();
                    if (!_shuttingDown && Settings.ActAsClient && Settings.ClientDelayTime > 0) {
                        AddLogEntry("Client: Reconnecting..");
                        StartClient(true);
                    }
                }
            }
        }

        private delegate void ShowCommandWindowCallback();

        private void ShowCommandWindow() {
            if (!settings.ShowCommandWindow) return;
            if (this.InvokeRequired)
                this.BeginInvoke((ShowCommandWindowCallback)ShowCommandWindow);
            else {
                _cmdWindow.Visible = Settings.ShowCommandWindow;
            }
        }

        private delegate void HideCommandWindowCallback();

        private void HideCommandWindow() {
            if (this.InvokeRequired)
                this.BeginInvoke((HideCommandWindowCallback)HideCommandWindow);
            else {
                _cmdWindow.Visible = false;
            }
        }

        private void ReceivedData(Reply reply, String cmd) {
            try {
                CmdTable.Execute(reply, cmd);
            }
            catch (Exception e) {
                AddLogEntry($"Command: ({cmd}) error: {e}");
            }
        }

        // Sends a line of text (adds a "\n" to end) to connected client and server
        internal void SendLine(string v) {
            //AddLogEntry($"Send: {v}");
            if (_client != null)
                _client.Send(v + "\n");

            if (_server != null)
                _server.Send(v + "\n");

            if (_serialServer != null)
                _serialServer.Send(v + "\n");
        }

        private delegate void SetStatusCallback(string text);

        private void SetStatus(string text) {
            if (statusStrip.InvokeRequired) {
                statusStrip.BeginInvoke((SetStatusCallback)SetStatus, new object[] { text });
            }
            else {
                statusStripStatus.Text = text;
                _notifyIcon.Text = text;
            }
        }

        private delegate void SetServerStatusCallback(ServiceStatus status);
        private void SetServerStatus(ServiceStatus status) {
            if (statusStrip.InvokeRequired) {
                statusStrip.BeginInvoke((SetServerStatusCallback)SetServerStatus, new object[] { status });
            }
            else {
                statusStripServer.Text = $"Server on port {settings.ServerPort}";
                switch (status) {
                    case ServiceStatus.Started:
                        statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Waiting:
                        statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Connected:
                        statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
                        return;

                    case ServiceStatus.Stopped:
                        statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_gray_icon;
                        break;
                }
            }
        }

        private delegate void SetClientStatusCallback(ServiceStatus status);
        private void SetClientStatus(ServiceStatus status) {
            if (statusStrip.InvokeRequired) {
                statusStrip.BeginInvoke((SetClientStatusCallback)SetClientStatus, new object[] { status });
            }
            else {
                statusStripClient.Text = $"Client {settings.ClientHost}:{Settings.ClientPort}";
                switch (status) {
                    case ServiceStatus.Started:
                        statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Waiting:
                        statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Connected:
                        statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
                        return;

                    case ServiceStatus.Stopped:
                        statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_gray_icon;
                        break;
                }
            }
        }

        private delegate void SetSerialStatusCallback(ServiceStatus status);
        private void SetSerialStatus(ServiceStatus status) {
            if (statusStrip.InvokeRequired) {
                statusStrip.BeginInvoke((SetSerialStatusCallback)SetSerialStatus, new object[] { status });
            }
            else {
                // https://en.wikipedia.org/wiki/8-N-1
                statusStripSerial.Text = $"Serial {settings.SerialServerBaudRate}/{settings.SerialServerPortName} {settings.SerialServerDataBits}-{settings.SerialServerParity}-{settings.SerialServerStopBits}-{settings.SerialServerHandshake}";
                switch (status) {
                    case ServiceStatus.Started:
                        statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Waiting:
                        statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_red_icon;
                        break;

                    case ServiceStatus.Connected:
                        statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
                        return;

                    case ServiceStatus.Stopped:
                        statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_gray_icon;
                        break;
                }
            }
        }

        //
        // Notify callback for the TCP/IP Server
        //
        public void ServerSocketCallbackHandler(ServiceNotification notification, ServiceStatus status, Reply reply, String msg) {
            if (notification == ServiceNotification.StatusChange)
                HandleSocketServerStatusChange(status);
            else
                HandleSocketServerNotification(notification, status, (SocketServer.ServerReplyContext)reply, msg);
        }

        private void HandleSocketServerNotification(ServiceNotification notification, ServiceStatus status,
            SocketServer.ServerReplyContext serverReplyContext, String msg) {
            String s = "";

            switch (notification) {
                case ServiceNotification.ReceivedData:
                    Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null, notification.ToString());
                    s = $"Server: Received from Client #{serverReplyContext.ClientNumber} at {serverReplyContext.Socket.RemoteEndPoint}: {msg}";
                    AddLogEntry(s);
                    ReceivedData(serverReplyContext, msg);
                    return;

                case ServiceNotification.Write:
                    Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null, notification.ToString());
                    s = $"Wrote to Client #{serverReplyContext.ClientNumber} at {serverReplyContext.Socket.RemoteEndPoint}: {msg}";
                    break;

                case ServiceNotification.WriteFailed:
                    Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null, notification.ToString());
                    s = $"Write failed to Client #{serverReplyContext.ClientNumber} at {serverReplyContext.Socket.RemoteEndPoint}: {msg}";
                    break;

                case ServiceNotification.ClientConnected:
                    Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null, notification.ToString());
                    s = $"Client #{serverReplyContext.ClientNumber} at {serverReplyContext.Socket.RemoteEndPoint} connected.";
                    break;

                case ServiceNotification.ClientDisconnected:
                    Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null, notification.ToString());
                    s = $"Client #{serverReplyContext.ClientNumber} at {serverReplyContext.Socket.RemoteEndPoint} has disconnected.";
                    break;

                case ServiceNotification.Wakeup:
                    s = $"Wakeup: {(string)msg}";
                    break;

                case ServiceNotification.Error:
                    switch (status) {
                        case ServiceStatus.Waiting:
                        case ServiceStatus.Stopped:
                        case ServiceStatus.Sleeping:
                            s = $"{status}: {msg}";
                            break;

                        case ServiceStatus.Connected:
                            if (serverReplyContext != null) {
                                Debug.Assert(serverReplyContext.Socket != null);
                                Debug.Assert(serverReplyContext.Socket.RemoteEndPoint != null);
                                s = $"(Client #{serverReplyContext.ClientNumber} at {(serverReplyContext.Socket == null ? "n/a" : serverReplyContext.Socket.RemoteEndPoint.ToString())}): {msg}";
                            }
                            else
                                s = msg;
                            break;
                    }
                    s = "Error " + s;
                    break;

                default:
                    s = "Unknown notification: " + notification;
                    break;
            }
            AddLogEntry($"Server: {s}");
        }

        private void HandleSocketServerStatusChange(ServiceStatus status) {
            SetServerStatus(status);
            String s = "";
            switch (status) {
                case ServiceStatus.Started:
                    s = $"TCP/IP server started on port {Settings.ServerPort}";
                    //SetStatus(s);
                    if (Settings.WakeupEnabled)
                        _server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost,
                            Settings.WakeupPort);
                    break;

                case ServiceStatus.Waiting:
                    s = "Waiting for a client to connect";
                    break;

                case ServiceStatus.Connected:
                    //SetStatus("Clients connected, waiting for commands...");
                    return;

                case ServiceStatus.Stopped:
                    s = "Stopped.";
                    //SetStatus("Client/Sever Not Active");
                    if (Settings.WakeupEnabled)
                        _server.SendAwakeCommand(Settings.ClosingCommand, Settings.WakeupHost,
                            Settings.WakeupPort);
                    break;
            }
            AddLogEntry($"Server: {s}");
        }

        //
        // Notify callback for the TCP/IP Client
        //
        public void ClientSocketNotificationHandler(ServiceNotification notify, ServiceStatus status, Reply reply, String msg) {
            SetClientStatus(status);
            String s = null;
            switch (notify) {
                case ServiceNotification.StatusChange:
                    if (status == ServiceStatus.Started) {
                        Debug.WriteLine("ClientSocketNotificationHandler - ServiceStatus.Started");
                        s = $"Connecting to {Settings.ClientHost}:{Settings.ClientPort}";
                        //SetStatus(s);
                        HideCommandWindow();
                    }
                    else if (status == ServiceStatus.Connected) {
                        Debug.WriteLine("ClientSocketNotificationHandler - ServiceStatus.Connected");
                        s = $"Connected to {Settings.ClientHost}:{Settings.ClientPort}";
                        //SetStatus(s);
                        ShowCommandWindow();
                    }
                    else if (status == ServiceStatus.Stopped) {
                        Debug.WriteLine("ClientSocketNotificationHandler - ServiceStatus.Stopped");
                        s = "Stopped.";
                        //SetStatus("Client/Sever Not Active");
                        HideCommandWindow();
                    }
                    else if (status == ServiceStatus.Sleeping) {
                        Debug.WriteLine("ClientSocketNotificationHandler - ServiceStatus.Sleeping");
                        s = $"Waiting {(Settings.ClientDelayTime / 1000)} seconds to connect.";
                        //SetStatus(s);
                        HideCommandWindow();
                    }
                    break;

                case ServiceNotification.ReceivedData:
                    AddLogEntry($"Client: Received; {msg}");
                    ReceivedData(reply, (string)msg);
                    return;

                case ServiceNotification.Error:
                    Debug.WriteLine($"ClientSocketNotificationHandler - ServiceStatus.Error: {(string)msg}");
                    AddLogEntry($"Client: Error; {(string)msg}");
                    RestartClient();
                    return;

                default:
                    s = "Unknown notification";
                    break;
            }
            AddLogEntry($"Client: {s}");
        }

        //
        // Notify callback for the Serial Server
        //
        public void HandleSerialServerNotifications(ServiceNotification notify, ServiceStatus status, Reply reply, String msg) {
            SetSerialStatus(status);
            String s = null;
            switch (notify) {
                case ServiceNotification.StatusChange:
                    switch (status) {
                        case ServiceStatus.Started:
                            s = $"SerialServer: Opening port: {msg}";
                            break;

                        case ServiceStatus.Waiting:
                            s = $"SerialServer: Waiting for commands on {msg}...";
                            //SetStatus("Waiting for Serial commands...");
                            break;

                        case ServiceStatus.Stopped:
                            s = "SerialServer: Stopped.";
                            //SetStatus("Serial Server Not Active");
                            break;
                    }
                    break;

                case ServiceNotification.ReceivedData:
                    AddLogEntry($"SerialServer: Received: {msg}");
                    ReceivedData(reply, (string)msg);
                    return;

                case ServiceNotification.Error:
                    s = $"SerialServer: Error: {msg}";
                    break;

                default:
                    s = "SerialServer: Unknown notification";
                    break;
            }
            AddLogEntry(s);
        }

        public static void AddLogEntry(String text) {
            if (Instance == null) return;

            if (Instance.log4 == null) {
                string logFile = Environment.CurrentDirectory + @"\MCEControl.log";
                if (Environment.CurrentDirectory.Contains("Program Files (x86)"))
                    logFile = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Kindel Systems\MCE Controller\MCEControl.log";
                Logger.Setup(logFile);
                Instance.log4 = log4net.LogManager.GetLogger("MCEControl");
                AddLogEntry($"MCEC: Log file being written to {logFile}");
            }
            Instance.log4.Info(text);
            // Can only update the log in the main window when on the UI thread
            if (Instance.InvokeRequired || Instance._log.InvokeRequired)
                Instance.BeginInvoke((AddLogEntryUiThreadCallback)AddLogEntryUiThread, new object[] { text });
            else {
                AddLogEntryUiThread(text);
            }
        }

        private delegate void AddLogEntryUiThreadCallback(string text);
        private static void AddLogEntryUiThread(String text) {
            Instance._log.AppendText("["
                                     + DateTime.Now.ToString("yy'-'MM'-'dd' 'HH':'mm':'ss")
                                     + "] "
                                     + text
                                     + Environment.NewLine);
        }

        private void MenuItemExitClick(object sender, EventArgs e) {
            ShutDown();
        }

        private void NotifyIconDoubleClick(object sender, EventArgs e) {
            // Show the form when the user double clicks on the notify icon.

            // Set the WindowState to normal if the form is minimized.
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            // Activate the form.
            _notifyIcon.Visible = false;
            Activate();
            Show();
            Opacity = (double)Settings.Opacity / 100;
        }

        private void MenuItemAboutClick(object sender, EventArgs e) {
            var a = new AboutBox();
            a.ShowDialog(this);
        }

        private void MenuSettingsClick(object sender, EventArgs e) {
            ShowSettings("General");
        }

        private void ShowSettings(string defaultTabName) {
            var d = new SettingsDialog(Settings);
            d.DefaultTab = defaultTabName;

            if (d.ShowDialog(this) == DialogResult.OK) {
                Settings = d.Settings;

                Opacity = (double)Settings.Opacity / 100;

                Stop();
                Start();
            }
        }

        // Prevent input into the edit box
        private void LogKeyPress(object sender, KeyPressEventArgs e) {
            e.Handled = true;
        }

        // Keep the end of the log visible and prevent it from overflowing
        private void LogTextChanged(object sender, EventArgs e) {
            // We don't want to overrun the size a textbox can handle
            // limit to 16k
            if (_log.TextLength > (16 * 1024)) {
                _log.Text = _log.Text.Remove(0, _log.Text.IndexOf("\r\n", StringComparison.Ordinal) + 2);
                _log.Select(_log.TextLength, 0);
            }
            _log.ScrollToCaret();
        }

        private void MenuItemSendAwakeClick(object sender, EventArgs e) {
            _server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
        }

        private void MenuItemHelpClick(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void MenuItemSupportClick(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void MenuItemEditCommandsClick(object sender, EventArgs e) {
            Process.Start(Application.StartupPath);
        }

        private void MainWindow_HelpButtonClicked(object sender, CancelEventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void menuItemCheckVersion_Click(object sender, EventArgs e) {
            CheckVersion();
        }

        private void statusStripClient_Click(object sender, EventArgs e) {
            //ShowSettings("Client");
            ToggleClient();
        }

        private void statusStripServer_Click(object sender, EventArgs e) {
            //ShowSettings("Server");
            ToggleServer();
        }

        private void statusStripSerial_Click(object sender, EventArgs e) {
            ShowSettings("Serial");
        }

        private void statusStripStatus_Click(object sender, EventArgs e) {
            ShowSettings("General");
        }
    }

    public class Logger {
        public static void Setup(string logFile) {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date %-5level - %message%newline";
            patternLayout.ActivateOptions();

            RollingFileAppender roller = new RollingFileAppender {
                AppendToFile = true,
                Layout = patternLayout,
                MaxSizeRollBackups = 5,
                MaximumFileSize = "100KB",
                RollingStyle = RollingFileAppender.RollingMode.Size,
                StaticLogFileName = true,
                File = logFile
            };

            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            //MemoryAppender memory = new MemoryAppender();
            //memory.ActivateOptions();
            //hierarchy.Root.AddAppender(memory);

            var debugAppender = new ConsoleAppender { Layout = patternLayout };
            debugAppender.ActivateOptions();
            hierarchy.Root.AddAppender(debugAppender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }
    }
}
