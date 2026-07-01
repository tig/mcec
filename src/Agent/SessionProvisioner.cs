// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace MCEControl;

/// <summary>
/// Provisions a fresh, disposable, isolated MCEC instance (#138). Instead of an agent mutating the
/// operator's <b>installed</b> config — flipping <c>AgentCommandsEnabled</c> / per-command <c>Enabled</c>
/// and hoping best-effort cleanup runs — MCEC hands the agent a throwaway directory containing
/// <c>mcec.exe</c> + dependencies and a <b>co-located</b>, agent-ready config. All enabled state lives only
/// inside that copy, so a crashed or abandoned session leaves the real install untouched and "cleanup" is
/// just deleting the directory. Concurrent sessions get separate directories and never fight over one file.
///
/// <para>SECURITY: provisioning itself is gated behind <see cref="AppSettings.AllowSessionProvisioning"/> —
/// the one thing that cannot be self-served, or the isolation is theater. The co-located config disables
/// <c>ActAsServer</c> (no firewall prompt) and <c>AllowSessionProvisioning</c> (a provisioned session can't
/// re-provision), and binds the MCP server to localhost. The installed <c>mcec.settings</c> /
/// <c>mcec.commands</c> are never read or written.</para>
/// </summary>
public static class SessionProvisioner {
    /// <summary>The agent observation/action commands enabled in a provisioned session's co-located command table.</summary>
    public static readonly string[] DefaultCommands =
        ["capture", "query", "displays", "find", "wait-for", "invoke", "drag", "click", "record"];

    private static string? _sessionsRoot;

    /// <summary>
    /// Root under which each session gets its own directory. Defaults to
    /// <c>%LOCALAPPDATA%\MCEC\sessions</c> (temp fallback if that can't be resolved). Settable by tests.
    /// </summary>
    public static string SessionsRoot {
        get => _sessionsRoot ??= DefaultSessionsRoot();
        set => _sessionsRoot = value;
    }

    /// <summary>The directory the running mcec.exe (and its dependencies) live in — the copy source.</summary>
    public static string BinariesDir { get; set; } = AppContext.BaseDirectory;

