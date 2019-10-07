//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
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
using System.Drawing;

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
            // https://docs.microsoft.com/en-us/dotnet/framework/winforms/high-dpi-support-in-windows-forms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start logging
            Logger.Instance.LogFile = $@"{ConfigPath}MCEControl.log";
            Logger.Instance.Log4.Debug("Main");

            // TODO: Update to check for 4.7 or newer
            if (!IsNet45OrNewer()) {
                MessageBox.Show(
                    "MCE Controller requires .NET Framework 4.7 or newer.\r\n\r\nDownload and install from http://www.microsoft.com/net/");
                return;
            }

            // Load AppSettings
            Instance.Settings = AppSettings.Deserialize($@"{ConfigPath}{AppSettings.SettingsFileName}");

            Application.Run(Instance);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public static bool IsNet45OrNewer() {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        // If running from default install location (in Program Files) find the
        // .commands, .settings, and .log files in %appdata%. Otherwise find them in the 
        // directory MCEControl.exe was run from.
        private static string ConfigPath {
            get {
                // Get dir of mcecontrol.exe
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                // is this in Program Files?
                if (path.Contains(programfiles)) {
                    // We're running from the default install location. Use %appdata%.
                    // strip % programfiles %
                    path = $@"{appdata}\{path.Substring(programfiles.Length + 1)}";
                }
                return path;
            }
        }

        public MainWindow() {
            log4 = Logger.Instance.Log4;
            
            InitializeComponent();
            
            //Font = SystemFonts.DefaultFont;
            //= SystemFonts.MenuFont;
            //statusStrip.Font = SystemFonts.StatusFont;
            //_log.Font = SystemFonts.DefaultFont;
            //_log.Font = new System.Drawing.Font("Lucida Console", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);

            Logger.Instance.LogTextBox = _log;

            CheckVersion();

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
            this._log.Font = new System.Drawing.Font("Lucida Console", 8F);
            this._log.Location = new System.Drawing.Point(16, 6);
            this._log.Multiline = true;
            this._log.Name = "_log";
            this._log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._log.Size = new System.Drawing.Size(635, 331);
            this._log.TabIndex = 1;
            this._log.WordWrap = false;
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusStripStatus,
            this.statusStripClient,
            this.statusStripServer,
            this.statusStripSerial});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 325);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.ShowItemToolTips = true;
            this.statusStrip.Size = new System.Drawing.Size(645, 42);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "MCE Controller";
            // 
            // statusStripStatus
            // 
            this.statusStripStatus.BackColor = System.Drawing.SystemColors.Control;
            this.statusStripStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusStripStatus.DoubleClickEnabled = true;
            this.statusStripStatus.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripStatus.Name = "statusStripStatus";
            this.statusStripStatus.Size = new System.Drawing.Size(248, 32);
            this.statusStripStatus.Text = "MCE Controller Status";
            this.statusStripStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripStatus.Click += new System.EventHandler(this.statusStripStatus_Click);
            // 
            // statusStripClient
            // 
            this.statusStripClient.BackColor = System.Drawing.SystemColors.Control;
            this.statusStripClient.DoubleClickEnabled = true;
            this.statusStripClient.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripClient.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripClient.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripClient.Name = "statusStripClient";
            this.statusStripClient.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripClient.Size = new System.Drawing.Size(109, 37);
            this.statusStripClient.Text = "Client";
            this.statusStripClient.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripClient.DoubleClick += new System.EventHandler(this.statusStripClient_Click);
            // 
            // statusStripServer
            // 
            this.statusStripServer.BackColor = System.Drawing.SystemColors.Control;
            this.statusStripServer.DoubleClickEnabled = true;
            this.statusStripServer.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripServer.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripServer.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripServer.Name = "statusStripServer";
            this.statusStripServer.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripServer.Size = new System.Drawing.Size(114, 37);
            this.statusStripServer.Text = "Server";
            this.statusStripServer.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripServer.DoubleClick += new System.EventHandler(this.statusStripServer_Click);
            // 
            // statusStripSerial
            // 
            this.statusStripSerial.BackColor = System.Drawing.SystemColors.Control;
            this.statusStripSerial.DoubleClickEnabled = true;
            this.statusStripSerial.Image = global::MCEControl.Properties.Resources.Trafficlight_green_icon;
            this.statusStripSerial.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.statusStripSerial.Margin = new System.Windows.Forms.Padding(10, 3, 0, 2);
            this.statusStripSerial.Name = "statusStripSerial";
            this.statusStripSerial.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStripSerial.Size = new System.Drawing.Size(105, 37);
            this.statusStripSerial.Text = "Serial";
            this.statusStripSerial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripSerial.DoubleClick += new System.EventHandler(this.statusStripSerial_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(10, 24);
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(645, 367);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this._log);
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
            log4.Info($"Logger: Logging to {Logger.Instance.LogFile}");
            Logger.Instance.TextBoxThreshold = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap[Instance.Settings.TextBoxLogThreshold];

            // Location can not be changed in constructor, has to be done here
            Location = Settings.WindowLocation;
            Size = Settings.WindowSize;

            CmdTable = CommandTable.Create($@"{ConfigPath}MCEControl.commands", Settings.DisableInternalCommands);
            if (CmdTable == null) {
                MessageBox.Show(this, Resources.MCEController_commands_read_error, Resources.App_FullName);
                _notifyIcon.Visible = false;
                Opacity = 100;
            }
            else {
                Logger.Instance.Log4.Info($"{CmdTable.NumCommands} commands available.");
                Opacity = (double)Settings.Opacity / 100;

                if (Settings.HideOnStartup) {
                    Opacity = 0;
                    Win32.PostMessage(Handle, (UInt32)WM.SYSCOMMAND, (UInt32)SC.CLOSE, 0);
                }
            }

            if (_cmdWindow == null)
                _cmdWindow = new CommandWindow();
 
            SetStatus($"Version: {Application.ProductVersion}");
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
            Logger.Instance.Log4.Info($"MCE Controller Version: {Application.ProductVersion}");
            var lv = new LatestVersion();
            lv.GetLatestStableVersionAsync((o, version) => {
                if (version == null && !String.IsNullOrWhiteSpace(lv.ErrorMessage)) {
                    Logger.Instance.Log4.Info(
                        $"Could not access tig.github.io/mcec to see if a newer version is available. {lv.ErrorMessage}");
                }
                else if (lv.CompareVersions() < 0) {
                    Logger.Instance.Log4.Info(
                        $"A newer version of MCE Controller ({version}) is available at tig.github.io/mcec.");
                }
                else if (lv.CompareVersions() > 0) {
                    Logger.Instance.Log4.Info(
                        $"You are are running a MORE recent version than can be found at tig.github.io/mcec ({version}).");
                }
                else {
                    Logger.Instance.Log4.Info("You are running the most recent version of MCE Controller.");
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
            Logger.Instance.Log4.Info("ShutDown");
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
                Logger.Instance.Log4.Info("Server: Starting...");
                _server = new SocketServer();
                _server.Notifications += ServerSocketCallbackHandler;
                _server.Start(Settings.ServerPort);
                _menuItemSendAwake.Enabled = Settings.WakeupEnabled;
            }
            else
                Logger.Instance.Log4.Debug("Attempt to StartServer() while an instance already exists!");
        }

        private void StopServer() {
            if (_server != null) {
                Logger.Instance.Log4.Info("Server: Stopping...");
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
                Logger.Instance.Log4.Info("Serial: Starting...");
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
                Logger.Instance.Log4.Info("Serial: Attempt to StartSerialServer() while an instance already exists!");
        }

        private void StopSerialServer() {
            if (_serialServer != null) {
                Logger.Instance.Log4.Info("Serial: Stopping...");
                // remove our notification handler
                _serialServer.Stop();
                _serialServer = null;
            }
        }

        private void StartClient(bool delay = false) {
            if (_client == null) {
                Logger.Instance.Log4.Info("Client: Starting...");
                _client = new SocketClient(Settings);
                _client.Notifications += ClientSocketNotificationHandler;
                _client.Start(delay);
            }
        }

        private void StopClient() {
            if (_client != null) {
                _cmdWindow.Visible = false;
                Logger.Instance.Log4.Info("Client: Stopping...");
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
                        Logger.Instance.Log4.Info("Client: Reconnecting...");
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
                Logger.Instance.Log4.Info($"Command: ({cmd}) error: {e}");
            }
        }

        // Sends a line of text (adds a "\n" to end) to connected client and server
        internal void SendLine(string v) {
            //Logger.Instance.Log4.Info($"Send: {v}");
            if (_client != null)
                _client.Send(v + "\n");

            if (_server != null)
                _server.Send(v + "\n");

            if (_serialServer != null)
                _serialServer.Send(v + "\n");
        }

        private void SetStatus(string text) {
            if (statusStrip.InvokeRequired) {
                statusStrip.BeginInvoke((Action)(() => {
                    statusStripStatus.Text = text;
                    _notifyIcon.Text = text;
                }));
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
                    Logger.Instance.Log4.Info(s);
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
            Logger.Instance.Log4.Info($"Server: {s}");
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
            Logger.Instance.Log4.Info($"Server: {s}");
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
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Started");
                        s = $"Connecting to {Settings.ClientHost}:{Settings.ClientPort}";
                        //SetStatus(s);
                        HideCommandWindow();
                    }
                    else if (status == ServiceStatus.Connected) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Connected");
                        s = $"Connected to {Settings.ClientHost}:{Settings.ClientPort}";
                        //SetStatus(s);
                        ShowCommandWindow();
                    }
                    else if (status == ServiceStatus.Stopped) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Stopped");
                        s = "Stopped.";
                        //SetStatus("Client/Sever Not Active");
                        HideCommandWindow();
                    }
                    else if (status == ServiceStatus.Sleeping) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Sleeping");
                        s = $"Waiting {(Settings.ClientDelayTime / 1000)} seconds to connect.";
                        //SetStatus(s);
                        HideCommandWindow();
                    }
                    break;

                case ServiceNotification.ReceivedData:
                    Logger.Instance.Log4.Info($"Client: Received; {msg}");
                    ReceivedData(reply, (string)msg);
                    return;

                case ServiceNotification.Error:
                    log4.Debug($"ClientSocketNotificationHandler - ServiceStatus.Error: {(string)msg}");
                    Logger.Instance.Log4.Info($"Client: Error; {(string)msg}");
                    RestartClient();
                    return;

                default:
                    s = "Unknown notification";
                    break;
            }
            Logger.Instance.Log4.Info($"Client: {s}");
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
                    Logger.Instance.Log4.Info($"SerialServer: Received: {msg}");
                    ReceivedData(reply, (string)msg);
                    return;

                case ServiceNotification.Error:
                    s = $"SerialServer: Error: {msg}";
                    break;

                default:
                    s = "SerialServer: Unknown notification";
                    break;
            }
            Logger.Instance.Log4.Info(s);
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
            if (Settings.ActAsClient)
                ToggleClient();
            else
                ShowSettings("Client");
        }

        private void statusStripServer_Click(object sender, EventArgs e) {
            if (Settings.ActAsServer)
                ToggleServer();
            else
                ShowSettings("Server");
        }

        private void statusStripSerial_Click(object sender, EventArgs e) {
            ShowSettings("Serial");
        }

        private void statusStripStatus_Click(object sender, EventArgs e) {
            ShowSettings("General");
        }
        private void ShowSettings(string defaultTabName) {
            var d = new SettingsDialog(Settings);
            d.DefaultTab = defaultTabName;

            if (d.ShowDialog(this) == DialogResult.OK) {
                Settings = d.Settings;

                Opacity = (double)Settings.Opacity / 100;

                Logger.Instance.TextBoxThreshold = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap[Settings.TextBoxLogThreshold];

                Stop();
                Start();
            }
        }
    }
}
