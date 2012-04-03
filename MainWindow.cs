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
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.Win32.Security;

namespace MCEControl
{
    /// <summary>
    /// Summary description for MainWindow.
    /// </summary>
    public class MainWindow : System.Windows.Forms.Form
    {
        // Used to enabled access to AddLogEntry
        public static MainWindow MainWnd = null;

        // Persisted application settings
        public AppSettings Settings = null;
        
        // Protocol objects
        private SocketServer Server = null;
        private SocketClient Client = null;
        private CommandTable Commands = null;

        // Indicates whether user hit the close box (minimize)
        // or the app is exiting
        private bool ShuttingDown = false;

        // Window controls
        private System.Windows.Forms.MainMenu mainMenu;
        private System.Windows.Forms.MenuItem menuItemFileMenu;
        private System.Windows.Forms.MenuItem menuItemExit;
        private System.Windows.Forms.MenuItem menuItemHelpMenu;
        private System.Windows.Forms.MenuItem menuItemAbout;
        private System.Windows.Forms.StatusBar statusBar;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.TextBox Log;
        private System.Windows.Forms.ContextMenu notifyMenu;
        private System.Windows.Forms.MenuItem notifyMenuItemExit;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.MenuItem menuItemSendAwake;
        private System.Windows.Forms.MenuItem menuItem1;
        private System.Windows.Forms.MenuItem menuItem2;
        private System.Windows.Forms.MenuItem menuSettings;
        private System.Windows.Forms.MenuItem menuItem4;
        private System.Windows.Forms.MenuItem notifyMenuItemSettings;
        private System.Windows.Forms.MenuItem menuItem3;
        private System.Windows.Forms.MenuItem notifyMenuViewStatus;
        private MenuItem menuItemHelp;
        private MenuItem menuItemSupport;
        private MenuItem menuItemEditCommands;
        private Icon DummyIcon = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {

            MainWindow main = new MainWindow();
            Application.Run(main);
        }

