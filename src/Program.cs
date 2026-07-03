// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Diagnostics;
using Terminal.Gui.Cli;

namespace MCEControl;

internal static class Program {
    internal static string ConfigPath {
        get {
            // Get dir of mcec.exe
            string path = AppDomain.CurrentDomain.BaseDirectory;

            // If we're running from a (read-only) Program Files install location, redirect log/
            // settings/command files to %AppData%.
            string? programFiles = GetProgramFilesRoot(path);
            if (programFiles is not null) {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                path = $@"{appdata}\{path.Substring(programFiles.Length + 1)}";
            }

            return path;
        }
    }

    /// <summary>
    ///     Returns the Program Files root <paramref name="path" /> lives under, or null when it is not
    ///     an installed location. Checks both 64-bit ("Program Files") and 32-bit ("Program Files
    ///     (x86)") roots; the installer puts the self-contained x64 build under 64-bit Program Files,
    ///     while older installs used the x86 path. Shared by <see cref="ConfigPath" /> (the %AppData%
    ///     redirect) and <see cref="IsProgramFilesInstall" /> (the agent-serving refusal).
    /// </summary>
    internal static string? GetProgramFilesRoot(string path) {
        foreach (Environment.SpecialFolder folder in new[] {
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86
                 }) {
            string programFiles = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(programFiles)) {
                continue;
            }

            // Directory-boundary match, not a plain StartsWith: "C:\Program Files (x86)\..." has
            // "C:\Program Files" as a string prefix, so a bare prefix test would report the x86
            // install under the 64-bit root (and mangle the ConfigPath %AppData% redirect); and a
            // sibling like "C:\Program FilesExtra\..." would falsely match. The path must equal the
            // root or sit under it (root + separator).
            if (path.Equals(programFiles, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                return programFiles;
            }
        }

        return null;
    }

    /// <summary>TEST SEAM: forces <see cref="IsProgramFilesInstall" /> for tests.</summary>
    internal static bool? IsProgramFilesInstallOverrideForTests { get; set; }

    /// <summary>
    ///     True when the running exe lives under Program Files; the installed, operator-owned copy.
    ///     SECURITY: the installed copy must never serve agents (no <c>--mcp</c>, no MCP/HTTP server).
    ///     Serving from it requires enabling agent security gates in the one config the operator's own
    ///     MCEC reads (redirected to %AppData%), where a crashed or killed session leaks them enabled;
    ///     exactly what isolated session provisioning (#138) exists to prevent. Agent serving is only
    ///     allowed from non-installed locations (a provisioned session or a manual copy), which read a
    ///     disposable co-located config instead.
    /// </summary>
    internal static bool IsProgramFilesInstall =>
        IsProgramFilesInstallOverrideForTests ??
        GetProgramFilesRoot(AppDomain.CurrentDomain.BaseDirectory) is not null;

    /// <summary>
    ///     The operator-facing explanation both refusal sites share (the <c>--mcp</c> exit and the
    ///     MCP/HTTP server start).
    /// </summary>
    internal const string InstalledAgentServingGuidance =
        "MCEC will not serve agents from its installed (Program Files) location: enabling agent " +
        "security gates in the installed copy's configuration would leak them enabled if a session " +
        "crashed. Either (1) run the installed MCEC normally and have your agent call the " +
        "'provision-session' MCP tool (requires AllowSessionProvisioning=true in Settings) to get a " +
        "disposable, isolated copy to drive, or (2) copy the MCEC install directory somewhere " +
        "writable and run it from there; a non-installed copy reads its own co-located mcec.settings. " +
        "See the Agent Server documentation (docs/agent-server.md).";

    /// <summary>
    ///     Safely launches a URL, file, or folder using the shell (UseShellExecute).
    ///     Catches launch errors (e.g. no browser association), logs them, and shows a non-fatal message.
    /// </summary>
    internal static void LaunchExternal(string target) {
        if (string.IsNullOrWhiteSpace(target)) {
            return;
        }

        try {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"Failed to launch external target '{target}': {ex.Message}");
            try {
                MessageBox.Show(
                    @$"MCEC could not open:\n{target}\n\n{ex.Message}",
                    @"MCEC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch {
                // UI may not be available (e.g. headless)
            }
        }
    }

