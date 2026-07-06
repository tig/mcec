//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using log4net;
using MCEControl.Dialogs;
using Microsoft.Win32;
using static MCEControl.Hooks.PowerNativeMethods;

namespace MCEControl; 
public partial class MainWindow : Form, IAppHost {
    // MainWindow is a singleton, but NOT a lazy one (#209): the instance is explicitly assigned by
    // Program.Main's GUI path before Application.Run. It used to be a Lazy<MainWindow>, so ANY touch
    //; including from engine code on a worker thread in headless --mcp mode; silently constructed
    // the Form. Now a touch before assignment (or ever, headless) throws a pointed exception instead.
    public static MainWindow Instance {
        get => field ?? throw new InvalidOperationException(
            "MainWindow.Instance touched in headless mode or before Program assigned it; code below " +
            "the UI layer must use the AgentRuntime seam instead (AgentRuntime.Invoker / SendLine / " +
            "RequestShutdown / MessageWindowHandle).");
        internal set;
    }

    // The live transport instances now live on the ServiceController descriptors (#211);
    // these properties remain for the code that addresses a transport directly (SendLine,
    // the Send Awake menu item, the server wakeup quirk).
    private SocketServer? Server => _serverController.Instance as SocketServer;
    private SocketClient? Client => _clientController.Instance as SocketClient;
    private SerialServer? SerialServer => _serialController.Instance as SerialServer;

    // Per-transport descriptors (#211): built by the Create*Controller factories in the
    // constructor (readonly; no construction window exists); ONE generic
    // start/stop/toggle/paint/log path iterates _serviceControllers instead of the old three
    // copy-pasted method families.
    private readonly ServiceController _serverController;
    private readonly ServiceController _clientController;
    private readonly ServiceController _serialController;
    private readonly List<ServiceController> _serviceControllers;

    // Read-only status entry for the MCP/HTTP agent front door (#211). AgentServer is static
    // with no lifecycle events (making it a real service is #215), so this is repainted from
    // Start()/Stop(); the only places the door is started/stopped.
    private readonly ToolStripStatusLabel _statusStripMcp;

    // The command dispatcher (#195): created by LoadCommands during mainWindow_Load and replaced
    // whenever the commands file changes. The nullable FIELD models the real pre-load window; the
    // non-nullable property turns a too-early touch into a pointed error instead of a silent NRE.
    private CommandInvoker? _invoker;
    public CommandInvoker Invoker {
        get => _invoker ?? throw new InvalidOperationException(
            "MainWindow.Invoker touched before mainWindow_Load ran LoadCommands.");
        private set => _invoker = value;
    }

    private CommandWindow? _cmdWindow;
    private CommandFileWatcher? _watcher;

    // The on-screen command overlay (#119), when enabled. Null in headless mode or when disabled.
    private CommandOverlayWindow? _commandOverlay;

    // Emergency stop (#135): the "Re-arm" affordance, shown on the menu only while a stop is engaged.
    private ToolStripMenuItem? _rearmMenuItem;
    private bool _emergencyStopArmed;

    // Indicates whether user hit the close box (minimize)
    // or the app is exiting
    private bool _shuttingDown;

    // #213: both exit paths (menu exit and OS logoff) converge on PerformShutdown(); this gate
    // makes that teardown run exactly once.
    private readonly OnceGate _shutdownGate = new();

    // Settings: applied by ApplySettings (the single apply path; load and dialog-OK). The nullable
    // backing field models the real pre-load window (the controller lambdas read Settings lazily
    // and only run post-load); the non-nullable property turns a too-early touch into a pointed
    // error instead of a silent NRE.
    public AppSettings Settings {
        get => field ?? throw new InvalidOperationException(
            "MainWindow.Settings touched before mainWindow_Load applied settings (ApplySettings).");
        private set;
    }

    public MainWindow() {
        InitializeComponent();
        Logger.Instance.LogTextBox = logTextBox;
        // menuStrip (the designer field), not Form.MainMenuStrip: same object, but the designer
        // field is non-nullable after InitializeComponent while MainMenuStrip is Control?-typed.
        logTextBox.Font = new Font(logTextBox.Font.FontFamily, menuStrip.Font.SizeInPoints - 1,
            FontStyle.Regular, GraphicsUnit.Point);

        notifyIcon.Icon = Icon;
        ShowInTaskbar = true;

        _serverController = CreateServerController();
        _clientController = CreateClientController();
        _serialController = CreateSerialController();
        _serviceControllers = [_serverController, _serialController, _clientController];

        _statusStripMcp = CreateMcpStatusLabel();
        statusStrip.Items.Add(_statusStripMcp);

        // The status label is a Spring item; its width changes with the window, so re-fit the
        // (possibly ellipsized) status text whenever the strip is resized.
        statusStrip.SizeChanged += (s, e) => ApplyStatusText();

        SetStatus("");
        sendAwakeMenuItem.Enabled = false;
        installLatestVersionMenuItem.Enabled = false;
    }

    // ----------------------------------------
    // Per-transport descriptor factories (#211). Everything transport-specific; construction,
    // start arguments, status-strip item, status formatting, and quirks (server wakeup, client
    // restart-on-error, the client's hide-command-window side effect); lives here; the
    // start/stop/toggle/paint/log machinery below is generic. The lambdas read
    // <see cref="Settings"/> lazily, so building these before settings are loaded is safe.