        public MainWindow()
        {
            MainWnd = this;
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            // Load AppSettings
            this.Settings = AppSettings.Deserialize(AppSettings.GetSettingsPath());

            System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(MainWindow));
            this.DummyIcon= ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));

            this.notifyIcon.Visible = true;
            this.notifyIcon.Icon = this.Icon;
            this.ShowInTaskbar = true;

            this.SetStatusBar("");
            notifyIcon.Text = "MCE Controller";
            menuItemSendAwake.Enabled = false;

            this.Commands = CommandTable.Deserialize();
            if (Commands == null)
            {
                MessageBox.Show(this, "No commands loaded. Something is wrong with the MCEController.commands file. See the log for details, fix, and restart.", "MCE Controller");
                notifyIcon.Visible = false;
                Opacity = 100;
            }
            else
            {
                AddLogEntry("Loaded " + Commands.NumCommands + " commands.");
                Opacity = (double)Settings.Opacity/100;
                if (Settings.ActAsServer)
                    StartServer();

                if (Settings.ActAsClient)
                    StartClient();

                if (Settings.HideOnStartup)
                {
                    this.Opacity = 0;
                    Win32.PostMessage(this.Handle, (UInt32)WM.SYSCOMMAND, (UInt32)SC.CLOSE, 0);
                }
            }		
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                // When the app exits we need to un-shift any modify keys that might
                // have been pressed or they'll still be stuck after exit
                SendInputCommand.ShiftKey("shift", false);
                SendInputCommand.ShiftKey("ctrl", false);
                SendInputCommand.ShiftKey("alt", false);
                SendInputCommand.ShiftKey("lwin", false);
                SendInputCommand.ShiftKey("rwin", false);

                if (components != null) 
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.mainMenu = new System.Windows.Forms.MainMenu(this.components);
            this.menuItemFileMenu = new System.Windows.Forms.MenuItem();
            this.menuItemSendAwake = new System.Windows.Forms.MenuItem();
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.menuSettings = new System.Windows.Forms.MenuItem();
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.menuItemExit = new System.Windows.Forms.MenuItem();
            this.menuItemHelpMenu = new System.Windows.Forms.MenuItem();
            this.menuItemAbout = new System.Windows.Forms.MenuItem();
            this.statusBar = new System.Windows.Forms.StatusBar();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.notifyMenu = new System.Windows.Forms.ContextMenu();
            this.notifyMenuViewStatus = new System.Windows.Forms.MenuItem();
            this.menuItem3 = new System.Windows.Forms.MenuItem();
            this.notifyMenuItemSettings = new System.Windows.Forms.MenuItem();
            this.menuItem4 = new System.Windows.Forms.MenuItem();
            this.notifyMenuItemExit = new System.Windows.Forms.MenuItem();
            this.Log = new System.Windows.Forms.TextBox();
            this.menuItemHelp = new System.Windows.Forms.MenuItem();
            this.menuItemSupport = new System.Windows.Forms.MenuItem();
            this.menuItemEditCommands = new System.Windows.Forms.MenuItem();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemFileMenu,
            this.menuItemHelpMenu});
            // 
            // menuItemFileMenu
            // 
            this.menuItemFileMenu.Index = 0;
            this.menuItemFileMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemSendAwake,
            this.menuItem2,
            this.menuSettings,
            this.menuItemEditCommands,
            this.menuItem1,
            this.menuItemExit});
            this.menuItemFileMenu.Text = "&File";
            // 
            // menuItemSendAwake
            // 
            this.menuItemSendAwake.Index = 0;
            this.menuItemSendAwake.Text = "Send &Awake Signal";
            this.menuItemSendAwake.Click += new System.EventHandler(this.menuItemSendAwake_Click);
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 1;
            this.menuItem2.Text = "-";
            // 
            // menuSettings
            // 
            this.menuSettings.Index = 2;
            this.menuSettings.Text = "&Settings...";
            this.menuSettings.Click += new System.EventHandler(this.menuSettings_Click);
            // 
            // menuItem1
            // 
            this.menuItem1.Index = 4;
            this.menuItem1.Text = "-";
            // 
            // menuItemExit
            // 
            this.menuItemExit.Index = 5;
            this.menuItemExit.Text = "E&xit";
            this.menuItemExit.Click += new System.EventHandler(this.menuItemExit_Click);
            // 
            // menuItemHelpMenu
            // 
            this.menuItemHelpMenu.Index = 1;
            this.menuItemHelpMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemHelp,
            this.menuItemSupport,
            this.menuItemAbout});
            this.menuItemHelpMenu.Text = "&Help";
            // 
            // menuItemAbout
            // 
            this.menuItemAbout.Index = 2;
            this.menuItemAbout.Text = "&About";
            this.menuItemAbout.Click += new System.EventHandler(this.menuItemAbout_Click);
            // 
            // statusBar
            // 
            this.statusBar.Location = new System.Drawing.Point(0, 205);
            this.statusBar.Name = "statusBar";
            this.statusBar.Size = new System.Drawing.Size(368, 20);
            this.statusBar.TabIndex = 0;
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenu = this.notifyMenu;
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "MCE Controller";
            this.notifyIcon.Visible = true;
            this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // notifyMenu
            // 
            this.notifyMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.notifyMenuViewStatus,
            this.menuItem3,
            this.notifyMenuItemSettings,
            this.menuItem4,
            this.notifyMenuItemExit});
            // 
            // notifyMenuViewStatus
            // 
            this.notifyMenuViewStatus.Index = 0;
            this.notifyMenuViewStatus.Text = "&View Status...";
            this.notifyMenuViewStatus.Click += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // menuItem3
            // 
            this.menuItem3.Index = 1;
            this.menuItem3.Text = "-";
            // 
            // notifyMenuItemSettings
            // 
            this.notifyMenuItemSettings.Index = 2;
            this.notifyMenuItemSettings.Text = "&Settings...";
            this.notifyMenuItemSettings.Click += new System.EventHandler(this.menuSettings_Click);
            // 
            // menuItem4
            // 
            this.menuItem4.Index = 3;
            this.menuItem4.Text = "-";
            // 
            // notifyMenuItemExit
            // 
            this.notifyMenuItemExit.Index = 4;
            this.notifyMenuItemExit.Text = "&Exit";
            this.notifyMenuItemExit.Click += new System.EventHandler(this.menuItemExit_Click);
            // 
            // Log
            // 
            this.Log.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Log.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Log.Location = new System.Drawing.Point(0, 0);
            this.Log.Multiline = true;
            this.Log.Name = "Log";
            this.Log.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.Log.Size = new System.Drawing.Size(368, 208);
            this.Log.TabIndex = 1;
            this.Log.WordWrap = false;
            this.Log.TextChanged += new System.EventHandler(this.log_TextChanged);
            this.Log.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.log_KeyPress);
            // 
            // menuItemHelp
            // 
            this.menuItemHelp.Index = 0;
            this.menuItemHelp.Shortcut = System.Windows.Forms.Shortcut.F1;
            this.menuItemHelp.Text = "&Help...";
            this.menuItemHelp.Click += new System.EventHandler(this.menuItem5_Click);
            // 
            // menuItemSupport
            // 
            this.menuItemSupport.Index = 1;
            this.menuItemSupport.Text = "&Support...";
            this.menuItemSupport.Click += new System.EventHandler(this.menuItemSupport_Click);
            // 
            // menuItemEditCommands
            // 
            this.menuItemEditCommands.Index = 3;
            this.menuItemEditCommands.Text = "&Edit .commands File...";
            this.menuItemEditCommands.Click += new System.EventHandler(this.menuItemEditCommands_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(368, 225);
            this.Controls.Add(this.Log);
            this.Controls.Add(this.statusBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Menu = this.mainMenu;
            this.MinimizeBox = false;
            this.Name = "MainWindow";
            this.Text = "MCE Controller";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.MainWindow_Closing);
            this.Load += new System.EventHandler(this.MainWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        
        protected override void WndProc(ref Message m)  
        {
            // If the session is being logged off, or the machine is shutting
            // down...
            if (m.Msg==0x11) // WM_QUERYENDSESSION
            {
                // Allow shut down (m.Result may already be non-zero, but I set it
                // just in case)
                m.Result = (IntPtr)1;

                // Indicate to MainWindow_Closing() that we are shutting down;
                // otherwise it will just minimize to the tray
                ShuttingDown = true;
            }
            base.WndProc(ref m);
        }
        
 
        // When the app closes, dispose of the talker object
        protected override void OnClosed(EventArgs e)
        {
            if(Server!=null)
            {
                // remove our notification handler
                Server.Notifications -= new
                    SocketServer.NotificationCallback(HandleServerNotifications);
            
                Server.Dispose();
            }
            if(Client!=null)
            {
                // remove our notification handler
                Client.Notifications -= new 
                    SocketClient.NotificationCallback(HandleClientNotifications);
            
                Client.Dispose();
            }

            base.OnClosed(e);
        }

        private void MainWindow_Load(object sender, System.EventArgs e)
        {
            // Location can not be changed in constructor, has to be done here
            this.Location = Settings.WindowLocation;
            this.Size = Settings.WindowSize;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ShuttingDown)
            {
                // If we're NOT shutting down (the user hit the close button or pressed
                // CTRL-F4) minimize to tray.
                e.Cancel = true;

                // Hide the form and make sure the taskbar icon is visible
                notifyIcon.Visible = true;
                Hide();
            }
        }

        private void ShutDown()
        {
            AddLogEntry("ShutDown");
            // hide icon from the systray
            notifyIcon.Visible = false; 
            StopServer();
            StopClient();

            // Prevent access to the static MainWnd
            MainWnd = null;

            // Save the window size/location
            Settings.WindowLocation = this.Location;
            Settings.WindowSize = this.Size;
            Settings.Serialize();

            ShuttingDown = true;
            Close();
            Application.Exit();
        }
        private void StartServer()
        {
            if (Server == null)
            {
                Server = new SocketServer();
                Server.Notifications += new
                    SocketServer.NotificationCallback(HandleServerNotifications);
                Server.Start(Settings.ServerPort);
                menuItemSendAwake.Enabled = Settings.WakeupEnabled;
            }
            else
                AddLogEntry("Fatal Error: Attempt to StartServer() while an instance already exists!");
        }

        private void StopServer()
        {
            if (Server != null)
            {
                // remove our notification handler
                Server.Stop();            
                Server = null;
                menuItemSendAwake.Enabled = false;
            }
        }

        private void StartClient()
        {
            if (Client == null)
            {
                Client = new SocketClient(Settings);
                Client.Notifications += new 
                    SocketClient.NotificationCallback(HandleClientNotifications);
                Client.Start();
            }
            else
                AddLogEntry("Fatal Error: Attempt to StartClient() while an instance already exists!");
        }

        private void StopClient()
        {
            if (Client != null)
            {
                Client.Stop();
                Client = null;
            }
        }

        private void ReceivedData(String cmd)
        {
            FlashNotifyIcon();
            AddLogEntry("Command received: " + cmd);
            try
            {
                Commands.Execute(cmd);
            }
            catch (Exception e)
            {
                AddLogEntry("Command error: " + e.ToString());
            }
        }

        delegate void SetStatusBarCallback(string text);
        private void SetStatusBar(string text)
        {
            if (statusBar.InvokeRequired)
            {
                SetStatusBarCallback d = new SetStatusBarCallback(SetStatusBar);
                statusBar.Invoke(d, new object[] { text });
            }
            else
            {
                statusBar.Text = text;
                notifyIcon.Text = text;
            }
        }

        //
        // Notify callback for the TCP/IP Server
        //
        public void HandleServerNotifications(SocketServer.Notification notify, SocketServer.Status status, int client, String ipaddress, Object data)
        {
            String s = null;
            switch (notify)
            {
                case SocketServer.Notification.Initialized:
                    s = "Server: Initialized.";
                    break;

                case SocketServer.Notification.StatusChange:
                    switch (status)
                    {
                        case SocketServer.Status.Listening:
                            s = "Server: Waiting for clients to connect on port " + Settings.ServerPort.ToString();
                            SetStatusBar("Waiting for clients to connect on port " + Settings.ServerPort.ToString());
                            if (Settings.WakeupEnabled)
                                Server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
                            break;

                        case SocketServer.Status.Connected:
                            //s = String.Format("Server: Client #{0} at {1} connected.", client, ipaddress);
                            SetStatusBar("Clients connected, waiting for commands...");
                            return;

                        case SocketServer.Status.Stopped:
                            s = "Server: Stopped.";
                            SetStatusBar("Client/Sever Not Active");
                            if (Settings.WakeupEnabled)
                                Server.SendAwakeCommand(Settings.ClosingCommand, Settings.WakeupHost, Settings.WakeupPort);
                            break;
                    }
                    break;

                case SocketServer.Notification.ReceivedData:
                    ReceivedData((string)data);
                    return;

                case SocketServer.Notification.Error:
                    s = String.Format("Server: Error (Client #{0} at {1}: {2})", client, ipaddress, (String)data);
                    break;

                case SocketServer.Notification.ClientConnected:
                    s = String.Format("Server: Client #{0} at {1} connected.", client, ipaddress);
                    break;

                case SocketServer.Notification.ClientDisconnected:
                    s = String.Format("Server: Client #{0} at {1} has disconnected.", client, ipaddress);
                    break;

                case SocketServer.Notification.Wakeup:
                    s = "Wakeup: " + (string)data;
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
        public void HandleClientNotifications(SocketClient.Notification notify, Object data)
        {
            String s = null;
            switch (notify)
            {
                case SocketClient.Notification.Initialized:
                    //s = "Client: Client Initialized.";
                    break;

                case SocketClient.Notification.StatusChange:
                    SocketClient.Status status = (SocketClient.Status)data;
                    if (status == SocketClient.Status.Listening)
                    {
                        s = "Client: Connecting to " + Settings.ClientHost + ":" + Settings.ClientPort.ToString();
                        SetStatusBar("Connecting to " + Settings.ClientHost + ":" + Settings.ClientPort.ToString());
                    }
                    else if (status == SocketClient.Status.Connected)
                    {
                        s = "Client: Connected to " + Settings.ClientHost + ":" + Settings.ClientPort.ToString();
                        SetStatusBar("Connected to " + Settings.ClientHost + ":" + Settings.ClientPort.ToString() + ", waiting for commands...");
                    }
                    else if (status == SocketClient.Status.Closed)
                    {
                        s = "Client: Stopped.";
                        SetStatusBar("Client/Sever Not Active");                    }
                    else if (status == SocketClient.Status.Sleeping)
                    {
                        s = "Client: Waiting " + (Settings.ClientDelayTime/1000).ToString() + " seconds to connect.";
                        SetStatusBar("Waiting " + (Settings.ClientDelayTime/1000).ToString() + " seconds to connect.");
                    }
                    break;

                case SocketClient.Notification.ReceivedData:
                    ReceivedData((string)data);
                    return;

                case SocketClient.Notification.Error:
                    s = "Client Error: " + (string)data;

                    break;

                case SocketClient.Notification.End:
                    s = "Client: " + (string)data + " Reconnecting...";
                    Client.Start(true);
                    break;

                default:
                    s = "Unknown notification";
                    break;
            }
            AddLogEntry(s);
        }

        delegate void AddLogEntryCallback(string text);

        public static void AddLogEntry(String text)
        {
            if (MainWnd != null)
            {
                if (MainWnd.InvokeRequired)
                {
                    AddLogEntryCallback d = new AddLogEntryCallback(AddLogEntry);
                    MainWnd.Invoke(d, new object[] { text });
                }
                else
                    MainWnd.Log.AppendText("[" + DateTime.Now.ToString("yy'-'MM'-'dd' 'HH':'mm':'ss") + "] " + text + "\r\n");
            }
        }

        private void FlashNotifyIcon()
        {
            notifyIcon.Icon = DummyIcon;
            notifyIcon.Icon = this.Icon;
        }

        private void menuItemExit_Click(object sender, System.EventArgs e)
        {
            ShutDown();
        }

        private void notifyIcon_DoubleClick(object sender, System.EventArgs e)
        {
            // Show the form when the user double clicks on the notify icon.

            // Set the WindowState to normal if the form is minimized.
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;

            // Activate the form.
            notifyIcon.Visible = false;
            this.Activate();
            this.Show();
            Opacity = (double)Settings.Opacity/100;
        }

        private void menuItemAbout_Click(object sender, System.EventArgs e)
        {
            AboutBox a = new AboutBox();
            a.ShowDialog(this);
        }

        private void menuSettings_Click(object sender, System.EventArgs e)
        {
            SettingsDialog d = new SettingsDialog(Settings);
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                Settings = d.Settings;

                Opacity = (double)Settings.Opacity/100;

                StopClient();
                StopServer();

                if (Settings.ActAsServer)
                    StartServer();

                if (Settings.ActAsClient)
                    StartClient();

            }
        }

        // Prevent input into the edit box
        private void log_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        // Keep the end of the log visible and prevent it from overflowing
        private void log_TextChanged(object sender, System.EventArgs e)
        {
            // We don't want to overrun the size a textbox can handle
            // limit to 16k
            if (Log.TextLength > (16*1024))
            {
                Log.Text = Log.Text.Remove(0, Log.Text.IndexOf("\r\n")+2);
                Log.Select(Log.TextLength, 0);
            }
                
            Log.ScrollToCaret();
        }

        private void menuItemSendAwake_Click(object sender, System.EventArgs e)
        {
            Server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
        }

        private void menuItem5_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://tig.github.com/mcecontroller/");
            
        }

        private void menuItemSupport_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://sourceforge.net/projects/mcecontroller/support");
        }

        private void menuItemEditCommands_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Application.StartupPath);
        }

    }

}
