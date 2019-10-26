using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCEControl {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start logging
            Logger.Instance.LogFile = $@"{ConfigPath}MCEControl.log";
            Logger.Instance.Log4.Debug("Main");

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

        internal static void CheckVersion() {
            Logger.Instance.Log4.Info($"MCE Controller Version: {Application.ProductVersion}");
            var lv = new LatestVersion() { Url = "https://tig.github.io/mcec/install_version.txt" };

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
