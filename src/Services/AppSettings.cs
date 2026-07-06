// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text.Json;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// The serialized application settings; a (mostly) pure POCO (#216): serialized properties,
/// defaults, and <see cref="Clone"/>. Persistence lives in <see cref="SettingsStore"/>
/// (load/save/path resolution) and registry policy in <see cref="MachinePolicy"/>; the host owns
/// dialogs and telemetry emission. The XML file format is unchanged.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This is just settings info.")]
public class AppSettings : ICloneable {
    // Machine policy (HKLM registry override; see MachinePolicy). Not serialized; populated by
    // SettingsStore.Load on every load, regardless of how (or whether) the file loaded.
    // Public field name is part of the AppSettings surface used across the codebase; not renamed.
    // ReSharper disable once InconsistentNaming
    [XmlIgnore] public bool DisableInternalCommands;

    [SafeForTelemetry]
    public bool AutoStart { get; set; }
    [SafeForTelemetry]
    public bool HideOnStartup { get; set; }
    [SafeForTelemetry]
    public string TextBoxLogThreshold { get; set; } = "INFO";
    [SafeForTelemetry]
    public bool ActAsClient { get; set; }
    [SafeForTelemetry]
    public bool ActAsServer { get; set; } = true;
    [SafeForTelemetry]
    public int ClientDelayTime { get; set; } = 30000;
    [SafeForTelemetry]
    public int CommandPacing { get; set; }

    // [SafeForTelemetry]
    // TELEMETRY: Client host may contain PII, so it is not collected
    public string ClientHost { get; set; } = "localhost";
    [SafeForTelemetry]
    public int ClientPort { get; set; } = 5150;
    [SafeForTelemetry]
    public string ClosingCommand { get; set; } = null!;
    [SafeForTelemetry]
    public int Opacity { get; set; } = 100;
    [SafeForTelemetry]
    public int ServerPort { get; set; } = 5150;

    // SECURITY (issue #149): which interface the TCP/IP command server binds to. The command server
    // turns received strings into keyboard/mouse/process actions with NO socket authentication (by
    // design, trusted-network model), so the bind interface is a security control. Accepted values
    // (case-insensitive): "0.0.0.0"/"any"/"*" (all interfaces), "127.0.0.1"/"localhost"/"loopback"
    // (single machine only), "::1", or a specific local IP. Junk is rejected loudly and falls back to
    // loopback (see SocketServer.ResolveBindAddress).
    // DEFAULT is "0.0.0.0" (all interfaces) to preserve the long-standing behavior on upgrade; many
    // existing installs are driven from another host on a trusted LAN. Single-machine operators should
    // set this to "127.0.0.1" to keep the unauthenticated command port off the network.
    // TELEMETRY: A bind address is PII-adjacent, so it is not collected (mirrors McpBindAddress).
    public string SocketServerBindAddress { get; set; } = "0.0.0.0";

    // [SafeForTelemetry]
    // TELEMETRY: WakeupCommand can be set by user and thus may contain PII, so it is not collected
    public string WakeupCommand { get; set; } = null!;
    [SafeForTelemetry]
    public bool WakeupEnabled { get; set; }
    [SafeForTelemetry]
    public string WakeupHost { get; set; } = null!;
    [SafeForTelemetry]
    public int WakeupPort { get; set; }
    [SafeForTelemetry]
    public bool ActAsSerialServer { get; set; }
    [SafeForTelemetry]
    public string SerialServerPortName { get; set; }
    [SafeForTelemetry]
    public int SerialServerBaudRate { get; set; }
    [SafeForTelemetry]
    public Parity SerialServerParity { get; set; }
    [SafeForTelemetry]
    public int SerialServerDataBits { get; set; }
    [SafeForTelemetry]
    public StopBits SerialServerStopBits { get; set; }
    [SafeForTelemetry]
    public Handshake SerialServerHandshake { get; set; }
    [SafeForTelemetry]
    public Point WindowLocation { get; set; }
    [SafeForTelemetry]
    public Size WindowSize { get; set; }
    [SafeForTelemetry]
    public bool ShowCommandWindow { get; set; }
    [SafeForTelemetry]
    public bool ActivityMonitorEnabled { get; set; }

    // [SafeForTelemetry]
    // TELEMETRY: Activity Montior command can be changed by user, and thus may contain PII, so it is not collected
    public string ActivityMonitorCommand { get; set; } = "activity";
    [SafeForTelemetry]
    public int ActivityMonitorDebounceTime { get; set; } = 10;
    [SafeForTelemetry]
    public bool UnlockDetection { get; set; }
    [SafeForTelemetry]
    public bool InputDetection { get; set; }
    [SafeForTelemetry]
    public bool UserPresenceDetection { get; set; }

