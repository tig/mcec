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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using MCEControl.Properties;
using Microsoft.Win32.Security;
using log4net;
using Microsoft.Win32;
using System.Drawing;

namespace MCEControl {
    public partial class MainWindow : Form {
        // MainWindow is a singleton
        private static readonly Lazy<MainWindow> lazy = new Lazy<MainWindow>(() => new MainWindow());
        public static MainWindow Instance { get { return lazy.Value; } }

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

        // Indicates whether user hit the close box (minimize)
        // or the app is exiting
        private bool shuttingDown;

        // Settings
        public AppSettings Settings { get => settings; set => settings = value; }
        // If running from default install location (in Program Files) find the
        // .commands, .settings, and .log files in %appdata%. Otherwise find them in the 
        // directory MCEControl.exe was run from.

        public MainWindow() {
            InitializeComponent();
            Logger.Instance.LogTextBox = logTextBox;

            notifyIcon.Icon = Icon;
            ShowInTaskbar = true;

            Program.CheckVersion();
            SetStatus("");
            sendAwakeMenuItem.Enabled = false;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
            Logger.Instance.Log4.Info($"Logger: Logging to {Logger.Instance.LogFile}");
            Logger.Instance.TextBoxThreshold = LogManager.GetLogger("MCEControl").Logger.Repository.LevelMap[Instance.Settings.TextBoxLogThreshold];

            if (cmdWindow == null)
                cmdWindow = new CommandWindow();

            // watch .command file for changes
            watcher = new CommandFileWatcher($@"{Program.ConfigPath}MCEControl.commands");
            watcher.ChangedEvent += (o, a) => CmdTable_CommandsChangedEvent(o, a);
            LoadCommands();

            if (Settings.HideOnStartup) {
                Opacity = 0;
                Win32.PostMessage(Handle, (UInt32)WM.SYSCOMMAND, (UInt32)SC.CLOSE, 0);
            }

            logTextBox.Font = new System.Drawing.Font(logTextBox.Font.FontFamily, MainMenuStrip.Font.SizeInPoints - 1,
                System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(SystemEvents_UserPreferenceChanged);

            // Location can not be changed in constructor, has to be done here
            // Use Window's default for location initially. Size needs highDPI conversion. 
            if (Settings.WindowLocation.IsEmpty || Settings.WindowSize.IsEmpty) {
                Size = new Size(this.LogicalToDeviceUnits(1024), this.LogicalToDeviceUnits(640));
            }
            else {
                Location = Settings.WindowLocation;
                Size = Settings.WindowSize;
            }

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

            Invoker = Commands.Create($@"{Program.ConfigPath}MCEControl.commands", Settings.DisableInternalCommands);
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
                Logger.Instance.Log4.Info("Exiting...");
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
        public void SendLine(string v) {
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
                        Logger.Instance.Log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Started");
                        s = $"Connecting to {Settings.ClientHost}:{Settings.ClientPort}";
                    }
                    else if (status == ServiceStatus.Connected) {
                        Logger.Instance.Log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Connected");
                        s = $"Connected to {Settings.ClientHost}:{Settings.ClientPort}";
                    }
                    else if (status == ServiceStatus.Stopped) {
                        Logger.Instance.Log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Stopped");
                        s = "Stopped.";
                    }
                    else if (status == ServiceStatus.Sleeping) {
                        Logger.Instance.Log4.Debug("ClientSocketNotificationHandler - ServiceStatus.Sleeping");
                        s = $"Waiting {(Settings.ClientDelayTime / 1000)} seconds to connect.";
                    }
                    break;

                case ServiceNotification.ReceivedData:
                    Logger.Instance.Log4.Info($"Client: Received; {msg}");
                    ReceivedData(reply, (string)msg);
                    return;

                case ServiceNotification.Error:
                    Logger.Instance.Log4.Debug($"ClientSocketNotificationHandler - ServiceStatus.Error: {(string)msg}");
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
            var a = new About();
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
            Process.Start(Program.ConfigPath);
        }

        private void helpMenuItem_Click(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void wikiMenuItem_Click(object sender, EventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void MenuItemEditCommands_Click(object sender, EventArgs e) {
            Process.Start(Program.ConfigPath);
        }

        private void helpMenuItem_Click(object sender, CancelEventArgs e) {
            Process.Start("https://github.com/tig/mcec/wiki");
        }

        private void updatesMenuItem_Click(object sender, EventArgs e) {
            Program.CheckVersion();
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
