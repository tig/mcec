// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCEControl {
    internal static class Program {
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
            Logger.Instance.Log4.Debug($"------ START: v{Application.ProductVersion} - OS: {Environment.OSVersion.ToString()} on {(Environment.Is64BitProcess ? "x64" : "x86")} - .NET: {Environment.Version.ToString()} ------");

            Application.Run(MainWindow.Instance);

            Logger.Instance.Log4.Debug($"------ END runtime: {TelemetryService.Instance.RunTime.Elapsed:g} ------");
        }
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) {
            Logger.DumpException(e.Exception);
            TelemetryService.Instance.TrackException(e.Exception);
            MessageBox.Show($"Unhandled Exception: {e.Exception.Message}\n\n" +
                $"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec", Application.ProductName);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            var ex = e.ExceptionObject as Exception;
            Logger.DumpException(ex);
            TelemetryService.Instance.TrackException(ex);
            MessageBox.Show($"Unhandled Exception: {ex.Message}\n\n" +
                $"See log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec", Application.ProductName);
        }

        internal static string ConfigPath {
            get {
                // Get dir of mcecontrol.exe
                var path = AppDomain.CurrentDomain.BaseDirectory;
                var programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

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