    private ServiceController CreateServerController() =>
        new() {
            Name = "SocketServer",
            Create = () => new SocketServer(),
            StartTransport = (s, _) => ((SocketServer)s).Start(Settings.ServerPort, Settings.SocketServerBindAddress),
            StopTransport = s => ((SocketServer)s).Stop(),
            StatusStripItem = statusStripServer,
            StatusStripText = () => $"Server on port {Settings.ServerPort}",
            FormatStatus = (status, _) => FormatServerStatus(status, Settings.ServerPort),
            // Wakeup quirk: send the wakeup command when the server starts, the closing command
            // when it reports Stopped. (As before #211, an operator-initiated stop unsubscribes
            // handlers before Stop(), so the closing command fires only when the server itself
            // reports Stopped; e.g. a failed start.) The pattern guard replaces the old
            // null-forgiving Server!: the quirk only fires while the instance is wired, but a
            // vanished instance now degrades to a no-op instead of an NRE.
            StatusQuirk = status => {
                if (!Settings.WakeupEnabled || Server is not { } server) {
                    return;
                }
                if (status == ServiceStatus.Started) {
                    server.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
                }
                else if (status == ServiceStatus.Stopped) {
                    server.SendAwakeCommand(Settings.ClosingCommand, Settings.WakeupHost, Settings.WakeupPort);
                }
            },
            AfterStart = () => sendAwakeMenuItem.Enabled = Settings.WakeupEnabled,
            AfterStop = () => sendAwakeMenuItem.Enabled = false,
            IsConfigured = () => Settings.ActAsServer,
        };

    private ServiceController CreateClientController() =>
        new() {
            Name = "Client",
            Create = () => new SocketClient(Settings),
            StartTransport = (s, delay) => ((SocketClient)s).Start(delay),
            StopTransport = s => ((SocketClient)s).Stop(),
            StatusStripItem = statusStripClient,
            StatusStripText = () => $"Client {Settings.ClientHost}:{Settings.ClientPort}",
            FormatStatus = (status, _) => FormatClientStatus(status, Settings.ClientHost, Settings.ClientPort, Settings.ClientDelayTime),
            // Restart-on-error: any client error tears the connection down and (when a reconnect
            // delay is configured) schedules a delayed reconnect; same contract as before #211.
            ErrorQuirk = _ => RestartClient(),
            // Long-standing StopClient side effect, made explicit (#211): stopping the client
            // also hides the command window, so the operator is not left typing commands into a
            // connection that no longer exists.
            AfterStop = () => {
                if (_cmdWindow != null) {
                    _cmdWindow.Visible = false;
                }
            },
            IsConfigured = () => Settings.ActAsClient,
        };

    private ServiceController CreateSerialController() =>
        new() {
            Name = "SerialServer",
            Create = () => new SerialServer(),
            StartTransport = (s, _) => ((SerialServer)s).Start(Settings.SerialServerPortName,
                Settings.SerialServerBaudRate,
                Settings.SerialServerParity,
                Settings.SerialServerDataBits,
                Settings.SerialServerStopBits,
                Settings.SerialServerHandshake),
            StopTransport = s => ((SerialServer)s).Stop(),
            StatusStripItem = statusStripSerial,
            // https://en.wikipedia.org/wiki/8-N-1
            StatusStripText = () => $"Serial {Settings.SerialServerBaudRate}/{Settings.SerialServerPortName} {Settings.SerialServerDataBits}-{Settings.SerialServerParity}-{Settings.SerialServerStopBits}-{Settings.SerialServerHandshake}",
            FormatStatus = FormatSerialStatus,
            IsConfigured = () => Settings.ActAsSerialServer,
        };

    private static ToolStripStatusLabel CreateMcpStatusLabel() =>
        new() {
            BackColor = SystemColors.Control,
            Image = Properties.Resources.Trafficlight_gray_icon,
            ImageAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(10, 3, 0, 2),
            Name = "statusStripMcp",
            RightToLeft = RightToLeft.No,
            Text = "MCP",
            TextAlign = ContentAlignment.MiddleLeft,
        };

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
        if (disposing) {
            // When the app exits we need to un-shift any modify keys that might
            // have been pressed or they'll still be stuck after exit
            Logger.Instance.Log4.Debug("Ensuring shift key modifiers are reset...");
            SendInputCommand.ShiftKey("shift", false);
            SendInputCommand.ShiftKey("ctrl", false);
            SendInputCommand.ShiftKey("alt", false);
            SendInputCommand.ShiftKey("lwin", false);
            SendInputCommand.ShiftKey("rwin", false);

            components?.Dispose();

            // #211: one loop unwires and disposes every transport (the old code repeated
            // this per service, and again in the per-service Stop methods).
            foreach (ServiceController controller in _serviceControllers) {
                if (controller.Instance != null) {
                    UnwireService(controller);
                    (controller.Instance as IDisposable)?.Dispose();
                    controller.Instance = null;
                }
            }

            UpdateService.Instance.GotLatestVersion -= UpdateService_GotLatestVersion;

            EmergencyStop.StateChanged -= OnEmergencyStopStateChanged;
            if (_emergencyStopArmed) {
                EmergencyStop.Stop();
                _emergencyStopArmed = false;
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
            m.Result = 1;

            // Indicate to MainWindow_Closing() that we are shutting down;
            // otherwise it will just minimize to the tray
            _shuttingDown = true;
        }

        if (m.Msg == WM_POWERBROADCAST) {
            UserActivityMonitorService.Instance.HandlePowerBroadcast(m.WParam, m.LParam);
        }
        base.WndProc(ref m);
    }