    private static string DefaultSessionsRoot() {
        try {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local)) {
                return Path.Combine(local, "MCEC", "sessions");
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"SessionProvisioner: falling back to temp sessions root: {e.Message}");
        }
        return Path.Combine(Path.GetTempPath(), "MCEC", "sessions");
    }

    /// <summary>
    /// Creates a fresh session directory, copies the binaries in, writes an agent-ready co-located config,
    /// and returns the handoff. Throws on failure after removing any partial directory.
    /// </summary>
    /// <param name="mcpServerEnabled">Enable the session's MCP/HTTP transport (bound to localhost).</param>
    /// <param name="commands">Command names to enable in the session (defaults to <see cref="DefaultCommands"/>).</param>
    public static ProvisionedSession Provision(bool mcpServerEnabled = true, IEnumerable<string>? commands = null) {
        string sessionId = Guid.NewGuid().ToString("N")[..12];
        string dir = Path.Combine(SessionsRoot, sessionId);
        string version = System.Windows.Forms.Application.ProductVersion;
        int port = mcpServerEnabled ? FindFreeLoopbackPort() : 0;
        string[] enable = [.. (commands ?? DefaultCommands)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        try {
            Directory.CreateDirectory(dir);
            CopyBinaries(BinariesDir, dir);
            WriteSessionSettings(dir, mcpServerEnabled, port);
            WriteSessionCommands(dir, enable, version);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"SessionProvisioner: provisioning failed, cleaning up '{dir}': {e.Message}");
            TryDeleteDirectory(dir);
            throw new InvalidOperationException($"Failed to provision session '{sessionId}': {e.Message}", e);
        }

        string exePath = Path.Combine(dir, "mcec.exe");
        AgentRuntime.Audit("provision-session",
            $"provisioned {sessionId} at {dir} (mcp={mcpServerEnabled}{(mcpServerEnabled ? $" port={port}" : "")}, commands=[{string.Join(",", enable)}])");

        return new ProvisionedSession {
            SessionId = sessionId,
            Directory = dir,
            ExePath = exePath,
            McpServerEnabled = mcpServerEnabled,
            BindAddress = "127.0.0.1",
            Port = port,
            Token = Guid.NewGuid().ToString("N"),
        };
    }

    /// <summary>
    /// Tears down a provisioned session by deleting its directory. Returns true when the directory was
    /// removed (or was already gone). A directory whose files are still locked (the session is running)
    /// returns false — stop the session first.
    /// </summary>
    public static bool Teardown(string sessionId) {
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return false;
        }
        // Guard against path traversal: a session id is a bare token, never a path.
        string safeId = Path.GetFileName(sessionId.Trim());
        string dir = Path.Combine(SessionsRoot, safeId);
        if (!Directory.Exists(dir)) {
            AgentRuntime.Audit("end-session", $"{safeId} — directory already gone");
            return true;
        }
        bool ok = TryDeleteDirectory(dir);
        AgentRuntime.Audit("end-session", ok ? $"{safeId} — torn down ({dir})" : $"{safeId} — could not delete (in use?) {dir}");
        return ok;
    }

    /// <summary>
    /// Belt-and-suspenders cleanup: deletes session directories older than <paramref name="maxAge"/> so a
    /// leaked/abandoned session never lingers. A running session's files are locked and are skipped (they'll
    /// be reaped on a later launch once the process exits). Best-effort — never throws. Returns the count
    /// reaped.
    /// </summary>
    public static int ReapOrphans(TimeSpan maxAge) {
        int reaped = 0;
        try {
            if (!Directory.Exists(SessionsRoot)) {
                return 0;
            }
            DateTime cutoff = DateTime.UtcNow - maxAge;
            foreach (string dir in Directory.GetDirectories(SessionsRoot)) {
                DateTime created;
                try {
                    created = Directory.GetCreationTimeUtc(dir);
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Warn($"SessionProvisioner: could not stat '{dir}': {e.Message}");
                    continue;
                }
                if (created > cutoff) {
                    continue; // too new — could be an active or just-provisioned session
                }
                if (TryDeleteDirectory(dir)) {
                    reaped++;
                    Logger.Instance.Log4.Info($"SessionProvisioner: reaped stale session dir {dir}");
                }
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"SessionProvisioner: reaping failed: {e.Message}");
        }
        if (reaped > 0) {
            AgentRuntime.Audit("provision-session", $"reaped {reaped} stale session dir(s) under {SessionsRoot}");
        }
        return reaped;
    }

    /// <summary>Recursively copies the binaries, skipping any mutable config/log the installed instance left behind.</summary>
    private static void CopyBinaries(string source, string dest) {
        // Never carry the installed instance's mutable state into the copy — the session gets a fresh,
        // agent-ready config written separately. Also never recurse into the sessions root if it happens
        // to live under the binaries dir.
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source)) {
            string name = Path.GetFileName(file);
            if (IsExcludedFile(name)) {
                continue;
            }
            File.Copy(file, Path.Combine(dest, name), overwrite: true);
        }
        foreach (string subDir in Directory.GetDirectories(source)) {
            if (string.Equals(Path.GetFullPath(subDir).TrimEnd(Path.DirectorySeparatorChar),
                              Path.GetFullPath(SessionsRoot).TrimEnd(Path.DirectorySeparatorChar),
                              StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            CopyBinaries(subDir, Path.Combine(dest, Path.GetFileName(subDir)));
        }
    }

    private static bool IsExcludedFile(string name) =>
        name.Equals(AppSettings.SettingsFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals("mcec.commands", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes the co-located, agent-ready <c>mcec.settings</c> for the session.</summary>
    private static void WriteSessionSettings(string dir, bool mcpServerEnabled, int port) {
        AppSettings settings = new() {
            AgentCommandsEnabled = true,          // this copy is FOR the agent; enabled only here
            McpServerEnabled = mcpServerEnabled,
            McpBindAddress = "127.0.0.1",         // localhost only
            McpHttpPort = port,
            ActAsServer = false,                  // no TCP server → no Windows Firewall prompt (isolation practice)
            ActAsSerialServer = false,
            ActAsClient = false,
            ActivityMonitorEnabled = false,
            CommandOverlayEnabled = true,         // keep the "MCEC is driving" overlay for auditability
            EmergencyStopEnabled = true,          // the operator panic hotkey still applies to the session
            AllowSessionProvisioning = false,     // a provisioned session must not re-provision
            HideOnStartup = false,
        };
        settings.Serialize(Path.Combine(dir, AppSettings.SettingsFileName));
    }

    /// <summary>Writes the co-located <c>mcec.commands</c> enabling the requested agent commands.</summary>
    private static void WriteSessionCommands(string dir, IReadOnlyList<string> commands, string version) {
        List<Command> list = [];
        foreach (string name in commands) {
            Command? cmd = CreateEnabledCommand(name);
            if (cmd is null) {
                Logger.Instance.Log4.Warn($"SessionProvisioner: unknown command '{name}' requested; skipping.");
                continue;
            }
            list.Add(cmd);
        }
        SerializedCommands sc = new() { commandArray = [.. list] };
        SerializedCommands.SaveCommands(Path.Combine(dir, "mcec.commands"), sc, version);
    }

    /// <summary>
    /// Builds the correctly-typed <see cref="Command"/> for a name, enabled, so it serializes under the
    /// right element (query/capture/invoke/…) and loads back as the right derived type. Returns null for an
    /// unknown name.
    /// </summary>
    private static Command? CreateEnabledCommand(string name) => name.ToLowerInvariant() switch {
        "capture" => new CaptureCommand { Cmd = "capture", Enabled = true },
        "query" => new QueryCommand { Cmd = "query", Enabled = true },
        "displays" => new DisplaysCommand { Cmd = "displays", Enabled = true },
        "find" => new FindCommand { Cmd = "find", Enabled = true },
        "wait-for" => new FindCommand { Cmd = "wait-for", Enabled = true },
        "invoke" => new InvokeCommand { Cmd = "invoke", Enabled = true },
        "drag" => new DragCommand { Cmd = "drag", Enabled = true },
        "click" => new ClickCommand { Cmd = "click", Enabled = true },
        "record" => new RecordCommand { Cmd = "record", Enabled = true },
        _ => null,
    };

    /// <summary>Asks the OS for a free loopback TCP port by binding to port 0 and reading the assignment.</summary>
    private static int FindFreeLoopbackPort() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        try {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally {
            listener.Stop();
        }
    }

    private static bool TryDeleteDirectory(string dir) {
        try {
            if (Directory.Exists(dir)) {
                Directory.Delete(dir, recursive: true);
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Files are locked (session still running) — leave it for a later reap.
            Logger.Instance.Log4.Warn($"SessionProvisioner: could not delete '{dir}': {e.Message}");
            return false;
        }
    }
}