    /// <summary>
    ///     The main entry point. Three dispatch shapes: no args → the WinForms GUI (unchanged);
    ///     <c>mcp</c> / legacy <c>--mcp</c> → the headless MCP stdio server, intercepted BEFORE the CLI
    ///     host because it owns the process for its lifetime and stdout is the JSON-RPC stream
    ///     (Terminal.Gui must never initialize around it); anything else → the Terminal.Gui.Cli surface
    ///     (<c>--help</c>, <c>--version</c>, <c>--opencli</c>, <c>agent-guide</c>).
    /// </summary>
    [STAThread]
    private static int Main(string[] args) {
        // mcec.exe is a WinExe: it has no console unless the parent hands it one. Attaching to the
        // parent's console (best effort; fails harmlessly when there is none or stdio is piped)
        // makes the CLI surface and error messages visible when run from a terminal. VT processing
        // must then be enabled explicitly: an attached GUI-subsystem process does not inherit the
        // shell's ANSI-enabled output mode the way a console app does, and Terminal.Gui.Cli's help
        // rendering emits ANSI.
        if (args.Length > 0) {
            _ = ConsoleNativeMethods.AttachConsole(ConsoleNativeMethods.AttachParentProcess);
            ConsoleNativeMethods.TryEnableVtProcessing();
        }

        // Start logging. The START banner is logged in Bootstrap (the app modes), not here: the
        // CLI surface (--help/--version/--opencli) must not spray log lines onto the console it
        // just attached to.
        Logger.Instance.LogFile = $@"{ConfigPath}mcec.log";

        if (args.Length == 0) {
            Bootstrap();
            RunGui();
            return 0;
        }

        if (IsMcpInvocation(args)) {
            Bootstrap();
            return RunHeadlessMcp();
        }

        // The CLI surface runs no app mode, so it skips Bootstrap (a --version query should not
        // migrate configs or reap session directories).
        return RunCli(args);
    }