    private void mainWindow_Load(object? sender, EventArgs e) {
        Logger.Instance.Log4.Info($"MCEC v{Application.ProductVersion}" +
            $" - OS: {Environment.OSVersion} on {(Environment.Is64BitProcess ? "x64" : "x86")}" +
            $" - .NET: {Environment.Version}");

#if _DEBUG
        IntPtr hWnd = WindowsInput.Native.NativeMethods.FindWindow(null, this.Text);
        var sb = new StringBuilder(256);
        WindowsInput.Native.NativeMethods.GetClassName(hWnd, sb, 256);
        Logger.Instance.Log4.Info($"Window Class - {sb}");
#endif
        // Load AppSettings (also configures logging; some logging already happened).
        // #216: SettingsStore.Load never shows UI; the GUI host (here) owns the failure dialogs
        // that used to live inside AppSettings.Deserialize, gated the same way (headless = no UI).
        SettingsLoadResult settingsLoad = SettingsStore.Load($@"{Program.ConfigPath}{SettingsStore.SettingsFileName}");
        ShowSettingsLoadFailure(settingsLoad, $@"{Program.ConfigPath}{SettingsStore.SettingsFileName}");
        ApplySettings(settingsLoad.Settings);

        // TELEMETRY:
        // what: Settings
        // why: To understand what settings get changed and which dont
        // how is PII protected: only settings clearly identified as not containing PII are collected
        TelemetryService.Instance.TrackEvent("Settings", settingsLoad.Settings.GetTelemetryDictionary());

        Logger.Instance.Log4.Info($"Logger: Logging to {Logger.Instance.LogFile}");

        // Telemetry
        TelemetryService.Instance.Start("MCE Controller");
        Logger.Instance.Log4.Info($"Telemetry: {(TelemetryService.Instance.TelemetryEnabled ? "Enabled" : "Disabled")}");

        // Commands
        _cmdWindow ??= new CommandWindow();

        LoadCommands();
        // watch .command file for changes
        _watcher = new CommandFileWatcher($@"{Program.ConfigPath}mcec.commands");
        _watcher.ChangedEvent += CmdTable_CommandsChangedEvent;

        if (Settings.HideOnStartup) {
            Opacity = 0;
            Win32NativeMethods.PostMessage(Handle, Win32NativeMethods.WM_SYSCOMMAND, Win32NativeMethods.SC_CLOSE, 0);
        }

        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

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

        // Updates - UpdateService.Instance.CheckVersion() is called from VisibleChanged

        UpdateService.Instance.GotLatestVersion += UpdateService_GotLatestVersion;
        UpdateService.Instance.StartPeriodicChecks(); // 24h recheck timer; needs the UI message loop (#214)

        SetUpEmergencyStopUi();

        Start();
    }

    /// <summary>
    /// Adds the emergency-stop (#135) "Re-arm" menu item and wires it to <see cref="EmergencyStop"/>. The
    /// item is a clear operator affordance that appears only while a stop is engaged; the latch is never
    /// cleared automatically. Built in code (not the designer) so the safety UI travels with the feature.
    /// </summary>
    private void SetUpEmergencyStopUi() {
        _rearmMenuItem = new ToolStripMenuItem("⛔ &Re-arm (Emergency Stop)") {
            Visible = false,
            ForeColor = Color.Firebrick,
        };
        _rearmMenuItem.Click += (_, _) => EmergencyStop.Rearm();
        menuStrip.Items.Add(_rearmMenuItem);

        EmergencyStop.StateChanged += OnEmergencyStopStateChanged;
    }

    private void OnEmergencyStopStateChanged(bool stopped) {
        // StateChanged fires on the global-hook thread; marshal to the UI thread to touch the menu.
        if (_rearmMenuItem is null) {
            return;
        }
        if (menuStrip.InvokeRequired) {
            menuStrip.BeginInvoke((Action)(() => OnEmergencyStopStateChanged(stopped)));
            return;
        }
        _rearmMenuItem.Visible = stopped;
        SetStatus(stopped
            ? $"⛔ STOPPED by operator; Re-arm to resume ({EmergencyStop.StoppedReason})"
            : $"Version: {Application.ProductVersion}");
    }

    private void UpdateService_GotLatestVersion(object? sender, Version? version) {
        if (InvokeRequired) {
            BeginInvoke((Action)(() => { UpdateService_GotLatestVersion(sender, version); }));
        }
        else {
            if (version == null || !String.IsNullOrWhiteSpace(UpdateService.Instance.ErrorMessage)) {
                Logger.Instance.Log4.Info(
                    $"Could not access {UpdateService.Instance.ReleasePageUri} to see if a newer version is available. {UpdateService.Instance.ErrorMessage}");
            }
            else if (UpdateService.Instance.CompareVersions() < 0) {
                installLatestVersionMenuItem.Enabled = true;
                Logger.Instance.Log4.Info("A newer version is available at");
                Logger.Instance.Log4.Info($"   {UpdateService.Instance.ReleasePageUri}");

                if (!Settings.DisableUpdatePopup)
                    UpdateDialog.Instance.ShowDialog(this);
            }
            else if (UpdateService.Instance.CompareVersions() > 0) {
                Logger.Instance.Log4.Info(
                    $"You are are running a more recent version than the latest published version ({UpdateService.Instance.ReleasePageUri})");
            }
            else {
                Logger.Instance.Log4.Info("You are running the most recent version");
            }
        }
    }

    private void CmdTable_CommandsChangedEvent(object? sender, EventArgs e) {
        // The watcher only exists after mainWindow_Load created the command window, but a change
        // event racing teardown degrades to a no-op instead of an NRE.
        if (_cmdWindow is not { } window) {
            return;
        }
        if (window.InvokeRequired) {
            window.BeginInvoke((Action)(() => { CmdTable_CommandsChangedEvent(sender, e); }));
        }
        else {
            LoadCommands();
        }
    }

    private void LoadCommands() {
        // #195: the invoker owns a dispatcher thread; stop the old one (dropping its queue; the
        // commands file changed, so what's queued is stale) before replacing it. The field, not
        // the property: on the first load there IS no old invoker, and that is not an error.
        _invoker?.Shutdown();

        // Create never returns null (it falls back to the built-in commands), so the old
        // "no invoker → hide the tray icon" branch was dead and is gone.
        Invoker = CommandInvoker.Create($@"{Program.ConfigPath}mcec.commands", Application.ProductVersion, Settings.DisableInternalCommands);
        AgentRuntime.Invoker = Invoker;
        _cmdWindow?.RefreshList();
        Logger.Instance.Log4.Info($"CommandInvoker: {Invoker.Values.Cast<Command>().Count(cmd => (cmd.Enabled))} " +
            $"commands enabled ({Invoker.Values.Cast<Command>().Count(cmd => (!cmd.Enabled))} commands disabled).");
    }

