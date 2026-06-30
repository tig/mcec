// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCEControl;

internal static class Program {
    internal static string ConfigPath {
        get {
            // Get dir of mcecontrol.exe
            string path = AppDomain.CurrentDomain.BaseDirectory;
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // If we're running from a (read-only) Program Files install location, redirect log/
            // settings/command files to %AppData%. Check both 64-bit ("Program Files") and 32-bit
            // ("Program Files (x86)") roots — the installer puts the self-contained x64 build under
            // 64-bit Program Files, while older installs used the x86 path.
            foreach (Environment.SpecialFolder folder in new[] {
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
            }) {
                string programFiles = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(programFiles) &&
                    path.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) {
                    path = $@"{appdata}\{path.Substring(programFiles.Length + 1)}";
                    break;
                }
            }

            return path;
        }
    }

    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args) {
        // Start logging
        Logger.Instance.LogFile = $@"{ConfigPath}mcec.log";
        Logger.Instance.Log4.Debug(
            $"------ START: v{Application.ProductVersion} - OS: {Environment.OSVersion} on {(Environment.Is64BitProcess ? "x64" : "x86")} - .NET: {Environment.Version.ToString()} ------");

        // v3.0: carry an existing user's MCEControl.settings/.commands forward to the new mcec.* names.
        ConfigMigration.Run(ConfigPath);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // MCEC 3.0: headless MCP server mode. An MCP client launches `mcec.exe --mcp` and speaks
        // JSON-RPC over stdio. No WinForms message loop runs; stdout is reserved for the protocol.
        if (Array.Exists(args, a => string.Equals(a, "--mcp", StringComparison.OrdinalIgnoreCase))) {
            RunHeadlessMcp();
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(MainWindow.Instance);

        Logger.Instance.Log4.Debug($"------ END runtime: {TelemetryService.Instance.RunTime!.Elapsed:g} ------");
    }

    /// <summary>
    /// Headless bootstrap for <c>--mcp</c>: loads settings and the command core through the
    /// UI-agnostic <see cref="AgentRuntime"/> seam (no <c>MainWindow</c>), then serves MCP over stdio.
    /// </summary>
    private static void RunHeadlessMcp() {
        // Headless: never show a modal dialog (no operator; stdout is the JSON-RPC stream).
        AgentRuntime.Headless = true;

        // Match the GUI's DPI awareness so PrintWindow/GetWindowRect capture geometry is consistent
        // whether MCEC runs headless (--mcp) or interactively.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        TelemetryService.Instance.Start("MCE Controller");

        AppSettings settings = AppSettings.Deserialize($@"{ConfigPath}{AppSettings.SettingsFileName}");
        AgentRuntime.Settings = settings;
        AgentRuntime.Invoker = CommandInvoker.Create(
            $@"{ConfigPath}mcec.commands", Application.ProductVersion, settings.DisableInternalCommands);

        Logger.Instance.Log4.Info($"MCEC: headless MCP mode (AgentCommandsEnabled={settings.AgentCommandsEnabled}).");

        using Stream stdin = Console.OpenStandardInput();
        using Stream stdout = Console.OpenStandardOutput();
        AgentServer.RunStdio(stdin, stdout);
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        Logger.DumpException(e.Exception);
        TelemetryService.Instance.TrackException(e.Exception);
        MessageBox.Show(@$"Unhandled Exception: {e.Exception.Message}\n\n" +
                       @$"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec",
            Application.ProductName);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
        Exception ex = (e.ExceptionObject as Exception)!;
        Logger.DumpException(ex);
        TelemetryService.Instance.TrackException(ex);
        MessageBox.Show(@$"Unhandled Exception: {ex.Message}\n\n" +
                        @$"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec",
            Application.ProductName);
    }
}