    /// <summary>App-mode initialization shared by the GUI and MCP paths (not the CLI surface).</summary>
    private static void Bootstrap() {
        Logger.Instance.Log4.Debug(
            $"------ START: v{Application.ProductVersion} - OS: {Environment.OSVersion} on {(Environment.Is64BitProcess ? "x64" : "x86")} - .NET: {Environment.Version.ToString()} ------");

        // v3.0: carry an existing user's MCEControl.settings/.commands forward to the new mcec.* names.
        ConfigMigration.Run(ConfigPath);

        // #138: belt-and-suspenders; reap any stale/abandoned provisioned session directories so a leaked
        // session (crashed/killed before teardown) never lingers. Running sessions' files are locked and skipped.
        SessionProvisioner.ReapOrphans(TimeSpan.FromHours(AgentServer.SessionReapAgeHours));

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// Logs whether the agent front door is open and every provisioned instance that exists (#259).
    /// Logged like any other setting: at startup AND whenever the settings are (re)applied (the GUI host
    /// calls this from <c>ApplySettings</c>, which the dialog-OK path re-runs; the headless host calls it
    /// once at startup, its only apply point). Makes plain, in the operator's log, whether agents can
    /// drive this copy and what disposable copies exist (which the operator manages from the Agent tab).
    /// </summary>
    internal static void LogAgentState(AppSettings settings) {
        Logger.Instance.Log4.Info(
            $"MCEC: agent commands {(settings.AgentCommandsEnabled ? "ENABLED" : "disabled")} " +
            $"(McpServerEnabled={settings.McpServerEnabled}, AllowSessionProvisioning={settings.AllowSessionProvisioning}).");

        System.Collections.Generic.IReadOnlyList<ProvisionedSessionInfo> sessions = SessionProvisioner.ListSessions();
        if (sessions.Count == 0) {
            Logger.Instance.Log4.Info($"MCEC: no provisioned instances under {SessionProvisioner.SessionsRoot}.");
            return;
        }
        Logger.Instance.Log4.Info($"MCEC: {sessions.Count} provisioned instance(s) under {SessionProvisioner.SessionsRoot}:");
        foreach (ProvisionedSessionInfo s in sessions) {
            Logger.Instance.Log4.Info(
                $"  - {s.SessionId}  age={SessionDisplayFormat.Age(DateTime.UtcNow - s.CreatedUtc)}" +
                $"  size={SessionDisplayFormat.Size(s.SizeBytes)}  {(s.IsRunning ? "running" : "stale")}");
        }
    }

    /// <summary>
    ///     <c>mcp</c> as the first token (the Terminal.Gui.Cli-style spelling) or <c>--mcp</c> anywhere
    ///     (the v3.0 spelling existing MCP client configs use).
    /// </summary>
    private static bool IsMcpInvocation(string[] args) =>
        string.Equals(args[0], "mcp", StringComparison.OrdinalIgnoreCase) ||
        Array.Exists(args, a => string.Equals(a, "--mcp", StringComparison.OrdinalIgnoreCase));

    private static void RunGui() {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // #209: the ONLY place the MainWindow Form is constructed. MainWindow.Instance is explicitly
        // assigned here (no more lazy construct-on-touch); anything that touches it earlier; or ever,
        // in headless --mcp mode; gets a pointed exception instead of a silent Form on the wrong thread.
        MainWindow mainWindow = new();
        MainWindow.Instance = mainWindow;
        Application.Run(mainWindow);

        Logger.Instance.Log4.Debug($"------ END runtime: {TelemetryService.Instance.RunTime?.Elapsed:g} ------");
    }

    /// <summary>
    ///     The Terminal.Gui.Cli surface: <c>--help</c>, <c>--version</c>, <c>--opencli</c>
    ///     (machine-readable command metadata), and <c>agent-guide</c> (the embedded
    ///     AgentInstructions.md, the same guidance the MCP server hands connecting agents). The
    ///     <c>mcp</c> command is registered for metadata so help and OpenCLI output describe it, but
    ///     its dispatch is intercepted in <see cref="Main" />.
    /// </summary>
    private static int RunCli(string[] args) {
        // Viewer commands (help, agent-guide) default to an INTERACTIVE Terminal.Gui session in
        // Terminal.Gui.Cli. That can never work here: mcec.exe is a WinExe sharing an attached
        // console with a live shell, and the shell resumed reading input the moment the process
        // launched (shells do not wait for GUI-subsystem exes), so a TUI would fight the shell
        // for the same input queue while painting full-screen frames over its prompt. Force the
        // headless --cat rendering instead; that output flows like normal console text.
        if (args.Length > 0 &&
            Array.Exists(_viewerAliases, v => string.Equals(v, args[0], StringComparison.OrdinalIgnoreCase)) &&
            !Array.Exists(args, a => string.Equals(a, "--cat", StringComparison.OrdinalIgnoreCase))) {
            args = [.. args, "--cat"];
        }

        CliHost host = new(options => {
            options.ApplicationName = "mcec";
            options.Version = Application.ProductVersion;
            options.AgentGuide = "MCEControl.AgentInstructions.md";
            options.AgentGuideIsResource = true;
            options.ResourceAssembly = typeof(Program).Assembly;
        });
        host.Registry.Register(new McpCommandMetadata());
        return host.RunAsync(args).GetAwaiter().GetResult();
    }

    // The registered viewer commands, kept in sync with RunCli's registrations: any viewer added
    // later needs the same forced-non-interactive treatment.
    private static readonly string[] _viewerAliases = ["help", "agent-guide"];

    /// <summary>
    ///     Headless bootstrap for <c>mcp</c>/<c>--mcp</c>: loads settings and the command core through
    ///     the UI-agnostic <see cref="AgentRuntime" /> seam (no <c>MainWindow</c>), starts the operator
    ///     safety surface (<see cref="HeadlessOperatorUi" />: e-stop hotkey + overlay on their own STA
    ///     pump thread), then serves MCP over stdio. Returns the process exit code.
    /// </summary>
    private static int RunHeadlessMcp() {
        // SECURITY: the installed copy never serves agents (see IsProgramFilesInstall). stderr so a
        // terminal user and an MCP client's error log both see WHY the server exited immediately.
        if (IsProgramFilesInstall) {
            Logger.Instance.Log4.Error($"MCEC: --mcp refused from the installed location. {InstalledAgentServingGuidance}");
            Console.Error.WriteLine("mcec: --mcp refused: running from the installed (Program Files) location.");
            Console.Error.WriteLine(InstalledAgentServingGuidance);
            return ExitCodes.UsageError;
        }

        // mcp serves JSON-RPC over stdio and is meant to be SPAWNED by an MCP client with
        // stdin/stdout redirected to pipes. Launched from an interactive terminal it can never
        // work: the shell does not wait for a WinExe, so the server blocks reading the SHARED
        // console, fights the shell's line editor for every keystroke, and Ctrl+C never reaches
        // it (the line editor consumes it before it becomes a control event). Refuse with
        // guidance instead of wedging. (Piped/file stdin, and a missing stdin handle; which reads
        // as instant EOF; both count as redirected and serve normally.)
        if (!Console.IsInputRedirected) {
            const string guidance =
                "mcec: mcp refused: stdin is an interactive console. This mode speaks JSON-RPC over " +
                "stdio and is meant to be spawned by an MCP client (which redirects stdin/stdout to " +
                "pipes); see the mcpServers example in the Agent Server docs. Run from a terminal it " +
                "would block on the shared console and Ctrl+C could not stop it. To poke at the " +
                "protocol by hand, pipe requests in: echo '{...}' | mcec mcp. A running server stops " +
                "when its client closes stdin (EOF) or sends 'send_command mcec:exit'.";
            Logger.Instance.Log4.Error(guidance);
            Console.Error.WriteLine(guidance);
            return ExitCodes.UsageError;
        }

        // Headless: the engine's load/save paths never show a dialog (nothing may block protocol
        // startup; stdout is the JSON-RPC stream). The one deliberate UI exception is the operator
        // safety surface below, which lives on its own pump thread and blocks nothing.
        AgentRuntime.Headless = true;

        // Match the GUI's DPI awareness so PrintWindow/GetWindowRect capture geometry is consistent
        // whether MCEC runs headless (--mcp) or interactively; visual styles and the text-rendering
        // default because this process CAN now create windows (overlay, re-arm prompt) and the latter
        // must be set before any handle exists.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        TelemetryService.Instance.Start("MCE Controller");

        // #216: SettingsStore.Load never shows UI; failures are already logged by the store
        // (headless: log instead of dialogs; same observable behavior as before the split).
        SettingsLoadResult settingsLoad = SettingsStore.Load($@"{ConfigPath}{SettingsStore.SettingsFileName}");
        AppSettings settings = settingsLoad.Settings;

        // TELEMETRY:
        // what: Settings
        // why: To understand what settings get changed and which dont
        // how is PII protected: only settings clearly identified as not containing PII are collected
        TelemetryService.Instance.TrackEvent("Settings", settings.GetTelemetryDictionary());

        AgentRuntime.Settings = settings;
        AgentRuntime.Invoker = CommandInvoker.Create(
            $@"{ConfigPath}mcec.commands", Application.ProductVersion, settings.DisableInternalCommands);
        // #209: the headless host capabilities; SendLine is a logged no-op (no legacy transports),
        // RequestShutdown is a clean deferred process exit (so mcec:exit over MCP works headless),
        // and MessageWindowHandle throws (the activity monitor never runs headless).
        AgentRuntime.Host = new HeadlessAppHost();

        // #135 follow-up: the operator safety surface. Arms the emergency-stop hotkey and shows the
        // command overlay on a dedicated STA pump thread (both are message-loop-bound), so a headless
        // agent session gets the same panic hotkey and narration the GUI host provides. Waits
        // (bounded) for arming, so by the time the protocol serves, the hotkey is live.
        HeadlessOperatorUi.Start(settings);

        Logger.Instance.Log4.Info("MCEC: headless MCP mode.");
        LogAgentState(settings);

        using Stream stdin = Console.OpenStandardInput();
        using Stream stdout = Console.OpenStandardOutput();
        AgentServer.RunStdio(stdin, stdout);

        // Stop the safety surface first: disarm the hotkey and dismiss any open re-arm prompt before
        // the invoker drops its queue (the prompt can no longer re-arm a dispatcher that is exiting).
        HeadlessOperatorUi.Stop();

        // #195: stop the command dispatcher thread before exiting. It starts lazily on the first
        // enqueue (e.g. a send_command) and is a background thread, so it could never keep the
        // process alive; this is a deliberate, clean stop that drops anything still queued (a
        // drop that severs a command tree releases held input) and briefly joins so an in-flight
        // command usually finishes before the process ends.
        AgentRuntime.Invoker.Shutdown(joinTimeoutMs: 2000);

        // #215: stop the dedicated UIA worker and dispose its cached UIA3Automation (bounded join).
        UiaService.Shutdown();

        return ExitCodes.Ok;
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        Logger.DumpException(e.Exception);
        TelemetryService.Instance.TrackException(e.Exception);
        MessageBox.Show(@$"Unhandled Exception: {e.Exception.Message}\n\n" +
                        @$"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec",
            Application.ProductName);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
        Exception ex = e.ExceptionObject as Exception ?? new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}");
        Logger.DumpException(ex);
        TelemetryService.Instance.TrackException(ex);
        MessageBox.Show(@$"Unhandled Exception: {ex.Message}\n\n" +
                        @$"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec",
            Application.ProductName);
    }
}
