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
using System.Windows.Forms;
using MCEControl.Properties;
using Microsoft.Win32.Security;
using log4net;
using Microsoft.Win32;

[assembly: System.CLSCompliant(true)]
namespace MCEControl {
    /// <summary>
    /// Summary description for MainWindow.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1501", Justification = "WinForms generated", Scope = "namespace")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1506", Justification = "WinForms generated", Scope = "namespace")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "IDE0069")]
    public class MainWindow : Form {
        private readonly log4net.ILog log4;

        // Persisted application settings
        private AppSettings settings;

        // Protocol objects
        private SocketServer server;
        public SocketServer Server { get { return server; } }

        private SocketClient client;
        public SocketClient Client { get { return client; } }
        private SerialServer serialServer;
        public SerialServer SerialServer { get { return serialServer; } }


        // Commands
        private Commands commands;
        public Commands Invoker { get => commands; set => commands = value; }

        private CommandWindow cmdWindow;

        // Window controls
        private MenuStrip menuStrip;
        private ToolStripMenuItem fileMenu;
        private ToolStripMenuItem exitMenuItem;
        private ToolStripMenuItem commandsMenu;
        private ToolStripMenuItem showCommandsMenuItem;
        private ToolStripMenuItem openCommandsFolderMenuItem;
        private ToolStripMenuItem sendAwakeMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem wikiMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem checkUpdatesMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem aboutMenuItem;
        private TextBoxExt logTextBox;
        private MenuItem menuSeparator5;
        private MenuItem notifySettingsMenuItem;
        private MenuItem menuSeparator4;
        private MenuItem notifyStatusMenuItem;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusStripStatus;
        private ToolStripStatusLabel statusStripClient;
        private ToolStripStatusLabel statusStripServer;
        private ToolStripStatusLabel statusStripSerial;
        private IContainer components;
        private NotifyIcon notifyIcon;
        private ContextMenu notifyMenu;
        private MenuItem notifyExitMenuItem;


        // MainWindow is a singleton
        private static readonly Lazy<MainWindow> lazy = new Lazy<MainWindow>(() => new MainWindow());
        private ToolStripMenuItem settingsMenuItem;

        public static MainWindow Instance { get { return lazy.Value; } }

        // Indicates whether user hit the close box (minimize)
        // or the app is exiting
        private bool shuttingDown;

        // Settings
        public AppSettings Settings { get => settings; set => settings = value; }
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

        // The main entry point for the application.
        [STAThread]
        public static void Main() {
            // https://docs.microsoft.com/en-us/dotnet/framework/winforms/high-dpi-support-in-windows-forms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start logging
            Logger.Instance.LogFile = $@"{ConfigPath}MCEControl.log";
            Logger.Instance.Log4.Debug("Main");

            // TODO: Update to check for 4.7 or newer
            if (!IsNet45OrNewer()) {
                MessageBox.Show(Resources.Error_RequiresDotNetVersion);
                return;
            }

            // Load AppSettings
            Instance.Settings = AppSettings.Deserialize($@"{ConfigPath}{AppSettings.SettingsFileName}");

            Application.Run(Instance);
        }

