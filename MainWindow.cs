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
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MCEControl.Properties;
using Microsoft.Win32.Security;

namespace MCEControl {
    /// <summary>
    /// Summary description for MainWindow.
    /// </summary>
    public class MainWindow : Form {
        // Used to enabled access to AddLogEntry
        public static MainWindow MainWnd;
        public readonly CommandTable CmdTable;

        // Persisted application settings
        public AppSettings Settings;

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
        private StatusBar _statusBar;
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
        private MenuItem _menuItemSupport;
        private MenuItem _menuItemEditCommands;
        private readonly Icon _dummyIcon;

        public SocketClient Client {
            get { return _client; }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args) {
            MainWindow.MainWnd = new MainWindow();
            Application.Run(MainWindow.MainWnd);
        }

        public MainWindow() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // Load AppSettings
            Settings = AppSettings.Deserialize(AppSettings.GetSettingsPath());

            var resources = new ResourceManager(typeof (MainWindow));
            _dummyIcon = ((Icon) (resources.GetObject("notifyIcon.Icon")));

            _notifyIcon.Visible = true;
            _notifyIcon.Icon = Icon;
            ShowInTaskbar = true;

            SetStatusBar("");
            _notifyIcon.Text = Resources.App_FullName;
            _menuItemSendAwake.Enabled = false;