    [SafeForTelemetry]
    public bool DisableUpdatePopup { get; set; }

    // TELEMETRY: NOT SAFE FOR PII - MUST DEFAULT TO FALSE
    public bool LogUserActivity { get; set; }

    // --- MCEC 3.0 agent (Model Context Environment Controller) settings ---
    // SECURITY: The observation/targeting commands (capture/query/find/invoke) ship DISABLED by
    // default and require their OWN explicit opt-in, separate from the actuation enable. Enabling
    // "press keys" must not silently enable "screenshot my screen".
    [SafeForTelemetry]
    public bool AgentCommandsEnabled { get; set; }

    // The MCP/HTTP façade is off by default and binds to localhost only unless deliberately changed.
    [SafeForTelemetry]
    public bool McpServerEnabled { get; set; }

    // --- Emergency stop (issue #135) ---
    // SAFETY: a global "dead man's switch" hotkey the operator can hit from ANY window to instantly halt
    // an agent session. On by default whenever the agent front door is used; reacts to physical input only
    // (the agent can never trip or defeat it). The default chord is one no app uses and the agent never
    // synthesizes. See EmergencyStopHotkey for the accepted spec format.
    [SafeForTelemetry]
    public bool EmergencyStopEnabled { get; set; } = true;

    // TELEMETRY: a rebound hotkey is a benign UI preference, but keep it out of telemetry for simplicity.
    public string EmergencyStopHotkey { get; set; } = MCEControl.EmergencyStopHotkey.DefaultSpec;

    // --- Isolated session provisioning (issue #138) ---
    // SECURITY: an agent asks MCEC to hand it a fresh, disposable instance dir (agent commands enabled only
    // inside the copy) instead of mutating the installed config. Provisioning is the ONE thing that cannot
    // be self-served; it must be an explicit operator opt-in or the isolation is theater. Off by default.
    [SafeForTelemetry]
    public bool AllowSessionProvisioning { get; set; }

    // TELEMETRY: A bind address is PII-adjacent, so it is not collected.
    public string McpBindAddress { get; set; } = "127.0.0.1";
    [SafeForTelemetry]
    public int McpHttpPort { get; set; } = 5151;

    // SECURITY (#143): defense-in-depth bearer token for the HTTP front door. The HTTP handler ALWAYS
    // validates the Host header (must be a loopback authority; defeats DNS rebinding) and the Origin
    // header (must be absent or loopback; defeats drive-by browser CSRF). Those two need no
    // configuration. Setting a non-empty token additionally requires every HTTP request to carry
    // `Authorization: Bearer <token>`, which also protects against a same-machine hostile process.
    // Empty (default) = rely on Host/Origin only. A token is NOT PII, but keep it out of telemetry so
    // a shared secret is never transmitted.
    public string McpAuthToken { get; set; } = "";

    // --- On-screen command overlay (issue #119) ---
    // ON by default: the overlay shows each command as it executes so anyone watching can see that MCEC
    // is driving the machine (auditability), which also makes demos self-documenting. A settings file
    // without this element deserializes to the initialized default (true).
    [SafeForTelemetry]
    public bool CommandOverlayEnabled { get; set; } = true;

    // Which side of the primary screen the overlay docks to. Default Right.
    [SafeForTelemetry]
    public OverlayPosition CommandOverlayPosition { get; set; } = OverlayPosition.Right;

    // --- GIF recording limits (issue #80) ---
    // SECURITY/SAFETY: the agent `record` command is bounded by these so it cannot accidentally create
    // an unbounded file. Requests above a limit are CLAMPED (not failed) and the clamp is audited.
    [SafeForTelemetry]
    public int AgentRecordMaxFps { get; set; } = 30;
    [SafeForTelemetry]
    public int AgentRecordMaxDurationMs { get; set; } = 60000;
    [SafeForTelemetry]
    public int AgentRecordMaxFrames { get; set; } = 600;
    [SafeForTelemetry]
    public int AgentRecordMaxWidth { get; set; } = 1280;

    #region ICloneable Members

    public object Clone() {
        return MemberwiseClone();
    }

    #endregion

    // Must have a default public constructor so XMLSerialization will work
    // This class is NOT supposed to be creatable (use SettingsStore.Load to construct).
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
