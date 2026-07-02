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
    /// SECURITY (issue #155): a registry problem (deny-read ACE, key marked for deletion) must never
    /// crash startup — such failures return <paramref name="defaultValue"/> and are logged.
    /// </summary>
    public static object? GetRegistryValue(string valueName, object? defaultValue) {
        return GetRegistryValue(valueName, defaultValue, Registry.GetValue);
    }

    /// <summary>
    /// Seam for <see cref="GetRegistryValue(string, object?)"/>: <paramref name="getValue"/> stands in
    /// for <see cref="Registry.GetValue(string, string?, object?)"/> so the failure path is testable
    /// without touching the real registry.
    /// </summary>
    internal static object? GetRegistryValue(string valueName, object? defaultValue, Func<string, string?, object?, object?> getValue) {
        try {
            object? val = getValue(RegistryKeyPath, valueName, null);
            if (val == null) {
                val = getValue(LegacyRegistryKeyPath, valueName, defaultValue);
            }
            return val;
        }
        catch (Exception e) when (e is System.Security.SecurityException || e is IOException || e is UnauthorizedAccessException) {
            Logger.Instance.Log4.Error(
                $"Settings: Could not read registry value '{valueName}'; using default ({defaultValue ?? "null"}). {e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts a raw registry value to a bool, tolerating junk (issue #155). A REG_SZ like "banana"
    /// makes <see cref="Convert.ToBoolean(object?, IFormatProvider)"/> throw FormatException (and a
    /// REG_BINARY blob throws InvalidCastException) — a machine-policy value that cannot be parsed
    /// must not crash startup. Falls back to <paramref name="defaultValue"/> and logs the bad value
    /// so an admin can fix it.
    /// </summary>
    internal static bool RegistryValueToBoolean(object? value, bool defaultValue) {
        if (value is null) {
            return defaultValue;
        }
        try {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException || e is InvalidCastException) {
            Logger.Instance.Log4.Error(
                $"Settings: Ignoring invalid registry value '{value}' (expected a boolean); using default ({defaultValue}). {e.Message}");
            return defaultValue;
        }
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

    // --- Emergency stop (issue #135) ---
    // SAFETY: a global "dead man's switch" hotkey the operator can hit from ANY window to instantly halt
    // an agent session. On by default whenever the agent front door is used; reacts to physical input only
    // (the agent can never trip or defeat it). The default chord is one no app uses and the agent never
    // synthesizes. See EmergencyStopHotkey for the accepted spec format.
    [SafeForTelemetryAttribute]
    public bool EmergencyStopEnabled { get; set; } = true;

    // TELEMETRY: a rebound hotkey is a benign UI preference, but keep it out of telemetry for simplicity.
    public string EmergencyStopHotkey { get; set; } = MCEControl.EmergencyStopHotkey.DefaultSpec;

    // --- Isolated session provisioning (issue #138) ---
    // SECURITY: an agent asks MCEC to hand it a fresh, disposable instance dir (agent commands enabled only
    // inside the copy) instead of mutating the installed config. Provisioning is the ONE thing that cannot
    // be self-served — it must be an explicit operator opt-in or the isolation is theater. Off by default.
    [SafeForTelemetryAttribute]
    public bool AllowSessionProvisioning { get; set; } = false;

    // TELEMETRY: A bind address is PII-adjacent, so it is not collected.
    public string McpBindAddress { get; set; } = "127.0.0.1";
    [SafeForTelemetryAttribute]
    public int McpHttpPort { get; set; } = 5151;

    // --- On-screen command overlay (issue #119) ---
    // ON by default: the overlay shows each command as it executes so anyone watching can see that MCEC
    // is driving the machine (auditability), which also makes demos self-documenting. A settings file
    // without this element deserializes to the initialized default (true).
    [SafeForTelemetryAttribute]
    public bool CommandOverlayEnabled { get; set; } = true;

    // Which side of the primary screen the overlay docks to. Default Right.
    [SafeForTelemetryAttribute]
    public OverlayPosition CommandOverlayPosition { get; set; } = OverlayPosition.Right;

    // --- GIF recording limits (issue #80) ---
    // SECURITY/SAFETY: the agent `record` command is bounded by these so it cannot accidentally create
    // an unbounded file. Requests above a limit are CLAMPED (not failed) and the clamp is audited.
    [SafeForTelemetryAttribute]
    public int AgentRecordMaxFps { get; set; } = 30;
    [SafeForTelemetryAttribute]
    public int AgentRecordMaxDurationMs { get; set; } = 60000;
    [SafeForTelemetryAttribute]
    public int AgentRecordMaxFrames { get; set; } = 600;
    [SafeForTelemetryAttribute]
    public int AgentRecordMaxWidth { get; set; } = 1280;

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
    /// DeSerializes settings from XML.
    /// SECURITY (issue #155): a bad settings file must never put MCEC into a fail-to-start state
    /// (GUI or headless --mcp). Missing file → defaults are created; corrupt/unreadable file →
    /// defaults are used for this run and the file is left untouched so the user can recover it.
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
            Logger.Instance.Log4.Info("Settings: Loaded settings from " + settingsFile);
        }
        catch (FileNotFoundException) {
            // First time through, so create file with defaults
            Logger.Instance.Log4.Info($"Settings: Creating settings file with defaults: {settingsFile}");
            settings = new AppSettings();
            settings.Serialize(settingsFile);
        }
        catch (UnauthorizedAccessException e) {
            // File exists but cannot be read (ACLs). Use defaults for this run; do NOT try to
            // overwrite a file we cannot even read.
            Logger.Instance.Log4.Error(
                $"Settings: Settings file could not be loaded ({settingsFile}); using default settings for this run. {e.Message}");
            settings = new AppSettings();
            if (!AgentRuntime.Headless) {
                MessageBox.Show($"Settings file could not be loaded. {e.Message}\n\nMCE Controller will use default settings for this run.");
            }
        }
        catch (Exception e) when (e is XmlException || e is InvalidOperationException) {
            // Malformed XML (mid-write crash, disk error, hand-edit). XmlSerializer wraps the
            // XmlException in an InvalidOperationException. Use defaults for this run and
            // deliberately do NOT overwrite the corrupt file, so the user can inspect/repair it.
            string detail = (e.InnerException ?? e).Message;
            Logger.Instance.Log4.Error(
                $"Settings: Settings file is corrupt or invalid ({settingsFile}); using default settings for this run. " +
                $"The file was NOT overwritten - fix or delete it to recover. {detail}");
            settings = new AppSettings();
            if (!AgentRuntime.Headless) {
                MessageBox.Show($"Settings file is corrupt or invalid: {settingsFile}\n\n{detail}\n\n" +
                    "MCE Controller will use default settings for this run. The file was not overwritten - fix or delete it to recover your settings.");
            }
        }
        catch (Exception e) {
            // Last resort: a settings problem must never prevent startup.
            Logger.Instance.Log4.Error(
                $"Settings: Unexpected error loading settings file ({settingsFile}); using default settings for this run. {e.Message}");
            settings = new AppSettings();
            if (!AgentRuntime.Headless) {
                MessageBox.Show($"Settings file could not be loaded. {e.Message}\n\nMCE Controller will use default settings for this run.");
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

        // XmlSerializer.Deserialize contractually returns non-null here, but guard anyway (#155):
        // this method must always hand back usable settings.
        settings ??= new AppSettings();

        // Machine policy: read the registry override regardless of how (or whether) the file
        // loaded. RegistryValueToBoolean tolerates junk values (see #155).
        settings.DisableInternalCommands = RegistryValueToBoolean(
            GetRegistryValue("DisableInternalCommands", false), false);

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