            CmdTable = CommandTable.Deserialize();
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
            this._menuItemSupport = new System.Windows.Forms.MenuItem();
            this._menuItemAbout = new System.Windows.Forms.MenuItem();
            this._statusBar = new System.Windows.Forms.StatusBar();
            this._notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this._notifyMenu = new System.Windows.Forms.ContextMenu();
            this._notifyMenuViewStatus = new System.Windows.Forms.MenuItem();
            this._menuSeparator4 = new System.Windows.Forms.MenuItem();
            this._notifyMenuItemSettings = new System.Windows.Forms.MenuItem();
            this._menuSeparator5 = new System.Windows.Forms.MenuItem();
            this._notifyMenuItemExit = new System.Windows.Forms.MenuItem();
            this._log = new System.Windows.Forms.TextBox();
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
            this._menuItemEditCommands.Text = "&Edit .commands File...";
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
            this._menuItemSupport,
            this._menuItemAbout});
            this._menuItemHelpMenu.Text = "&Help";
            // 
            // _menuItemHelp
            // 
            this._menuItemHelp.Index = 0;
            this._menuItemHelp.Shortcut = System.Windows.Forms.Shortcut.F1;
            this._menuItemHelp.Text = "&Help...";
            this._menuItemHelp.Click += new System.EventHandler(this.MenuItemHelpClick);
            // 
            // _menuItemSupport
            // 
            this._menuItemSupport.Index = 1;
            this._menuItemSupport.Text = "&Support...";
            this._menuItemSupport.Click += new System.EventHandler(this.MenuItemSupportClick);
            // 
            // _menuItemAbout
            // 
            this._menuItemAbout.Index = 2;
            this._menuItemAbout.Text = "&About";
            this._menuItemAbout.Click += new System.EventHandler(this.MenuItemAboutClick);
            // 
            // _statusBar
            // 
            this._statusBar.Location = new System.Drawing.Point(0, 184);
            this._statusBar.Name = "_statusBar";
            this._statusBar.Size = new System.Drawing.Size(368, 20);
            this._statusBar.TabIndex = 0;
            // 
            // _notifyIcon
            // 
            this._notifyIcon.ContextMenu = this._notifyMenu;
            this._notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("_notifyIcon.Icon")));
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
            this._log.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._log.Location = new System.Drawing.Point(0, 0);
            this._log.Multiline = true;
            this._log.Name = "_log";
            this._log.Size = new System.Drawing.Size(368, 187);
            this._log.TabIndex = 1;
            this._log.WordWrap = false;
            this._log.TextChanged += new System.EventHandler(this.LogTextChanged);
            this._log.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.LogKeyPress);
            // 
            // MainWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(368, 204);
            this.Controls.Add(this._log);
            this.Controls.Add(this._statusBar);
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
                m.Result = (IntPtr) 1;

                // Indicate to MainWindow_Closing() that we are shutting down;
                // otherwise it will just minimize to the tray
                _shuttingDown = true;
            }
            base.WndProc(ref m);
        }


        // When the app closes, dispose of the talker object
        protected override void OnClosed(EventArgs e) {
            if (_server != null) {
                // remove our notification handler
                _server.Notifications -= HandleServerNotifications;
                _server.Dispose();
            }
            if (_client != null) {
                // remove our notification handler
                _client.Notifications -= HandleClientNotifications;
                _client.Dispose();
            }

            if (_serialServer != null) {
                _serialServer.Notifications -= HandleSerialServerNotifications;
                _serialServer.Dispose();
            }

            base.OnClosed(e);
        }

        private void MainWindowLoad(object sender, EventArgs e) {
            // Location can not be changed in constructor, has to be done here
            Location = Settings.WindowLocation;
            Size = Settings.WindowSize;

            if (CmdTable == null)
            {
                MessageBox.Show(this, Resources.MCEController_commands_read_error, Resources.App_FullName);
                _notifyIcon.Visible = false;
                Opacity = 100;
            }
            else
            {

                AddLogEntry("Loaded " + CmdTable.NumCommands + " commands.");
                Opacity = (double)Settings.Opacity / 100;

                if (Settings.HideOnStartup)
                {
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

        private void Start()
        {
            if (Settings.ActAsServer)
                StartServer();

            if (Settings.ActAsSerialServer)
                StartSerialServer();

            if (Settings.ActAsClient)
                StartClient();
        }

        private void ShutDown() {
            AddLogEntry("ShutDown");
            _shuttingDown = true;
            // hide icon from the systray
            _notifyIcon.Visible = false;
            StopServer();
            StopClient();
            StopSerialServer();

            // Prevent access to the static MainWnd
            MainWnd = null;

            // Save the window size/location
            Settings.WindowLocation = Location;
            Settings.WindowSize = Size;
            Settings.Serialize();

            Close();
            Application.Exit();
        }

        private void StartServer() {
            if (_server == null) {
                _server = new SocketServer();
                _server.Notifications += HandleServerNotifications;
                _server.Start(Settings.ServerPort);
                _menuItemSendAwake.Enabled = Settings.WakeupEnabled;
            }
            else
                AddLogEntry("Fatal Error: Attempt to StartServer() while an instance already exists!");
        }

        private void StopServer() {
            if (_server != null) {
                // remove our notification handler
                _server.Stop();
                _server = null;
                _menuItemSendAwake.Enabled = false;
            }
        }

        private void StartSerialServer()
        {
            if (_serialServer == null)
            {
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
                AddLogEntry("Fatal Error: Attempt to StartSerialServer() while an instance already exists!");
        }

        private void StopSerialServer()
        {
            if (_serialServer != null)
            {
                // remove our notification handler
                _serialServer.Stop();
                _serialServer = null;
            }
        }

        private void StartClient() {
            if (_client == null) {
                _client = new SocketClient(Settings);
                _client.Notifications += HandleClientNotifications;
                _client.Start();
            }
            else
                AddLogEntry("Fatal Error: Attempt to StartClient() while an instance already exists!");
        }

        private delegate void StopClientCallback();
        private void StopClient() {
            if (_client != null) {
                _client.Stop();
                _client = null;
            }

            if (_cmdWindow != null) {
                if (this.InvokeRequired)
                    this.BeginInvoke((StopClientCallback) StopClient);
                else
                    _cmdWindow.Visible = false;
            }
        }

        private delegate void ShowCommandWindowCallback();
        private void ShowCommandWindow()
        {
            if (this.InvokeRequired)
                this.BeginInvoke((ShowCommandWindowCallback)ShowCommandWindow);
            else {
                _cmdWindow.Visible = Settings.ShowCommandWindow;
            }
        }

        private delegate void HideCommandWindowCallback();
        private void HideCommandWindow()
        {
            if (this.InvokeRequired)
                this.BeginInvoke((HideCommandWindowCallback)HideCommandWindow);
            else
            {
                _cmdWindow.Visible = false;
            }
        }

        private void ReceivedData(String cmd) {
            try {
                AddLogEntry("Command received: " + cmd);
                CmdTable.Execute(cmd);
            }
            catch (Exception e) {
                AddLogEntry(String.Format("Command ({0}) error: {1}", cmd, e));
            }
        }

        private delegate void SetStatusBarCallback(string text);
        private void SetStatusBar(string text) {
            if (_statusBar.InvokeRequired) {
                 _statusBar.BeginInvoke((SetStatusBarCallback)SetStatusBar, new object[] { text });
            }
            else {
                _statusBar.Text = text;
                _notifyIcon.Text = text;
            }
        }

        //
        // Notify callback for the TCP/IP Server
        //
        public void HandleServerNotifications(SocketServer.Notification notify, SocketServer.Status status, int client,
                                              String ipaddress, Object data) {
            String s = null;
            switch (notify) {
                case SocketServer.Notification.Initialized:
                    s = "Server: Initialized.";
                    break;

                case SocketServer.Notification.StatusChange:
                    switch (status) {
                        case SocketServer.Status.Listening:
                            s = "Server: Waiting for clients to connect on port " +
                                Settings.ServerPort.ToString(CultureInfo.InvariantCulture);
                            SetStatusBar("Waiting for clients to connect on port " +
                                         Settings.ServerPort.ToString(CultureInfo.InvariantCulture));
                            if (Settings.WakeupEnabled)
                                _server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost,
                                                         Settings.WakeupPort);
                            break;

                        case SocketServer.Status.Connected:
                            //s = String.Format("Server: Client #{0} at {1} connected.", client, ipaddress);
                            SetStatusBar("Clients connected, waiting for commands...");
                            return;

                        case SocketServer.Status.Stopped:
                            s = "Server: Stopped.";
                            SetStatusBar("Client/Sever Not Active");
                            if (Settings.WakeupEnabled)
                                _server.SendAwakeCommand(Settings.ClosingCommand, Settings.WakeupHost,
                                                         Settings.WakeupPort);
                            break;
                    }
                    break;

                case SocketServer.Notification.ReceivedData:
                    ReceivedData((string) data);
                    return;

                case SocketServer.Notification.Error:
                    s = String.Format("Server: Error (Client #{0} at {1}: {2})", client, ipaddress, data);
                    break;

                case SocketServer.Notification.ClientConnected:
                    s = String.Format("Server: Client #{0} at {1} connected.", client, ipaddress);
                    break;

                case SocketServer.Notification.ClientDisconnected:
                    s = String.Format("Server: Client #{0} at {1} has disconnected.", client, ipaddress);
                    break;

                case SocketServer.Notification.Wakeup:
                    s = "Wakeup: " + (string) data;
                    break;

                default:
                    s = "Server: Unknown notification";
                    break;
            }
            AddLogEntry(s);
        }

        //
        // Notify callback for the TCP/IP Client
        //
        public void HandleClientNotifications(SocketClient.Notification notify, Object data) {
            String s = null;
            switch (notify) {
                case SocketClient.Notification.Initialized:
                    //s = "Client: Client Initialized.";
                    break;

                case SocketClient.Notification.StatusChange:
                    var status = (SocketClient.Status) data;
                    if (status == SocketClient.Status.Listening) {
                        s = "Client: Connecting to " + Settings.ClientHost + ":" +
                            Settings.ClientPort.ToString(CultureInfo.InvariantCulture);
                        SetStatusBar("Connecting to " + Settings.ClientHost + ":" +
                                     Settings.ClientPort.ToString(CultureInfo.InvariantCulture));
                        HideCommandWindow();
                    }
                    else if (status == SocketClient.Status.Connected) {
                        s = "Client: Connected to " + Settings.ClientHost + ":" +
                            Settings.ClientPort.ToString(CultureInfo.InvariantCulture);
                        SetStatusBar("Connected to " + Settings.ClientHost + ":" +
                                     Settings.ClientPort.ToString(CultureInfo.InvariantCulture) +
                                     ", waiting for commands...");

                        ShowCommandWindow();
                    }
                    else if (status == SocketClient.Status.Closed) {
                        s = "Client: Stopped.";
                        SetStatusBar("Client/Sever Not Active");
                        HideCommandWindow();
                    }
                    else if (status == SocketClient.Status.Sleeping) {
                        s = "Client: Waiting " + (Settings.ClientDelayTime/1000).ToString(CultureInfo.InvariantCulture) +
                            " seconds to connect.";
                        SetStatusBar("Waiting " + (Settings.ClientDelayTime/1000).ToString(CultureInfo.InvariantCulture) +
                                     " seconds to connect.");
                        HideCommandWindow();
                    }
                    break;

                case SocketClient.Notification.ReceivedData:
                    ReceivedData((string) data);
                    return;

                case SocketClient.Notification.Error:
                    s = "Client Error: " + (string) data;
                    break;

                case SocketClient.Notification.End:
                    if (!_shuttingDown && _client != null)
                    {
                        _client.Stop();
                        s = "Client: " + (string) data + " Reconnecting...";
                        _client.Start(true);
                    }
                    break;

                default:
                    s = "Unknown notification";
                    break;
            }
            AddLogEntry(s);
        }

        //
        // Notify callback for the Serial Server
        //
        public void HandleSerialServerNotifications(SerialServer.Notification notify, SerialServer.Status status, String message,
                                              Object data)
        {
            String s = null;
            switch (notify)
            {
                case SerialServer.Notification.StatusChange:
                    switch (status)
                    {
                        case SerialServer.Status.Started:
                            s = String.Format("SerialServer: Waiting for commands on {0}...", message);
                            SetStatusBar("Waiting for Serial commands...");
                            break;

                        case SerialServer.Status.Stopped:
                            s = "SerialServer: Stopped.";
                            SetStatusBar("Serial Server Not Active");
                            break;
                    }
                    break;

                case SerialServer.Notification.ReceivedData:
                    ReceivedData((string)data);
                    return;

                case SerialServer.Notification.Error:
                    s = String.Format("SerialServer: Error ({0}) {1}", message, data);
                    break;

                default:
                    s = "SerialServer: Unknown notification";
                    break;
            }
            AddLogEntry(s);
        }

      

        private delegate void AddLogEntryCallback(string text);
        public static void AddLogEntry(String text)
        {   
            if (MainWnd == null) return;
            if (MainWnd.InvokeRequired || MainWnd._log.InvokeRequired) 
                MainWnd.BeginInvoke((AddLogEntryCallback)AddLogEntry, new object[] { text });
            else
                MainWnd._log.AppendText("[" + DateTime.Now.ToString("yy'-'MM'-'dd' 'HH':'mm':'ss") + "] " + text +
                                        Environment.NewLine);
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
            Opacity = (double) Settings.Opacity/100;
        }

        private void MenuItemAboutClick(object sender, EventArgs e) {
            var a = new AboutBox();
            a.ShowDialog(this);
        }

        private void MenuSettingsClick(object sender, EventArgs e) {
            var d = new SettingsDialog(Settings);
            if (d.ShowDialog(this) == DialogResult.OK) {
                Settings = d.Settings;

                Opacity = (double) Settings.Opacity/100;

                StopClient();
                StopServer();
                StopSerialServer();

                if (Settings.ActAsServer)
                    StartServer();

                if (Settings.ActAsSerialServer)
                    StartSerialServer();

                if (Settings.ActAsClient) {
                    StartClient();
                }
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
            if (_log.TextLength > (16*1024)) {
                _log.Text = _log.Text.Remove(0, _log.Text.IndexOf("\r\n", StringComparison.Ordinal) + 2);
                _log.Select(_log.TextLength, 0);
            }
            _log.ScrollToCaret();
        }

        private void MenuItemSendAwakeClick(object sender, EventArgs e) {
            _server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
        }

        private void MenuItemHelpClick(object sender, EventArgs e) {
            Process.Start("http://mcec.codeplex.com/documentation/");
        }

        private void MenuItemSupportClick(object sender, EventArgs e) {
            Process.Start("http://mcec.codeplex.com/discussions/");
        }

        private void MenuItemEditCommandsClick(object sender, EventArgs e) {
            Process.Start(Application.StartupPath);
        }

        private void MainWindow_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            Process.Start("http://mcec.codeplex.com/documentation/");
        }
    }
}