    // FormClosing, not the obsolete Closing (WFDEV004); FormClosingEventArgs derives from
    // CancelEventArgs, so the minimize-to-tray Cancel contract is unchanged.
    private void mainWindow_Closing(object? sender, FormClosingEventArgs e) {
        if (!_shuttingDown) {
            Logger.Instance.Log4.Info("Hiding Main Window...");
            // If we're NOT shutting down (the user hit the close button or pressed
            // CTRL-F4) minimize to tray.
            e.Cancel = true;

            // Hide the form and make sure the taskbar icon is visible
            notifyIcon.Visible = true;
            Hide();
        }
        else {
            Logger.Instance.Log4.Info("Closing Main Window...");

            // #213: two ways to get here with _shuttingDown set; menu exit (ShutDown() already ran
            // PerformShutdown() and then Close()d us; the gate makes this a no-op) and OS
            // logoff/shutdown (WM_QUERYENDSESSION set the flag and Windows closes the window; this
            // is the ONLY teardown that will run). The logoff path used to skip Stop() and the
            // settings save entirely; window size/location changes were silently lost on every
            // logoff and EmergencyStop/AgentServer relied on process teardown.
            PerformShutdown();
        }
    }

    /// <summary>
    /// The single, idempotent application teardown (#213), shared by BOTH exit paths: menu exit
    /// (<see cref="ShutDown"/>) and OS logoff/shutdown (WM_QUERYENDSESSION →
    /// <see cref="mainWindow_Closing"/>). Stops the services, persists window placement and
    /// settings via the SettingsStore path (#216), stops the command dispatcher (#195), and stops
    /// telemetry. Must run on the UI thread (both callers do).
    /// </summary>
    private void PerformShutdown() {
        if (!_shutdownGate.TryEnter()) {
            return;
        }

        Stop();

        // hide icon from the systray
        notifyIcon.Visible = false;

        // Save the window size/location
        Settings.WindowLocation = Location;
        Settings.WindowSize = Size;
        SaveSettings(Settings);

        // #195: stop the command dispatcher thread (drops anything still queued; a drop that
        // severs a command tree releases held input). The bounded join lets an in-flight
        // command usually finish cleanly; the thread is background so it can never keep the
        // process alive past that. The field: shutting down before any load is not an error.
        _invoker?.Shutdown(joinTimeoutMs: 2000);

        // #215: stop the dedicated UIA worker and dispose its cached UIA3Automation (bounded join).
        UiaService.Shutdown();

        // Save Commands
        // Stop file system watcher
        _watcher?.Dispose();
        _watcher = null;

        // BUGBUG: Why do we need to save when exiting the app? Could this be the cause of Issue #24?
        //Invoker.Save($@"{Program.ConfigPath}mcec.commands");
        TelemetryService.Instance.Stop();
    }

    private void Start() {
        // #211: one loop paints the initial light and starts every configured transport
        // (server, serial, client; the old per-service order).
        foreach (ServiceController controller in _serviceControllers) {
            PaintServiceStatus(controller, ServiceStatus.Stopped);
            if (controller.IsConfigured()) {
                StartService(controller);
            }
        }

        // MCEC 3.0: optional MCP/HTTP agent front door (localhost-bound, off by default).
        if (Settings.McpServerEnabled) {
            AgentServer.StartHttp();
        }
        PaintMcpStatus();

        // MCEC 3.0: emergency stop (#135); a global panic hotkey that halts an agent session from ANY
        // focused window. Arm it here in the GUI host (the low-level keyboard hook needs this thread's
        // message loop) whenever the agent front door could be driving. It reacts to physical input only,
        // so the agent can never trip or defeat it.
        if (!AgentRuntime.Headless && Settings.EmergencyStopEnabled && (Settings.McpServerEnabled || Settings.AgentCommandsEnabled)) {
            EmergencyStop.Start(EmergencyStopHotkey.ParseOrDefault(Settings.EmergencyStopHotkey));
            _emergencyStopArmed = true;
        }

        // MCEC 3.0: on-screen command overlay (#119); narrates each command as it executes so anyone
        // watching sees that MCEC is driving. Shown only when the agent front door is open (#292); a
        // normal instance (e.g. a socket server for a Media Center remote) must never show the
        // "controlling your PC" banner. Same front-door qualifier as the emergency stop above. Headless
        // --mcp hosts its copy on HeadlessOperatorUi's pump thread, never here (stdio is always a live
        // front door there). Independent (not owned) so it keeps narrating even when minimized to the tray.
        if (ShouldShowCommandOverlay(Settings) && !AgentRuntime.Headless && _commandOverlay is null) {
            _commandOverlay = new CommandOverlayWindow();
            _commandOverlay.Show();
        }

        if (Settings.ActivityMonitorEnabled) {
            UserActivityMonitorService.Instance.DebounceTime = Settings.ActivityMonitorDebounceTime;
            UserActivityMonitorService.Instance.ActivityMsg = Settings.ActivityMonitorCommand;
            UserActivityMonitorService.Instance.InputDetection = Settings.InputDetection;
            UserActivityMonitorService.Instance.UnlockDetection = Settings.UnlockDetection;
            UserActivityMonitorService.Instance.PowerBroadcastDetection = Settings.UserPresenceDetection;
            UserActivityMonitorService.Instance.LogActivity = Settings.LogUserActivity;
            UserActivityMonitorService.Instance.Start();
        }
    }

