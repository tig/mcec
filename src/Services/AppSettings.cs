// Copyright © Kindel, LLC - http://www.kindel.com
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

namespace MCEControl;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This is just settings info.")]
public class AppSettings : ICloneable {
    public const string SettingsFileName = "mcec.settings";

    // Registry key for per-machine settings (telemetry opt-in, disable-internal-commands override).
    // For MCEC 3.0 rebrand, new location under "Kindel"; legacy "Kindel Systems" is read as fallback for upgrades.
    public const string RegistryKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel\MCE Controller";
    public const string LegacyRegistryKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller";

    /// <summary>
    /// Read a registry value preferring the current (Kindel) key, falling back to legacy (Kindel Systems) key.
    /// </summary>
    public static object? GetRegistryValue(string valueName, object? defaultValue) {
        object? val = Registry.GetValue(RegistryKeyPath, valueName, null);
        if (val == null) {
            val = Registry.GetValue(LegacyRegistryKeyPath, valueName, defaultValue);
        }
        return val;
    }

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
    public string ClosingCommand { get; set; } = null!;
    [SafeForTelemetryAttribute]
    public int Opacity { get; set; } = 100;
    [SafeForTelemetryAttribute]
    public int ServerPort { get; set; } = 5150;

    // [SafeForTelemetryAttribute] 
    // TELEMETRY: WakeupCommand can be set by user and thus may contain PII, so it is not collected
    public string WakeupCommand { get; set; } = null!;
    [SafeForTelemetryAttribute]
    public bool WakeupEnabled { get; set; }
    [SafeForTelemetryAttribute]
    public string WakeupHost { get; set; } = null!;
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
    public bool UserPresenceDetection { get; set; }

    [SafeForTelemetryAttribute]
    public bool DisableUpdatePopup { get; set; }

    // TELEMETRY: NOT SAFE FOR PII - MUST DEFAULT TO FALSE
    public bool LogUserActivity { get; set; } = false;

    // --- MCEC 3.0 agent (Model Context Environment Controller) settings ---
    // SECURITY: The observation/targeting commands (capture/query/find/invoke) ship DISABLED by
    // default and require their OWN explicit opt-in, separate from the actuation enable. Enabling
    // "press keys" must not silently enable "screenshot my screen".
    [SafeForTelemetryAttribute]
    public bool AgentCommandsEnabled { get; set; } = false;

    // The MCP/HTTP façade is off by default and binds to localhost only unless deliberately changed.
    [SafeForTelemetryAttribute]
    public bool McpServerEnabled { get; set; } = false;

    // TELEMETRY: A bind address is PII-adjacent, so it is not collected.
    public string McpBindAddress { get; set; } = "127.0.0.1";
    [SafeForTelemetryAttribute]
    public int McpHttpPort { get; set; } = 5151;

    #region ICloneable Members

    public object Clone() {
        return MemberwiseClone();
    }

    #endregion

    // Must have a default public constructor so XMLSerialization will work
    // This class is NOT supposed to be creatable (use Deserialize to construct).
    public AppSettings() {
        SerialPort defaultPort = new SerialPort();
        SerialServerPortName = defaultPort.PortName;
        SerialServerBaudRate = defaultPort.BaudRate;
        SerialServerParity = defaultPort.Parity;
        SerialServerDataBits = defaultPort.DataBits;
        SerialServerStopBits = defaultPort.StopBits;
        SerialServerHandshake = defaultPort.Handshake;
        defaultPort.Dispose();
        UnlockDetection = true;
        InputDetection = true;
        UserPresenceDetection = true;
    }



    /// <summary>
    /// By default we want the settings file stored with the EXE
    /// This allows the app to be run with multiple instances with a settings
    /// file for each instance (each being in different directory).
    /// However, typical installs get put into to %PROGRAMFILES% which 
    /// is ACLd to allow only admin writes on Win7.         
    /// </summary>
    /// <param name="startupPath">Path to where exe was started from (aka Applciation.StartupPath)</param>
    /// <returns>Path to where Settings & Log Files should be</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
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
            XmlSerializer ser = new XmlSerializer(typeof(AppSettings));
            StreamWriter sw = new StreamWriter(settingsFile);
            ser.Serialize(sw, this);
            sw.Close();

            Logger.Instance.Log4.Info("Settings: Wrote settings to " + settingsFile);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Info($"Settings: Settings file could not be written. {settingsFile} {e.Message}");
            if (!AgentRuntime.Headless) {
                MessageBox.Show($"Settings file could not be written. {settingsFile} {e.Message}");
            }
        }
    }

    /// <summary>
    /// DeSerializes settings from XML
    /// </summary>
    /// <param name="settingsFile">full path to settings file</param>
    public static AppSettings Deserialize(String settingsFile) {
        AppSettings? settings = null;

        XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
        // A FileStream is needed to read the XML document.
        FileStream? fs = null;
        XmlReader? reader = null;
        try {
            fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
            reader = new XmlTextReader(fs);
            settings = (AppSettings?)serializer.Deserialize(reader);

            settings!.DisableInternalCommands = Convert.ToBoolean(
                GetRegistryValue("DisableInternalCommands", false), new NumberFormatInfo());
            Logger.Instance.Log4.Info("Settings: Loaded settings from " + settingsFile);
        }
        catch (FileNotFoundException) {
            // First time through, so create file with defaults
            Logger.Instance.Log4.Info($"Settings: Creating settings file with defaults: {settingsFile}");
            settings = new AppSettings();
            settings.Serialize(settingsFile);

            // even if it's first run, read global commands
            settings.DisableInternalCommands = Convert.ToBoolean(
                GetRegistryValue("DisableInternalCommands", false), new NumberFormatInfo());
        }
        catch (UnauthorizedAccessException e) {
            Logger.Instance.Log4.Error($"Settings: Settings file could not be loaded. {e.Message}");
            if (!AgentRuntime.Headless) {
                MessageBox.Show($"Settings file could not be loaded. {e.Message}");
            }
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
        TelemetryService.Instance.TrackEvent("Settings", settings!.GetTelemetryDictionary());

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
        Dictionary<string, string> dictionary = [];
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(this)) {
            if (property.Attributes.Contains(new SafeForTelemetryAttribute())) {
                object? value = property.GetValue(this);
                if (value != null) {
                    if (property.PropertyType.IsSubclassOf(typeof(AppSettings))) {
                        // Go deep
                        IDictionary<string, string> propDict = ((AppSettings)value).GetTelemetryDictionary();
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
