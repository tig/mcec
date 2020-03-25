using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MCEControl.Services;

namespace MCEControl {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;


            // Start logging
            Logger.Instance.LogFile = $@"{ConfigPath}MCEControl.log";
            Logger.Instance.Log4.Debug("Main");

            TelemetryService.Instance.Start("MCE Controller");
            UpdateService.Instance.GotLatestVersion += Instance_GotLatestVersion;

            // TODO: Update to check for 4.7 or newer
            if (!IsNet45OrNewer()) {
                MessageBox.Show(global::MCEControl.Properties.Resources.Error_RequiresDotNetVersion);
                return;
            }

            // Load AppSettings
            MainWindow.Instance.Settings = AppSettings.Deserialize($@"{ConfigPath}{AppSettings.SettingsFileName}");
            Application.Run(MainWindow.Instance);
        }

        internal static bool IsNet45OrNewer() {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) {
            Logger.DumpException(e.Exception);
            TelemetryService.Instance.TrackException(e.Exception);
            MessageBox.Show($"Unhandled Exception: {e.Exception.Message}\n\nSee log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec", Application.ProductName);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            var ex = e.ExceptionObject as Exception;
            Logger.DumpException(ex);
            TelemetryService.Instance.TrackException(ex);
            MessageBox.Show($"Unhandled Exception: {ex.Message}\n\nSee log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec", Application.ProductName);
        }
                    
        internal static void CheckVersion() {
            Logger.Instance.Log4.Info($"MCE Controller Version: {Application.ProductVersion}");
            UpdateService.Instance.GetLatestStableVersionAsync().ConfigureAwait(false);
        }

        private static void Instance_GotLatestVersion(object sender, Version version) {
            if (version == null && !String.IsNullOrWhiteSpace(UpdateService.Instance.ErrorMessage)) {
                Logger.Instance.Log4.Info(
                    $"Could not access tig.github.io/mcec to see if a newer version is available. {UpdateService.Instance.ErrorMessage}");
            }
            else if (UpdateService.Instance.CompareVersions() < 0) {
                Logger.Instance.Log4.Info(
                    $"A newer version of MCE Controller ({version}) is available at {UpdateService.Instance.DownloadUri}");
            }
            else if (UpdateService.Instance.CompareVersions() > 0) {
                Logger.Instance.Log4.Info(
                    $"You are are running a MORE recent version than can be found at tig.github.io/mcec ({version}).");
            }
            else {
                Logger.Instance.Log4.Info("You are running the most recent version of MCE Controller.");
            }
        }

        internal static string ConfigPath {
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
    }
}