        public static bool IsNet45OrNewer() {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        public MainWindow() {
            log4 = Logger.Instance.Log4;

            InitializeComponent();
            Logger.Instance.LogTextBox = logTextBox;

            notifyIcon.Icon = Icon;
            ShowInTaskbar = true;

            CheckVersion();
            SetStatus("");
            sendAwakeMenuItem.Enabled = false;
        }

        // Clean up any resources being used.
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

                if (server != null) {
                    // remove our notification handler
                    server.Notifications -= serverSocketCallbackHandler;
                    server.Dispose();
                }
                if (client != null) {
                    // remove our notification handler
                    client.Notifications -= clientSocketNotificationHandler;
                    client.Dispose();
                }

                if (serialServer != null) {
                    serialServer.Notifications -= HandleSerialServerNotifications;
                    serialServer.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m) {
            // If the session is being logged off, or the machine is shutting
            // down...
            if (m.Msg == 0x11) { // WM_QUERYENDSESSION
                // Allow shut down (m.Result may already be non-zero, but I set it
                // just in case)
                m.Result = (IntPtr)1;

                // Indicate to MainWindow_Closing() that we are shutting down;
                // otherwise it will just minimize to the tray
                shuttingDown = true;
            }
            base.WndProc(ref m);
        }

        private void mainWindow_Load(object sender, EventArgs e) {
            log4.Info($"Logger: Logging to {Logger.Instance.LogFile}");
            Logger.Instance.TextBoxThreshold = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap[Instance.Settings.TextBoxLogThreshold];

            // Location can not be changed in constructor, has to be done here
            Location = Settings.WindowLocation;
            Size = Settings.WindowSize;

            if (cmdWindow == null)
                cmdWindow = new CommandWindow();

            // watch .command file for changes
            watcher = new CommandFileWatcher($@"{ConfigPath}MCEControl.commands");
            watcher.ChangedEvent += (o, a) => CmdTable_CommandsChangedEvent(o, a);
            LoadCommands();

            if (Settings.HideOnStartup) {
                Opacity = 0;
                Win32.PostMessage(Handle, (UInt32)WM.SYSCOMMAND, (UInt32)SC.CLOSE, 0);
            }

            logTextBox.Font = new System.Drawing.Font(logTextBox.Font.FontFamily, MainMenuStrip.Font.SizeInPoints - 1,
                System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(SystemEvents_UserPreferenceChanged);

            SetStatus($"Version: {Application.ProductVersion}");
            Start();
        }

        private void CmdTable_CommandsChangedEvent(object sender, EventArgs e) {

            if (cmdWindow.InvokeRequired)
                cmdWindow.BeginInvoke((Action)(() => { CmdTable_CommandsChangedEvent(sender, e); }));
            else {
                LoadCommands();
            }
        }

        private CommandFileWatcher watcher;

        private void LoadCommands() {
            if (Invoker != null) {
               // Invoker.Dispose();
            }

            Invoker = Commands.Create($@"{ConfigPath}MCEControl.commands", Settings.DisableInternalCommands);
            if (Invoker == null)
                notifyIcon.Visible = false;
            else {
                cmdWindow.RefreshList();
                Logger.Instance.Log4.Info($"{Invoker.Count} commands available.");
            }
        }

        private void mainWindow_Closing(object sender, CancelEventArgs e) {
            if (!shuttingDown) {
                // If we're NOT shutting down (the user hit the close button or pressed
                // CTRL-F4) minimize to tray.
                e.Cancel = true;

                // Hide the form and make sure the taskbar icon is visible
                notifyIcon.Visible = true;
                Hide();
            }
            else
                log4.Info("Exiting...");
        }

        private void CheckVersion() {
            Logger.Instance.Log4.Info($"MCE Controller Version: {Application.ProductVersion}");
            var lv = new LatestVersion() { Url = "https://tig.github.io/mcec/install_version.txt" };
            ;
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
                UserActivityMonitor.Instance.Start(Settings.ActivityMonitorDebounceTime);
        }

        private void Stop() {
            if (this.InvokeRequired)
                this.BeginInvoke((MethodInvoker)delegate () { Stop(); });
            else {
                UserActivityMonitor.Stop();
                StopClient();
                StopServer();
                StopSerialServer();
            }
        }

        public void ShutDown() {

            if (this.InvokeRequired) {
                Logger.Instance.Log4.Info("ShutDown InvokeRequired");
                this.BeginInvoke((MethodInvoker)delegate () { ShutDown(); });
                return;
            }

            Logger.Instance.Log4.Info("ShutDown");
            shuttingDown = true;

            Stop();

            // hide icon from the systray
            notifyIcon.Visible = false;

            // Save the window size/location
            Settings.WindowLocation = Location;
            Settings.WindowSize = Size;
            Settings.Serialize();

            Close();
            Application.Exit();
        }

        private void StartServer() {
            if (server == null) {
                Logger.Instance.Log4.Info("Server: Starting...");
                server = new SocketServer();
                server.Notifications += serverSocketCallbackHandler;
                server.Start(Settings.ServerPort);
                sendAwakeMenuItem.Enabled = Settings.WakeupEnabled;
            }
            else
                Logger.Instance.Log4.Debug("Attempt to StartServer() while an instance already exists!");
        }

        private void StopServer() {
            if (server != null) {
                Logger.Instance.Log4.Info("Server: Stopping...");
                // remove our notification handler
                server.Stop();
                server = null;
                sendAwakeMenuItem.Enabled = false;
            }
        }

        private void ToggleServer() {
            if (server == null)
                StartServer();
            else
                StopServer();
        }

        private void StartSerialServer() {
            if (serialServer == null) {
                Logger.Instance.Log4.Info("Serial: Starting...");
                serialServer = new SerialServer();
                serialServer.Notifications += HandleSerialServerNotifications;
                serialServer.Start(Settings.SerialServerPortName,
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
            if (serialServer != null) {
                Logger.Instance.Log4.Info("Serial: Stopping...");
                // remove our notification handler
                serialServer.Stop();
                serialServer = null;
            }
        }

        private void StartClient(bool delay = false) {
            if (client == null) {
                Logger.Instance.Log4.Info("Client: Starting...");
                client = new SocketClient(Settings);
                client.Notifications += clientSocketNotificationHandler;
                client.Start(delay);
            }
        }

        private void StopClient() {
            if (client != null) {
                cmdWindow.Visible = false;
                Logger.Instance.Log4.Info("Client: Stopping...");
                client.Stop();
                client = null;
            }
        }

        private void ToggleClient() {
            if (client == null)
                StartClient();
            else
                StopClient();
        }

        private void RestartClient() {
            if (cmdWindow != null) {
                if (this.InvokeRequired)
                    this.BeginInvoke((MethodInvoker)delegate () { RestartClient(); });
                else {
                    StopClient();
                    if (!shuttingDown && Settings.ActAsClient && Settings.ClientDelayTime > 0) {
                        Logger.Instance.Log4.Info("Client: Reconnecting...");
                        StartClient(true);
                    }
                }
            }
        }

        private void ShowCommandWindow() {
            if (this.InvokeRequired)
                this.BeginInvoke((MethodInvoker)delegate () { ShowCommandWindow(); });
            else {
                cmdWindow.Visible = Settings.ShowCommandWindow = true;
            }
        }

        private void HideCommandWindow() {
            if (this.InvokeRequired)
                this.BeginInvoke((MethodInvoker)delegate () { HideCommandWindow(); });
            else {
                Settings.ShowCommandWindow = cmdWindow.Visible = false;
            }
        }

        /// <summary>
        /// Anytime a client or server receives data that looks like a command, this function is called.
        /// </summary>
        /// <param name="reply">THe reply context any replies should be sent to</param>
        /// <param name="cmd">the command string</param>
        private void ReceivedData(Reply reply, String cmd) {
            // To ensure we are single-threaded for Invoker, check if we're on UI thread
            // if not, use Invoke to get onto UI thread.
            //
            // TOOD: This is probably not the right model. What we should do is have 
            // the Invoker run on it's won thread. 
            if (this.InvokeRequired)
                this.BeginInvoke((MethodInvoker)delegate () { ReceivedData(reply, cmd); });
            else
                try {
                    Invoker.Enqueue(reply, cmd);
                    Invoker.ExecuteNext();
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Info($"Command: ({cmd}) error: {e}");
                }
        }

        // Sends a line of text (adds a "\n" to end) to connected client and server
        internal void SendLine(string v) {
            //Logger.Instance.Log4.Info($"Send: {v}");
            if (client != null)
                client.Send(v + "\n");
            else if (server != null)
                server.Send(v + "\n");

            if (serialServer != null)
                serialServer.Send(v + "\n");
        }

        private void SetStatus(string text) {
            if (statusStrip.InvokeRequired)
                statusStrip.BeginInvoke((Action)(() => { SetStatus(text); }));
            else {
                statusStripStatus.Text = text;
                notifyIcon.Text = text;
            }
        }

        private void SetServerStatus(ServiceStatus status) {
            if (statusStrip.InvokeRequired)
                statusStrip.BeginInvoke((Action)(() => { SetServerStatus(status); }));
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
            if (statusStrip.InvokeRequired)
                statusStrip.BeginInvoke((Action)(() => { SetClientStatus(status); }));
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

        private void SetSerialStatus(ServiceStatus status) {
            if (statusStrip.InvokeRequired)
                statusStrip.BeginInvoke((Action)(() => { SetSerialStatus(status); }));
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


        // Notify callback for the TCP/IP Server
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "<Pending>")]
        public void serverSocketCallbackHandler(ServiceNotification notification, ServiceStatus status, Reply reply, String msg) {
            if (notification == ServiceNotification.StatusChange)
                HandleSocketServerStatusChange(status);
            else {
                HandleSocketServerNotification(notification, status, (SocketServer.ServerReplyContext)reply, msg);
            }
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
                        server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost,
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
                        server.SendAwakeCommand(Settings.ClosingCommand, Settings.WakeupHost,
                            Settings.WakeupPort);
                    break;
            }
            Logger.Instance.Log4.Info($"Server: {s}");
        }

        //
        // Notify callback for the TCP/IP Client
        //
        public void clientSocketNotificationHandler(ServiceNotification notify, ServiceStatus status, Reply reply, String msg) {
            SetClientStatus(status);
            String s = null;
            switch (notify) {
                case ServiceNotification.StatusChange:
                    if (status == ServiceStatus.Started) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Started");
                        s = $"Connecting to {Settings.ClientHost}:{Settings.ClientPort}";
                    }
                    else if (status == ServiceStatus.Connected) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Connected");
                        s = $"Connected to {Settings.ClientHost}:{Settings.ClientPort}";
                    }
                    else if (status == ServiceStatus.Stopped) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Stopped");
                        s = "Stopped.";
                    }
                    else if (status == ServiceStatus.Sleeping) {
                        log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Sleeping");
                        s = $"Waiting {(Settings.ClientDelayTime / 1000)} seconds to connect.";
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

        private void ShowSettings(string defaultTabName) {
            var d = new SettingsDialog(Settings) {
                DefaultTab = defaultTabName
            };

            if (d.ShowDialog(this) == DialogResult.OK) {
                Settings = d.Settings;

                Opacity = (double)Settings.Opacity / 100;

                Logger.Instance.TextBoxThreshold = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap[Settings.TextBoxLogThreshold];

                Stop();
                Start();
            }
            d.Dispose();
        }

        // ----------------------------------------
        // User action handlers
        private void exitMenuItem_Click(object sender, EventArgs e) {
            ShutDown();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e) {
            // Show the form when the user double clicks on the notify icon.

            // Set the WindowState to normal if the form is minimized.
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            // Activate the form.
            notifyIcon.Visible = false;
            Activate();
            Show();
            Opacity = (double)Settings.Opacity / 100;
        }

        private void aboutMenuItem_Click(object sender, EventArgs e) {
            var a = new AboutBox();
            a.ShowDialog(this);
            a.Dispose();
        }

        private void settingsMenuItem_Click(object sender, EventArgs e) {
            ShowSettings("General");
        }

        private void sendAwakeMenuItem_Click(object sender, EventArgs e) {
            server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
        }

        private void commandsMenuItem_Click(object sender, EventArgs e) {
            ShowCommandWindow();
        }
        private void openCommandsFolderMenuItem_Click(object sender, EventArgs e) {
            Process.Start(ConfigPath);
        }

        private void helpMenuItem_Click(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void wikiMenuItem_Click(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void MenuItemEditCommands_Click(object sender, EventArgs e) {
            Process.Start(ConfigPath);
        }

        private void helpMenuItem_Click(object sender, CancelEventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void updatesMenuItem_Click(object sender, EventArgs e) {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.notifyMenu = new System.Windows.Forms.ContextMenu();
            this.notifyStatusMenuItem = new System.Windows.Forms.MenuItem();
            this.menuSeparator4 = new System.Windows.Forms.MenuItem();
            this.notifySettingsMenuItem = new System.Windows.Forms.MenuItem();
            this.menuSeparator5 = new System.Windows.Forms.MenuItem();
            this.notifyExitMenuItem = new System.Windows.Forms.MenuItem();
            this.logTextBox = new MCEControl.TextBoxExt();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusStripStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripClient = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripServer = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStripSerial = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.commandsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.showCommandsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openCommandsFolderMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.sendAwakeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wikiMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.checkUpdatesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip.SuspendLayout();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenu = this.notifyMenu;
            this.notifyIcon.Text = global::MCEControl.Properties.Resources.App_FullName;
            this.notifyIcon.Visible = true;
            this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // notifyMenu
            // 
            this.notifyMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.notifyStatusMenuItem,
            this.menuSeparator4,
            this.notifySettingsMenuItem,
            this.menuSeparator5,
            this.notifyExitMenuItem});
            // 
            // notifyStatusMenuItem
            // 
            this.notifyStatusMenuItem.Index = 0;
            this.notifyStatusMenuItem.Text = "&View Status...";
            this.notifyStatusMenuItem.Click += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // menuSeparator4
            // 
            this.menuSeparator4.Index = 1;
            this.menuSeparator4.Text = "-";
            // 
            // notifySettingsMenuItem
            // 
            this.notifySettingsMenuItem.Index = 2;
            this.notifySettingsMenuItem.Text = "&Settings...";
            this.notifySettingsMenuItem.Click += new System.EventHandler(this.settingsMenuItem_Click);
            // 
            // menuSeparator5
            // 
            this.menuSeparator5.Index = 3;
            this.menuSeparator5.Text = "-";
            // 
            // notifyExitMenuItem
            // 
            this.notifyExitMenuItem.Index = 4;
            this.notifyExitMenuItem.Text = "&Exit";
            this.notifyExitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // logTextBox
            // 
            this.logTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.logTextBox.Font = new System.Drawing.Font("Lucida Console", 8F);
            this.logTextBox.Location = new System.Drawing.Point(0, 44);
            this.logTextBox.Margin = new System.Windows.Forms.Padding(0);
            this.logTextBox.Multiline = true;
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.logTextBox.Size = new System.Drawing.Size(784, 455);
            this.logTextBox.TabIndex = 1;
            this.logTextBox.WordWrap = false;
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
            this.statusStrip.Location = new System.Drawing.Point(0, 498);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.ShowItemToolTips = true;
            this.statusStrip.Size = new System.Drawing.Size(784, 42);
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
            // menuStrip
            // 
            this.menuStrip.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenu,
            this.commandsMenu,
            this.helpToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Padding = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip.Size = new System.Drawing.Size(784, 40);
            this.menuStrip.TabIndex = 3;
            this.menuStrip.Text = "menuStrip";
            // 
            // fileMenu
            // 
            this.fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.settingsMenuItem,
            this.toolStripSeparator2,
            this.exitMenuItem});
            this.fileMenu.Name = "fileMenu";
            this.fileMenu.Size = new System.Drawing.Size(72, 36);
            this.fileMenu.Text = "&File";
            // 
            // settingsMenuItem
            // 
            this.settingsMenuItem.Name = "settingsMenuItem";
            this.settingsMenuItem.Size = new System.Drawing.Size(250, 44);
            this.settingsMenuItem.Text = "&Settings...";
            this.settingsMenuItem.Click += new System.EventHandler(this.settingsMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(247, 6);
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(250, 44);
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // commandsMenu
            // 
            this.commandsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showCommandsMenuItem,
            this.openCommandsFolderMenuItem,
            this.toolStripSeparator1,
            this.sendAwakeMenuItem});
            this.commandsMenu.Name = "commandsMenu";
            this.commandsMenu.Size = new System.Drawing.Size(156, 36);
            this.commandsMenu.Text = "&Commands";
            // 
            // showCommandsMenuItem
            // 
            this.showCommandsMenuItem.Name = "showCommandsMenuItem";
            this.showCommandsMenuItem.Size = new System.Drawing.Size(422, 44);
            this.showCommandsMenuItem.Text = "Show &Commands...";
            this.showCommandsMenuItem.Click += new System.EventHandler(this.commandsMenuItem_Click);
            // 
            // openCommandsFolderMenuItem
            // 
            this.openCommandsFolderMenuItem.Name = "openCommandsFolderMenuItem";
            this.openCommandsFolderMenuItem.Size = new System.Drawing.Size(422, 44);
            this.openCommandsFolderMenuItem.Text = "&Open .commands folder...";
            this.openCommandsFolderMenuItem.Click += new System.EventHandler(this.openCommandsFolderMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(419, 6);
            // 
            // sendAwakeMenuItem
            // 
            this.sendAwakeMenuItem.Name = "sendAwakeMenuItem";
            this.sendAwakeMenuItem.Size = new System.Drawing.Size(422, 44);
            this.sendAwakeMenuItem.Text = "Send &Awake Signal";
            this.sendAwakeMenuItem.Click += new System.EventHandler(this.sendAwakeMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.wikiMenuItem,
            this.toolStripSeparator3,
            this.checkUpdatesMenuItem,
            this.toolStripSeparator4,
            this.aboutMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(85, 36);
            this.helpToolStripMenuItem.Text = "&Help";
            // 
            // wikiMenuItem
            // 
            this.wikiMenuItem.Name = "wikiMenuItem";
            this.wikiMenuItem.Size = new System.Drawing.Size(343, 44);
            this.wikiMenuItem.Text = "&Wiki";
            this.wikiMenuItem.Click += new System.EventHandler(this.wikiMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(340, 6);
            // 
            // checkUpdatesMenuItem
            // 
            this.checkUpdatesMenuItem.Name = "checkUpdatesMenuItem";
            this.checkUpdatesMenuItem.Size = new System.Drawing.Size(343, 44);
            this.checkUpdatesMenuItem.Text = "&Check for updates";
            this.checkUpdatesMenuItem.Click += new System.EventHandler(this.updatesMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(340, 6);
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Size = new System.Drawing.Size(343, 44);
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(10, 24);
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(784, 540);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.Controls.Add(this.logTextBox);
            this.HelpButton = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainWindow";
            this.Text = "MCE Controller";
            this.HelpButtonClicked += new System.ComponentModel.CancelEventHandler(this.helpMenuItem_Click);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.mainWindow_Closing);
            this.Load += new System.EventHandler(this.mainWindow_Load);
            this.Layout += new System.Windows.Forms.LayoutEventHandler(this.MainWindow_Layout);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        // WinForms layout with MenuStrip and StatusStrip has issues (apparently) with
        // Anchor. This works around that.
        private void MainWindow_Layout(object sender, LayoutEventArgs e) {
            // Adjust vertical location & height of TextBox to deal with font scaling changes.
            // Note we add a little margin on the left
            logTextBox.Location = new System.Drawing.Point(8, menuStrip.Height);
            logTextBox.Size = new System.Drawing.Size(this.ClientSize.Width - logTextBox.Location.X, this.ClientSize.Height - menuStrip.Height - statusStrip.Height);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, EventArgs e) {
            logTextBox.Font = new System.Drawing.Font(logTextBox.Font.FontFamily, menuStrip.Font.SizeInPoints - 1, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        }
    }
}
