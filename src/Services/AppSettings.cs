// Copyright © Kindel Systems, LLC - http://www.kindel.com - charlie@kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace MCEControl {

    /// <summary>
    /// Used by TELEMETRY to determine which settings are safe for collection.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class SafeForTelemetryAttribute : System.Attribute {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This is just settings info.")]
    public class AppSettings : ICloneable {
        public const string SettingsFileName = "MCEControl.settings";

        // Global
        [XmlIgnore] public bool DisableInternalCommands;

        [SafeForTelemetryAttribute]
        public bool AutoStart { get; set; }
        [SafeForTelemetryAttribute]
        public bool HideOnStartup { get; set; }
        [SafeForTelemetryAttribute]
        public string TextBoxLogThreshold { get; set; } = "INFO";
        [SafeForTelemetryAttribute]
        public bool ActAsClient { get; set; }
        [SafeForTelemetryAttribute]
        public bool ActAsServer { get; set; } = true;
        [SafeForTelemetryAttribute]
        public int ClientDelayTime { get; set; } = 30000;
        [SafeForTelemetryAttribute]
        public int CommandPacing { get; set; } = 0;

        // [SafeForTelemetryAttribute] 
        // TELEMETRY: Client host may contain PII, so it is not collected
        public string ClientHost { get; set; } = "localhost";
        [SafeForTelemetryAttribute]
        public int ClientPort { get; set; } = 5150;
        [SafeForTelemetryAttribute]
        public string ClosingCommand { get; set; }
        [SafeForTelemetryAttribute]
        public int Opacity { get; set; } = 100;
        [SafeForTelemetryAttribute]
        public int ServerPort { get; set; } = 5150;

        // [SafeForTelemetryAttribute] 
        // TELEMETRY: WakeupCommand can be set by user and thus may contain PII, so it is not collected
        public string WakeupCommand { get; set; }
        [SafeForTelemetryAttribute]
        public bool WakeupEnabled { get; set; }
        [SafeForTelemetryAttribute]
        public string WakeupHost { get; set; }
        [SafeForTelemetryAttribute]
        public int WakeupPort { get; set; }
        [SafeForTelemetryAttribute]
        public bool ActAsSerialServer { get; set; } = false;
        [SafeForTelemetryAttribute]
        public string SerialServerPortName { get; set; }
        [SafeForTelemetryAttribute]
        public int SerialServerBaudRate { get; set; }
        [SafeForTelemetryAttribute]
        public Parity SerialServerParity { get; set; }
        [SafeForTelemetryAttribute]
        public int SerialServerDataBits { get; set; }
        [SafeForTelemetryAttribute]
        public StopBits SerialServerStopBits { get; set; }
        [SafeForTelemetryAttribute]
        public Handshake SerialServerHandshake { get; set; }
        [SafeForTelemetryAttribute]
        public Point WindowLocation { get; set; }
        [SafeForTelemetryAttribute]
        public Size WindowSize { get; set; }
        [SafeForTelemetryAttribute]
        public bool ShowCommandWindow { get; set; }
        [SafeForTelemetryAttribute]
        public bool ActivityMonitorEnabled { get; set; }

        // [SafeForTelemetryAttribute] 
        // TELEMETRY: Activity Montior command can be changed by user, and thus may contain PII, so it is not collected
        public string ActivityMonitorCommand { get; set; } = "activity";
        [SafeForTelemetryAttribute]
        public int ActivityMonitorDebounceTime { get; set; } = 10;
        [SafeForTelemetryAttribute]
        public bool UnlockDetection { get; set; }
        [SafeForTelemetryAttribute]
        public bool InputDetection { get; set; }

        [SafeForTelemetryAttribute]
        public bool DisableUpdatePopup { get; set; }



        #region ICloneable Members

        public object Clone() {
            return MemberwiseClone();
        }

        #endregion

        // Must have a default public constructor so XMLSerialization will work
        // This class is NOT supposed to be creatable (use Deserialize to construct).
        public AppSettings() {
            var defaultPort = new SerialPort();
            SerialServerPortName = defaultPort.PortName;
            SerialServerBaudRate = defaultPort.BaudRate;
            SerialServerParity = defaultPort.Parity;
            SerialServerDataBits = defaultPort.DataBits;
            SerialServerStopBits = defaultPort.StopBits;
            SerialServerHandshake = defaultPort.Handshake;
            defaultPort.Dispose();
            UnlockDetection = true;
            InputDetection = true;
        }


        /// <summary>
        /// By default we want the settings file stored with the EXE
        /// This allows the app to be run with multiple instances with a settings
        /// file for each instance (each being in different directory).
        /// However, typical installs get put into to %PROGRAMFILES% which 
        /// is ACLd to allow only admin writes on Win7.         /// </summary>
        /// <param name="startupPath">Path to where exe was started from (aka Applciation.StartupPath)</param>
        /// <returns>Path to where Settings & Log Files should be</returns>
        public static String GetSettingsPath(string startupPath) {
            if (string.IsNullOrWhiteSpace(startupPath)) {
                throw new ArgumentException("startupPath must be specified", nameof(startupPath));
            }
            // If app was started from within ProgramFiles then use UserAppDataPath.
            if (startupPath.Contains(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))) {
                // Strip off the trailing version ("\0.0.0.xxxx")
                startupPath = Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1));
            }

            return startupPath;
        }

        /// <summary>
        /// Serializes settings to XML
        /// </summary>
        /// <param name="settingsFile">full path to settings file</param>
        public void Serialize(string settingsFile) {
            try {
                var ser = new XmlSerializer(typeof(AppSettings));
                var sw = new StreamWriter(settingsFile);
                ser.Serialize(sw, this);
                sw.Close();

                Logger.Instance.Log4.Info("Settings: Wrote settings to " + settingsFile);
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"Settings: Settings file could not be written. {settingsFile} {e.Message}");
                MessageBox.Show($"Settings file could not be written. {settingsFile} {e.Message}");
            }
        }

        /// <summary>
        /// DeSerializes settings from XML
        /// </summary>
        /// <param name="settingsFile">full path to settings file</param>
        public static AppSettings Deserialize(String settingsFile) {
            AppSettings settings = null;

            var serializer = new XmlSerializer(typeof(AppSettings));
            // A FileStream is needed to read the XML document.
            FileStream fs = null;
            XmlReader reader = null;
            try {
                fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
                reader = new XmlTextReader(fs);
                settings = (AppSettings)serializer.Deserialize(reader);

                settings.DisableInternalCommands = Convert.ToBoolean(
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller",
                                    "DisableInternalCommands", false), new NumberFormatInfo());
                Logger.Instance.Log4.Info("Settings: Loaded settings from " + settingsFile);
            }
            catch (FileNotFoundException) {
                // First time through, so create file with defaults
                Logger.Instance.Log4.Info($"Settings: Creating settings file with defaults: {settingsFile}");
                settings = new AppSettings();
                settings.Serialize(settingsFile);

                // even if it's first run, read global commands
                settings.DisableInternalCommands = Convert.ToBoolean(
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller",
                                    "DisableInternalCommands", false), new NumberFormatInfo());
            }
            catch (UnauthorizedAccessException e) {
                Logger.Instance.Log4.Info($"Settings: Settings file could not be loaded. {e.Message}");
                MessageBox.Show($"Settings file could not be loaded. {e.Message}");
            }
            finally {
                if (reader != null) {
                    reader.Dispose();
                }

                if (fs != null) {
                    fs.Dispose();
                }
            }

            // TELEMETRY: 
            // what: Settings
            // why: To understand what settings get changed and which dont
            // how is PII protected: only settings clearly identified as not containing PII are collected
            TelemetryService.Instance.TrackEvent("Settings", settings.GetTelemetryDictionary());

            return settings;
        }

        /// <summary>
        /// Returns a dictionary of settings, filtered by those that can't contain PII
        /// TELEMETRY: 
        /// what: Settings
        /// why: To understand what settings get changed and which dont
        /// how is PII protected: only settings clearly identified as not containing PII are collected
        /// </summary>
        /// <returns></returns>
        public virtual IDictionary<string, string> GetTelemetryDictionary() {
            var dictionary = new Dictionary<string, string>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(this)) {
                if (property.Attributes.Contains(new SafeForTelemetryAttribute())) {
                    var value = property.GetValue(this);
                    if (value != null) {
                        if (property.PropertyType.IsSubclassOf(typeof(AppSettings))) {
                            // Go deep
                            var propDict = ((AppSettings)value).GetTelemetryDictionary();
                            dictionary.Add(property.Name, JsonSerializer.Serialize(propDict, propDict.GetType()));
                        }
                        else {
                            dictionary.Add(property.Name, JsonSerializer.Serialize(value, value.GetType()));
                        }
                    }
                }
            }
            return dictionary;
        }
    }
}