    // The command overlay (and its persistent "MCEC is controlling your PC" banner) only makes sense
    // when an agent could be driving; it must not appear on a normal, non-agent instance (#292). Gated
    // on the same front door as the emergency stop: the overlay narrates agent commands, so it belongs
    // exactly when the MCP/HTTP endpoint or the agent commands are enabled. CommandOverlayEnabled=false
    // still force-disables it. (Headless --mcp is handled by HeadlessOperatorUi, not this gate.)
    internal static bool ShouldShowCommandOverlay(AppSettings settings) {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.CommandOverlayEnabled && (settings.McpServerEnabled || settings.AgentCommandsEnabled);
    }

    private void Stop() {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)Stop);
        }
        else {
            UserActivityMonitorService.Instance.Stop();
            AgentServer.StopHttp();
            PaintMcpStatus();
            if (_emergencyStopArmed) {
                EmergencyStop.Stop();
                _emergencyStopArmed = false;
            }
            _commandOverlay?.Dispose();
            _commandOverlay = null;
            // #211: one loop stops every running transport.
            foreach (ServiceController controller in _serviceControllers) {
                StopService(controller);
            }
        }
    }

    private void ShutDown() {
        if (this.InvokeRequired) {
            this.BeginInvoke((MethodInvoker)ShutDown);
            return;
        }

        Logger.Instance.Log4.Info("Exiting app...");
        _shuttingDown = true;

        // #213: the one idempotent teardown, shared with the OS-logoff path (mainWindow_Closing).
        PerformShutdown();

        Close();
        Application.Exit();
    }

    // ----------------------------------------
    // Generic service wiring (#211): ONE start/stop/toggle path for every transport, driven by
    // the ServiceController descriptors. Before #211 each of these existed as three
    // near-identical per-transport copies (with a fourth copy of the teardown in Dispose).

    /// <summary>Creates the transport, wires the typed <see cref="ServiceBase"/> events to the
    /// generic handlers (handlers first, then start; so no event is missed), and starts it.</summary>
    /// <param name="controller">The per-transport descriptor to start.</param>
    /// <param name="delay">The client's "sleep before first connect" restart flag; other
    /// transports ignore it.</param>
    private void StartService(ServiceController controller, bool delay = false) {
        if (controller.Instance != null) {
            Logger.Instance.Log4.Debug($"{controller.Name}: Start attempted while an instance already exists!");
            return;
        }

        Logger.Instance.Log4.Info($"{controller.Name}: Starting...");
        ServiceBase service = controller.Create();
        controller.Instance = service;
        controller.StatusHandler = (status, detail) => OnServiceStatusChanged(controller, status, detail);
        // Producer-only (#195): commands are enqueued on whatever thread the transport
        // delivered them; deliberately NOT marshaled to the UI thread (see ReceivedData).
        controller.CommandHandler = ReceivedData;
        controller.ErrorHandler = error => OnServiceError(controller, error);
        service.StatusChanged += controller.StatusHandler;
        service.CommandReceived += controller.CommandHandler;
        service.ErrorOccurred += controller.ErrorHandler;
        controller.StartTransport(service, delay);
        controller.AfterStart?.Invoke();
    }

    /// <summary>Unwires the typed events, stops the transport, and drops the instance. Handlers
    /// are removed BEFORE Stop(); the same ordering the old per-service stops used; so an
    /// operator-initiated stop does not trigger status-change side effects (notably the server's
    /// closing wakeup command, which by long-standing behavior fires only when the server itself
    /// reports Stopped, e.g. on a failed start).</summary>
    private void StopService(ServiceController controller) {
        if (controller.Instance == null) {
            return;
        }

        Logger.Instance.Log4.Info($"{controller.Name}: Stopping...");
        ServiceBase service = controller.Instance;
        UnwireService(controller);
        controller.StopTransport(service);
        controller.Instance = null;
        controller.AfterStop?.Invoke();
        // The handlers were already removed, so the service's own Stopped status was not seen;
        // paint the final light explicitly. (The old per-service stops skipped even this,
        // leaving a stale light after a status-strip toggle.)
        PaintServiceStatus(controller, ServiceStatus.Stopped);
    }

    private static void UnwireService(ServiceController controller) {
        if (controller.Instance == null) {
            return;
        }
        controller.Instance.StatusChanged -= controller.StatusHandler;
        controller.Instance.CommandReceived -= controller.CommandHandler;
        controller.Instance.ErrorOccurred -= controller.ErrorHandler;
        controller.StatusHandler = null;
        controller.CommandHandler = null;
        controller.ErrorHandler = null;
    }

    private void ToggleService(ServiceController controller) {
        if (controller.Instance == null) {
            StartService(controller);
        }
        else {
            StopService(controller);
        }
    }

    private void RestartClient() {
        if (_cmdWindow != null) {
            if (this.InvokeRequired) {
                this.BeginInvoke((MethodInvoker)RestartClient);
            }
            else {
                StopService(_clientController);
                if (!_shuttingDown && Settings is { ActAsClient: true, ClientDelayTime: > 0 }) {
                    Logger.Instance.Log4.Info("Client: Reconnecting...");
                    StartService(_clientController, delay: true);
                }
            }
        }
    }

    private void ShowCommandWindow() {
        if (this.InvokeRequired) {
            TelemetryService.Instance.TrackEvent("ShowCommandWindow");
            this.BeginInvoke((MethodInvoker)ShowCommandWindow);
        }
        else if (_cmdWindow is not null) {
            _cmdWindow.Visible = Settings.ShowCommandWindow = true;
        }
    }

    /// <summary>
    /// Anytime a client or server receives data that looks like a command, this function is called.
    /// Producer-only (#195): decode + enqueue on whatever thread the transport delivered the data;
    /// the Invoker's own dispatcher thread executes. No UI-thread marshaling; commands no longer
    /// run on (or block) the UI thread.
    /// </summary>
    /// <param name="reply">The reply context any replies should be sent to</param>
    /// <param name="cmd">the command string</param>
    private void ReceivedData(Reply reply, String cmd) {
        try {
            Invoker.Enqueue(reply, cmd);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"Command: ({cmd}) error: {e}");
        }
    }

    // ----------------------------------------
    // IAppHost (#209); the GUI half of the AgentRuntime host seam (non-explicit; CA1033 forbids
    // explicit-only implementations on an unsealed type). SendLine below already matches.

    /// <summary>
    /// The GUI consent-prompt channel for <c>request-command-access</c> (#307), registered as
    /// <see cref="AgentConsent.Prompter"/> in <see cref="ApplySettings"/>. Marshals the modal
    /// <see cref="CommandAccessConsentDialog"/> onto the UI thread (unowned: this window may be
    /// hidden in the tray; the dialog is TopMost + CenterScreen) and blocks the calling MCP worker
    /// for the operator's decision. Null; reported as <c>consent-unavailable</c>, fail closed; when
    /// the window cannot host a dialog (torn down, no handle).
    /// </summary>
    private CommandAccessDecision? PromptCommandAccess(CommandAccessRequest request) {
        if (IsDisposed || !IsHandleCreated) {
            return null;
        }
        try {
            return Invoke(() => {
                using CommandAccessConsentDialog dialog = new(request);
                _ = dialog.ShowDialog();
                return dialog.Decision;
            });
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"MainWindow: could not show the consent prompt: {e.Message}");
            return null;
        }
    }

    // ShutDown() self-marshals (BeginInvoke when InvokeRequired), so this is callable from any
    // thread; the invoker's dispatcher (mcec:exit) and the updater's async download path both do.
    public void RequestShutdown() => ShutDown();

    // Control.Handle is safe to READ cross-thread once created; the only consumer
    // (UserActivityMonitorService.StartPowerBroadcastDetection) runs on the UI thread anyway,
    // called from Start() during load/settings-apply.
    public IntPtr MessageWindowHandle => Handle;

    // Sends a line of text (adds a "\n" to end) to connected client and server
    public void SendLine(string line) {
        //Logger.Instance.Log4.Info($"Send: {line}");
        if (Client != null) {
            Client.Send(line + "\n");
        }
        else if (Server != null) {
            Server.Send(line + "\n");
        }

        if (SerialServer != null) {
            SerialServer.Send(line + "\n");
        }
    }

    // The full, untruncated status text. The status label is a Spring item (it takes only the
    // space the Client/Server/Serial indicators leave over), so the displayed text is re-fitted
    // to that width on every set and on strip resize; the full text lives in the tooltip.
    private string _statusText = string.Empty;

    private void SetStatus(string text) {
        if (statusStrip.InvokeRequired) {
            statusStrip.BeginInvoke((Action)(() => { SetStatus(text); }));
        }
        else {
            _statusText = text ?? string.Empty;
            ApplyStatusText();
            // NotifyIcon.Text throws if longer than 127 chars. Status text can include the full
            // informational version (e.g. a long prerelease "x.y.z-branch.n+sha" string), so cap it.
            notifyIcon.Text = _statusText.Length > 127 ? _statusText[..124] + "..." : _statusText;
        }
    }

    /// <summary>
    /// Fits <see cref="_statusText"/> into the status label's current (Spring-allocated) width
    /// with a trailing ellipsis, so an arbitrarily long status (e.g. the full informational
    /// version) can never push the Client/Server/Serial indicators out of the strip. The full
    /// text is kept reachable as the label's tooltip when truncated. Called on every status set
    /// and on strip resize (the Spring width changes with the window).
    /// </summary>
    private void ApplyStatusText() {
        int width = statusStripStatus.ContentRectangle.Width;
        if (width <= 0) {
            // Not laid out yet (constructor-time SetStatus); the SizeChanged hook re-fits later.
            statusStripStatus.Text = _statusText;
            return;
        }
        string fitted = EllipsizedText.Fit(_statusText, width, statusStripStatus.Font);
        statusStripStatus.Text = fitted;
        statusStripStatus.ToolTipText = fitted == _statusText ? string.Empty : _statusText;
    }

    // ----------------------------------------
    // Generic service event handlers (#211): ONE status handler (paint + log + quirk) and ONE
    // error handler (log + quirk) replace the old three ~80-line per-transport switch handlers
    // and three byte-identical painters. These are also the ONE place transport-thread events
    // are marshaled to the UI thread (the old code repeated the InvokeRequired dance per
    // painter and left the rest of each handler running on the transport's thread).

    private void OnServiceStatusChanged(ServiceController controller, ServiceStatus status, string detail) {
        if (statusStrip.InvokeRequired) {
            statusStrip.BeginInvoke((Action)(() => OnServiceStatusChanged(controller, status, detail)));
            return;
        }
        PaintServiceStatus(controller, status);
        string? line = controller.FormatStatus(status, detail);
        if (line != null) {
            Logger.Instance.Log4.Info($"{controller.Name}: {line}");
        }
        controller.StatusQuirk?.Invoke(status);
    }

    private void OnServiceError(ServiceController controller, ServiceError error) {
        if (statusStrip.InvokeRequired) {
            statusStrip.BeginInvoke((Action)(() => OnServiceError(controller, error)));
            return;
        }
        Logger.Instance.Log4.Error($"{controller.Name}: Error: {error}");
        controller.ErrorQuirk?.Invoke(error);
    }

    private void PaintServiceStatus(ServiceController controller, ServiceStatus status) {
        if (statusStrip.InvokeRequired) {
            statusStrip.BeginInvoke((Action)(() => PaintServiceStatus(controller, status)));
            return;
        }
        controller.StatusStripItem.Text = controller.StatusStripText();
        Image? light = StatusLightImage(status);
        if (light != null) {
            controller.StatusStripItem.Image = light;
        }
    }

    private void PaintMcpStatus() {
        if (statusStrip.InvokeRequired) {
            statusStrip.BeginInvoke((Action)PaintMcpStatus);
            return;
        }
        _statusStripMcp.Text = $"MCP on port {Settings.McpHttpPort}";
        _statusStripMcp.Image = AgentServer.IsHttpListening
            ? Properties.Resources.Trafficlight_green_icon
            : Properties.Resources.Trafficlight_gray_icon;
    }

    // ----------------------------------------
    // Status formatting (#211); pure functions, extracted from the old per-transport handlers
    // so they can be unit tested. Null means "nothing to log for this status".

    /// <summary>Maps a status to its traffic light. The shipped icon set has only red, green,
    /// and gray (no yellow), so Started and Waiting both map to red; exactly what the three
    /// pre-#211 painters did in triplicate; a yellow-ish Waiting needs a new icon first.</summary>
    internal static StatusLight StatusLightFor(ServiceStatus status) => status switch {
        ServiceStatus.Connected => StatusLight.Green,
        ServiceStatus.Stopped => StatusLight.Gray,
        ServiceStatus.Started or ServiceStatus.Waiting => StatusLight.Red,
        _ => StatusLight.Unchanged,
    };

    private static Bitmap? StatusLightImage(ServiceStatus status) => StatusLightFor(status) switch {
        StatusLight.Green => Properties.Resources.Trafficlight_green_icon,
        StatusLight.Gray => Properties.Resources.Trafficlight_gray_icon,
        StatusLight.Red => Properties.Resources.Trafficlight_red_icon,
        _ => null,
    };

    internal static string? FormatServerStatus(ServiceStatus status, int port) => status switch {
        ServiceStatus.Started => $"Started on port {port}",
        ServiceStatus.Waiting => "Waiting for a client to connect",
        ServiceStatus.Stopped => "Stopped",
        _ => null,
    };

    internal static string? FormatClientStatus(ServiceStatus status, string host, int port, int delayTimeMs) => status switch {
        ServiceStatus.Started => $"Connecting to {host}:{port}",
        ServiceStatus.Connected => $"Connected to {host}:{port}",
        ServiceStatus.Stopped => "Stopped",
        ServiceStatus.Sleeping => $"Waiting {delayTimeMs / 1000} seconds to connect",
        _ => null,
    };

    internal static string? FormatSerialStatus(ServiceStatus status, string detail) => status switch {
        ServiceStatus.Started => $"Opening port: {detail}",
        ServiceStatus.Waiting => $"Waiting for commands on {detail}...",
        ServiceStatus.Stopped => "Stopped",
        _ => null,
    };

    private void ShowSettings(SettingsTab defaultTab) {
        SettingsDialog d = new SettingsDialog(Settings) {
            DefaultTab = defaultTab
        };

        TelemetryService.Instance.TrackEvent("ShowSettings");

        if (d.ShowDialog(this) == DialogResult.OK) {
            Stop();

            ApplySettings(d.Settings);

            // #213 ordering decision: persist AFTER the clone has been adopted as the active
            // settings object (ApplySettings, the single apply path) and BEFORE Start(). The
            // dialog itself no longer serializes (it used to commit to disk before the owner
            // applied anything). Saving before Start() is deliberate: Start() has no failure
            // contract; a service that cannot start with the new config logs the error and shows
            // it in the status bar, but does NOT roll back the in-memory settings; so disk must
            // match memory or the next exit/logoff (PerformShutdown saves too) would rewrite it
            // anyway. The user's OK is the commit point; a Cancel never touches disk.
            SaveSettings(Settings);

            Opacity = (double)Settings.Opacity / 100;

            Start();
        }
        d.Dispose();
    }

    /// <summary>
    /// Adopts <paramref name="settings"/> as the active settings object; for the GUI AND the
    /// UI-agnostic agent engine. This is the single apply path used both at load and when the
    /// Settings dialog is OK'd (the dialog hands back a deep clone, so the object identity changes
    /// and <see cref="AgentRuntime.Settings"/> MUST be re-published; see #196: security gates such
    /// as <c>AllowSessionProvisioning</c> and pacing read that seam, and a stale pre-dialog object
    /// would keep honoring old gate values until restart).
    /// </summary>
    private void ApplySettings(AppSettings settings) {
        Settings = settings;
        PublishAgentRuntimeSettings(settings);

        // #209: register the GUI host capabilities (SendLine / RequestShutdown / message-window
        // handle) on the same seam, in the same place the settings are published. Idempotent;
        // the dialog-OK path re-runs this with the same instance.
        AgentRuntime.Host = this;

        // #307: the request-command-access consent prompt shows on this window's UI thread.
        AgentConsent.Prompter = PromptCommandAccess;

        // LevelMap's indexer returns null for a name it does not know (e.g. a hand-edited
        // settings file); keep the current threshold instead of dereferencing the miss.
        log4net.Core.Level? threshold = LogManager.GetLogger("MCEControl").Logger.Repository?.LevelMap[settings.TextBoxLogThreshold];
        if (threshold is null) {
            Logger.Instance.Log4.Warn($"Unknown TextBoxLogThreshold '{settings.TextBoxLogThreshold}'; keeping the current threshold.");
        }
        else {
            Logger.Instance.TextBoxThreshold = threshold;
        }

        // #259: log the agent surface state and provisioned instances like any other setting; here (the
        // dialog-OK path re-runs ApplySettings) it fires on change, and OnLoad's ApplySettings fires it
        // at startup.
        Program.LogAgentState(settings);
    }

    /// <summary>
    /// Publishes the settings object to the UI-agnostic agent engine seam
    /// (capture/query/find/invoke + MCP/HTTP read gating and pacing from
    /// <see cref="AgentRuntime.Settings"/>). Static and internal so the regression test can
    /// exercise the republish contract headlessly without constructing a Form
    /// (InternalsVisibleTo MCEControl.xUnit).
    /// </summary>
    internal static void PublishAgentRuntimeSettings(AppSettings settings) {
        AgentRuntime.Settings = settings;
    }

    /// <summary>
    /// GUI-host save path (#216): persists <paramref name="settings"/> via
    /// <see cref="SettingsStore.TrySave"/> and shows the save-failure MessageBox that used to live
    /// inside <c>AppSettings.Serialize</c>, gated the same way (never when headless). Used by
    /// <see cref="ShutDown"/> and the Settings dialog OK path.
    /// </summary>
    private static void SaveSettings(AppSettings settings) {
        string settingsFile = $@"{Program.ConfigPath}{SettingsStore.SettingsFileName}";
        if (!SettingsStore.TrySave(settingsFile, settings, out Exception? error) && !AgentRuntime.Headless) {
            MessageBox.Show($"Settings file could not be written. {settingsFile} {error?.Message}");
        }
    }

    /// <summary>
    /// GUI-host load-failure UI (#216): shows the MessageBoxes that used to live inside
    /// <c>AppSettings.Deserialize</c>, with the same messages and the same headless gate. A clean
    /// load (or a normal first-run default creation) shows nothing.
    /// </summary>
    private static void ShowSettingsLoadFailure(SettingsLoadResult result, string settingsFile) {
        if (AgentRuntime.Headless) {
            return;
        }
        switch (result.Outcome) {
            case SettingsLoadOutcome.ParseError:
                MessageBox.Show($"Settings file is corrupt or invalid: {settingsFile}\n\n{result.ErrorDetail}\n\n" +
                    "MCE Controller will use default settings for this run. The file was not overwritten - fix or delete it to recover your settings.");
                break;
            case SettingsLoadOutcome.AccessDenied:
            case SettingsLoadOutcome.UnexpectedError:
                MessageBox.Show($"Settings file could not be loaded. {result.ErrorDetail}\n\nMCE Controller will use default settings for this run.");
                break;
            case SettingsLoadOutcome.CreatedDefault when result.Error is not null:
                // First run, but the default settings file could not be written.
                MessageBox.Show($"Settings file could not be written. {settingsFile} {result.ErrorDetail}");
                break;
        }
    }

    // ----------------------------------------
    // User action handlers
    private void exitMenuItem_Click(object? sender, EventArgs e) {
        ShutDown();
    }

    private void notifyIcon_DoubleClick(object? sender, EventArgs e) {
        // Show the form when the user double clicks on the notify icon.

        // Set the WindowState to normal if the form is minimized.
        if (WindowState == FormWindowState.Minimized) {
            WindowState = FormWindowState.Normal;
        }

        // Activate the form.
        notifyIcon.Visible = false;
        Activate();
        Show();
        Opacity = (double)Settings.Opacity / 100;
    }

    private void aboutMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("aboutMenuItem");
        About a = new About();
        a.ShowDialog(this);
        a.Dispose();
    }

    private void settingsMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("settingsMenuItem");
        ShowSettings(SettingsTab.General);
    }

    private void sendAwakeMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("sendAwakeMenuItem");

        // The menu item is only enabled while the server runs, but Enabled toggles race the
        // click queue; a vanished server degrades to a no-op instead of an NRE.
        Server?.SendAwakeCommand(Settings.WakeupCommand, Settings.WakeupHost, Settings.WakeupPort);
    }

    private void commandsMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("commandsMenuItem");
        ShowCommandWindow();
    }
    private void openCommandsFolderMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("openCommandsFolderMenuItem");

        Program.LaunchExternal(Program.ConfigPath);
    }


    private void docsMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("docsMenuItem");

        Program.LaunchExternal("https://tig.github.io/mcec/");
    }

    private void updatesMenuItem_Click(object? sender, EventArgs e) {
        TelemetryService.Instance.TrackEvent("updatesMenuItem");
        UpdateDialog.Instance.ShowDialog(this);
    }

    private void statusStripClient_Click(object? sender, EventArgs e) {
        if (_clientController.IsConfigured()) {
            ToggleService(_clientController);
        }
        else {
            ShowSettings(SettingsTab.Client);
        }
    }

    private void statusStripServer_Click(object? sender, EventArgs e) {
        if (_serverController.IsConfigured()) {
            ToggleService(_serverController);
        }
        else {
            ShowSettings(SettingsTab.Server);
        }
    }

    private void statusStripSerial_Click(object? sender, EventArgs e) {
        ShowSettings(SettingsTab.Serial);
    }

    private void statusStripStatus_Click(object? sender, EventArgs e) {
        ShowSettings(SettingsTab.General);
    }

    // WinForms layout with MenuStrip and StatusStrip has issues (apparently) with
    // Anchor. This works around that.
    private void MainWindow_Layout(object? sender, LayoutEventArgs e) {
        // Adjust vertical location & height of TextBox to deal with font scaling changes.
        // Note we add a little margin on the left
        logTextBox.Location = new Point(4, menuStrip.Height);
        logTextBox.Size = new Size(this.ClientSize.Width - logTextBox.Location.X, this.ClientSize.Height - menuStrip.Height - statusStrip.Height);
    }

    private void SystemEvents_UserPreferenceChanged(object? sender, EventArgs e) {
        logTextBox.Font = new Font(logTextBox.Font.FontFamily, menuStrip.Font.SizeInPoints - 1, FontStyle.Regular, GraphicsUnit.Point);
    }

    private void MainWindow_VisibleChanged(object? sender, EventArgs e) {
        if (Visible)
            UpdateService.Instance.CheckVersion();
    }
}
