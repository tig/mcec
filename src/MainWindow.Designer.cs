using System.Windows.Forms;
namespace MCEControl
{
    partial class MainWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
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
        private ToolStripMenuItem docsMenuItem;
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
        private NotifyIcon notifyIcon;
        private ContextMenu notifyMenu;
        private MenuItem notifyExitMenuItem;
        private ToolStripMenuItem settingsMenuItem;


        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
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
            this.docsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.logTextBox.Cursor = System.Windows.Forms.Cursors.Default;
            this.logTextBox.Font = new System.Drawing.Font("Lucida Console", 8F);
            this.logTextBox.HideSelection = false;
            this.logTextBox.Location = new System.Drawing.Point(0, 24);
            this.logTextBox.Margin = new System.Windows.Forms.Padding(0);
            this.logTextBox.Multiline = true;
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.logTextBox.Size = new System.Drawing.Size(597, 215);
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
            this.statusStrip.Location = new System.Drawing.Point(0, 224);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.ShowItemToolTips = true;
            this.statusStrip.Size = new System.Drawing.Size(597, 37);
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
            this.statusStripStatus.Size = new System.Drawing.Size(123, 32);
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
            this.statusStripClient.Size = new System.Drawing.Size(70, 32);
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
            this.statusStripServer.Size = new System.Drawing.Size(71, 32);
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
            this.statusStripSerial.Size = new System.Drawing.Size(67, 32);
            this.statusStripSerial.Text = "Serial";
            this.statusStripSerial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusStripSerial.DoubleClick += new System.EventHandler(this.statusStripSerial_Click);
            // 
            // menuStrip
            // 
            this.menuStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenu,
            this.commandsMenu,
            this.helpToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Padding = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip.Size = new System.Drawing.Size(597, 24);
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
            this.fileMenu.Size = new System.Drawing.Size(37, 20);
            this.fileMenu.Text = "&File";
            // 
            // settingsMenuItem
            // 
            this.settingsMenuItem.Name = "settingsMenuItem";
            this.settingsMenuItem.Size = new System.Drawing.Size(125, 22);
            this.settingsMenuItem.Text = "&Settings...";
            this.settingsMenuItem.Click += new System.EventHandler(this.settingsMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(122, 6);
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(125, 22);
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
            this.commandsMenu.Size = new System.Drawing.Size(81, 20);
            this.commandsMenu.Text = "&Commands";
            // 
            // showCommandsMenuItem
            // 
            this.showCommandsMenuItem.Name = "showCommandsMenuItem";
            this.showCommandsMenuItem.Size = new System.Drawing.Size(212, 22);
            this.showCommandsMenuItem.Text = "Show &Commands...";
            this.showCommandsMenuItem.Click += new System.EventHandler(this.commandsMenuItem_Click);
            // 
            // openCommandsFolderMenuItem
            // 
            this.openCommandsFolderMenuItem.Name = "openCommandsFolderMenuItem";
            this.openCommandsFolderMenuItem.Size = new System.Drawing.Size(212, 22);
            this.openCommandsFolderMenuItem.Text = "&Open .commands folder...";
            this.openCommandsFolderMenuItem.Click += new System.EventHandler(this.openCommandsFolderMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(209, 6);
            // 
            // sendAwakeMenuItem
            // 
            this.sendAwakeMenuItem.Name = "sendAwakeMenuItem";
            this.sendAwakeMenuItem.Size = new System.Drawing.Size(212, 22);
            this.sendAwakeMenuItem.Text = "Send &Awake Signal";
            this.sendAwakeMenuItem.Click += new System.EventHandler(this.sendAwakeMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.docsMenuItem,
            this.toolStripSeparator3,
            this.checkUpdatesMenuItem,
            this.toolStripSeparator4,
            this.aboutMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "&Help";
            // 
            // docsMenuItem
            // 
            this.docsMenuItem.Name = "docsMenuItem";
            this.docsMenuItem.Size = new System.Drawing.Size(170, 22);
            this.docsMenuItem.Text = "&Documentation...";
            this.docsMenuItem.Click += new System.EventHandler(this.docsMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(167, 6);
            // 
            // checkUpdatesMenuItem
            // 
            this.checkUpdatesMenuItem.Name = "checkUpdatesMenuItem";
            this.checkUpdatesMenuItem.Size = new System.Drawing.Size(170, 22);
            this.checkUpdatesMenuItem.Text = "&Check for updates";
            this.checkUpdatesMenuItem.Click += new System.EventHandler(this.updatesMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(167, 6);
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Size = new System.Drawing.Size(170, 22);
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(597, 261);
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
            this.HelpButtonClicked += new System.ComponentModel.CancelEventHandler(this.docsMenuItem_Click);
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
    }
}

