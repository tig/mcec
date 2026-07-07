// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace MCEControl;

/// <summary>
/// Provisions a fresh, disposable, isolated MCEC instance (#138). Instead of an agent mutating the
/// operator's <b>installed</b> config; flipping <c>AgentCommandsEnabled</c> / per-command <c>Enabled</c>
/// and hoping best-effort cleanup runs; MCEC hands the agent a throwaway directory containing
/// <c>mcec.exe</c> + dependencies and a <b>co-located</b>, agent-ready config. All enabled state lives only
/// inside that copy, so a crashed or abandoned session leaves the real install untouched and "cleanup" is
/// just deleting the directory. Concurrent sessions get separate directories and never fight over one file.
///
/// <para>SECURITY: provisioning itself is gated behind <see cref="AppSettings.AllowSessionProvisioning"/>;
/// the one thing that cannot be self-served, or the isolation is theater. The co-located config disables
/// <c>ActAsServer</c> (no firewall prompt) and <c>AllowSessionProvisioning</c> (a provisioned session can't
/// re-provision), and binds the MCP server to localhost. The installed <c>mcec.settings</c> /
/// <c>mcec.commands</c> are never read or written.</para>
/// </summary>
public static class SessionProvisioner {
    /// <summary>
    /// The agent observation/action commands enabled in a provisioned session's co-located command
    /// table: the <see cref="ToolDescriptor.ProvisionedByDefault"/> members of the
    /// <see cref="ToolCatalog"/> (#205); today every gated tool except <c>launch</c>.
    /// </summary>
    public static readonly string[] DefaultCommands =
        [.. ToolCatalog.All.Where(d => d.ProvisionedByDefault).Select(d => d.Name)];

    /// <summary>
    /// Agent UIA dependencies that must be co-located with <c>mcec.exe</c> in every provisioned copy.
    /// A session missing these cannot run (or safely shut down after) agent observation commands (#317).
    /// </summary>
    internal static readonly string[] RequiredAgentAssemblies = ["FlaUI.Core.dll", "FlaUI.UIA3.dll"];

    // A provisioned session id is a bare 12-char lowercase-hex token (Guid "N"[..12]). end-session accepts
    // ONLY this shape so a caller can never pass a path/traversal token (e.g. ".." or "a/b") that would make
    // Teardown delete something outside the sessions root. end-session is exposed over MCP and is not behind
    // the provisioning-authorization gate, so this validation is the security boundary.
    private static readonly Regex _sessionIdPattern = new("^[0-9a-f]{12}$", RegexOptions.Compiled);

    /// <summary>
    /// Root under which each session gets its own directory. Defaults to
    /// <c>%LOCALAPPDATA%\MCEC\sessions</c> (temp fallback if that can't be resolved). Settable by tests.
    /// </summary>
    public static string SessionsRoot {
        get => field ??= DefaultSessionsRoot();
        set;
    }

    /// <summary>The directory the running mcec.exe (and its dependencies) live in; the copy source.</summary>
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
        string version = Application.ProductVersion;
        int port = mcpServerEnabled ? FindFreeLoopbackPort() : 0;
        // The session credential (#215): written into the co-located config as the provisioned
        // instance's McpAuthToken (so every HTTP request to the session's MCP endpoint must carry
        // `Authorization: Bearer <token>`) and required by end-session as the teardown credential
        // (validated against that same co-located config). Possession of the token == ownership of
        // the session.
        string token = Guid.NewGuid().ToString("N");
        string[] enable = [.. (commands ?? DefaultCommands)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        try {
            Directory.CreateDirectory(dir);
            VerifyAgentDependencies(BinariesDir, sessionDir: null);
            CopyBinaries(BinariesDir, dir);
            VerifyAgentDependencies(dir, sessionDir: dir);
            WriteSessionSettings(dir, mcpServerEnabled, port, token);
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
            Token = token,
        };
    }

    /// <summary>
    /// Validates the teardown credential for <paramref name="sessionId"/> against the session's
    /// co-located config; the token <see cref="Provision"/> wrote as the instance's
    /// <c>McpAuthToken</c> (#215). Persisting the credential in the session directory (rather than in
    /// this process) means validation works across installed-instance restarts, and deleting the
    /// directory retires the credential with it. Fail-closed: an unreadable/defaulted config (or a
    /// pre-#215 session that never had the token written) rejects; such a directory is still
    /// collected by the age-based reaper.
    /// </summary>
    public static SessionTokenValidation ValidateTeardownToken(string? sessionId, string? token) {
        string id = sessionId?.Trim() ?? "";
        if (!_sessionIdPattern.IsMatch(id)) {
            return SessionTokenValidation.InvalidId;
        }
        string dir = Path.Combine(SessionsRoot, id);
        if (!Directory.Exists(dir)) {
            // Idempotent-teardown case: nothing exists, so there is nothing the credential protects.
            return SessionTokenValidation.SessionGone;
        }
        // Fail closed on a missing config WITHOUT calling SettingsStore.Load; Load would write a
        // default settings file into the (possibly foreign) directory as a side effect.
        string settingsFile = Path.Combine(dir, SettingsStore.SettingsFileName);
        if (!File.Exists(settingsFile)) {
            return SessionTokenValidation.TokenMismatch;
        }
        SettingsLoadResult load = SettingsStore.Load(settingsFile);
        string expected = load.Outcome == SettingsLoadOutcome.Loaded ? load.Settings.McpAuthToken : "";
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(token)) {
            return SessionTokenValidation.TokenMismatch;
        }
        byte[] a = System.Text.Encoding.UTF8.GetBytes(token);
        byte[] b = System.Text.Encoding.UTF8.GetBytes(expected);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b)
            ? SessionTokenValidation.Valid
            : SessionTokenValidation.TokenMismatch;
    }

    /// <summary>
    /// Tears down a provisioned session by deleting its directory. Returns true when the directory was
    /// removed (or was already gone). A directory whose files are still locked (the session is running)
    /// returns false; stop the session first.
    /// </summary>
    public static bool Teardown(string sessionId) {
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return false;
        }
        // Guard against path traversal: only a well-formed session id (bare 12-hex token) is accepted, so a
        // caller can never point Teardown at a directory outside the sessions root (e.g. "..", "a/b", rooted paths).
        string id = sessionId.Trim();
        if (!_sessionIdPattern.IsMatch(id)) {
            AgentRuntime.Audit("end-session", $"REJECTED; '{id}' is not a valid session id (expected 12 hex chars)");
            return false;
        }
        string dir = Path.Combine(SessionsRoot, id);
        // Defense in depth: confirm the resolved path really is inside the sessions root before deleting.
        string fullRoot = Path.GetFullPath(SessionsRoot).TrimEnd(Path.DirectorySeparatorChar);
        string fullDir = Path.GetFullPath(dir);
        if (!fullDir.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            AgentRuntime.Audit("end-session", $"REJECTED; '{id}' resolves outside the sessions root");
            return false;
        }
        if (!Directory.Exists(dir)) {
            AgentRuntime.Audit("end-session", $"{id}; directory already gone");
            return true;
        }
        bool ok = TryDeleteDirectory(dir);
        AgentRuntime.Audit("end-session", ok ? $"{id}; torn down ({dir})" : $"{id}; could not delete (in use?) {dir}");
        return ok;
    }

    /// <summary>
    /// Enumerates the session directories under <see cref="SessionsRoot"/> for the operator's
    /// management surface (#259; the Settings dialog's Agent tab). Lists every directory (not just
    /// well-formed ids), matching the reaper's scope, so a leftover the reaper would eventually
    /// collect is visible too. Best-effort; never throws; a directory that cannot be stat'ed is
    /// skipped with a warning. Ordered newest first.
    /// </summary>
    public static IReadOnlyList<ProvisionedSessionInfo> ListSessions() {
        List<ProvisionedSessionInfo> sessions = [];
        try {
            if (!Directory.Exists(SessionsRoot)) {
                return sessions;
            }
            foreach (string dir in Directory.GetDirectories(SessionsRoot)) {
                try {
                    sessions.Add(new ProvisionedSessionInfo {
                        SessionId = Path.GetFileName(dir),
                        Directory = dir,
                        CreatedUtc = Directory.GetCreationTimeUtc(dir),
                        SizeBytes = GetDirectorySize(dir),
                        IsRunning = IsSessionRunning(dir),
                    });
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Warn($"SessionProvisioner: could not stat '{dir}': {e.Message}");
                }
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"SessionProvisioner: listing sessions failed: {e.Message}");
        }
        return [.. sessions.OrderByDescending(s => s.CreatedUtc)];
    }

    /// <summary>
    /// Whether the session's <c>mcec.exe</c> is locked, i.e. the session is running. The loader holds
    /// a running image open, so an open demanding exclusive access fails with a sharing violation;
    /// that same lock is what makes <see cref="Teardown"/>/the reaper skip the directory.
    /// </summary>
    private static bool IsSessionRunning(string dir) {
        string exe = Path.Combine(dir, "mcec.exe");
        if (!File.Exists(exe)) {
            return false;
        }
        try {
            using FileStream _ = File.Open(exe, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return true;
        }
    }

    /// <summary>Total size of the directory's files in bytes; best-effort (0 on failure).</summary>
    private static long GetDirectorySize(string dir) {
        try {
            return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"SessionProvisioner: could not size '{dir}': {e.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Removes a directory that <see cref="ListSessions"/> reported, for the operator's management surface
    /// (#259; the Agent tab's Delete). Unlike <see cref="Teardown"/> (the MCP <c>end-session</c> boundary,
    /// which accepts only a well-formed 12-hex id because it serves untrusted callers), this deletes ANY
    /// listed directory, matching the reaper's scope; so the operator can remove exactly what the tab
    /// shows, including a stray non-session-id folder the reaper would eventually collect. The id here is a
    /// directory name that came from <see cref="ListSessions"/> (never free-form input), but a single
    /// path-containment check is kept as defense in depth so a delete can never escape the sessions root.
    /// Returns true when the directory was removed (or was already gone); false when its files are locked
    /// (a running session). Best-effort; never throws.
    /// </summary>
    public static bool RemoveListedSession(string sessionId) {
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return false;
        }
        string name = sessionId.Trim();
        if (name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar)
            || name is ".." or ".") {
            AgentRuntime.Audit("delete-session", $"REJECTED; '{name}' is not a plain session directory name");
            return false;
        }
        string dir = Path.Combine(SessionsRoot, name);
        string fullRoot = Path.GetFullPath(SessionsRoot).TrimEnd(Path.DirectorySeparatorChar);
        string fullDir = Path.GetFullPath(dir);
        if (!fullDir.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            AgentRuntime.Audit("delete-session", $"REJECTED; '{name}' resolves outside the sessions root");
            return false;
        }
        if (!Directory.Exists(dir)) {
            return true; // already gone; idempotent
        }
        bool ok = TryDeleteDirectory(dir);
        AgentRuntime.Audit("delete-session", ok ? $"{name}; removed by operator ({dir})" : $"{name}; could not delete (in use?) {dir}");
        return ok;
    }

    /// <summary>
    /// Belt-and-suspenders cleanup: deletes session directories older than <paramref name="maxAge"/> so a
    /// leaked/abandoned session never lingers. A running session's files are locked and are skipped (they'll
    /// be reaped on a later launch once the process exits). Best-effort; never throws. Returns the count
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
                    continue; // too new; could be an active or just-provisioned session
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

    /// <summary>
    /// Confirms every <see cref="RequiredAgentAssemblies"/> file exists under
    /// <paramref name="binariesDir"/> (and, when <paramref name="sessionDir"/> is set, was copied into
    /// the session). Throws before a partial provision is handed off.
    /// </summary>
    private static void VerifyAgentDependencies(string binariesDir, string? sessionDir) {
        foreach (string name in RequiredAgentAssemblies) {
            string path = Path.Combine(binariesDir, name);
            if (!File.Exists(path)) {
                string where = sessionDir is null ? $"the running install at '{binariesDir}'" : $"the provisioned copy at '{sessionDir}'";
                throw new InvalidOperationException($"Required agent dependency '{name}' is missing from {where}. Reinstall or rebuild MCEC.");
            }
        }
    }

    /// <summary>Recursively copies the binaries, skipping any mutable config/log the installed instance left behind.</summary>
    private static void CopyBinaries(string source, string dest) {
        // Never carry the installed instance's mutable state into the copy; the session gets a fresh,
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
        name.Equals(SettingsStore.SettingsFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals("mcec.commands", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes the co-located, agent-ready <c>mcec.settings</c> for the session.</summary>
    private static void WriteSessionSettings(string dir, bool mcpServerEnabled, int port, string token) {
        AppSettings settings = new() {
            AgentCommandsEnabled = true,          // this copy is FOR the agent; enabled only here
            McpServerEnabled = mcpServerEnabled,
            McpBindAddress = "127.0.0.1",         // localhost only
            McpHttpPort = port,
            // #215: the session credential. The instance's HTTP gate already enforces a configured
            // McpAuthToken as a required Bearer (#143), so setting it here means only the token
            // holder can drive the session's MCP endpoint; end-session validates teardown against
            // this same value (ValidateTeardownToken). Written only into the disposable copy.
            McpAuthToken = token,
            ActAsServer = false,                  // no TCP server → no Windows Firewall prompt (isolation practice)
            SocketServerBindAddress = "127.0.0.1", // defense in depth: if the TCP server is ever enabled, loopback only (#149)
            ActAsSerialServer = false,
            ActAsClient = false,
            ActivityMonitorEnabled = false,
            CommandOverlayEnabled = true,         // keep the "MCEC is driving" overlay for auditability
            EmergencyStopEnabled = true,          // the operator panic hotkey still applies to the session
            AllowSessionProvisioning = false,     // a provisioned session must not re-provision
            HideOnStartup = false,
        };
        // #216: no UI here (the store never shows dialogs); a write failure is logged by TrySave.
        _ = SettingsStore.TrySave(Path.Combine(dir, SettingsStore.SettingsFileName), settings, out _);
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
    /// right element (query/capture/invoke/…) and loads back as the right derived type; via the tool's
    /// <see cref="ToolDescriptor.CreateCommandInstance"/> in the <see cref="ToolCatalog"/> (#205).
    /// Returns null for an unknown name or one that is not provisionable
    /// (<see cref="ToolDescriptor.ProvisionedByDefault"/>; today that excludes only <c>launch</c>,
    /// preserving this provisioner's historical command set).
    /// </summary>
    private static Command? CreateEnabledCommand(string name) {
        if (!ToolCatalog.TryGet(name.ToLowerInvariant(), out ToolDescriptor descriptor) || !descriptor.ProvisionedByDefault) {
            return null;
        }
        Command cmd = descriptor.CreateCommandInstance();
        cmd.Cmd = descriptor.Name;
        cmd.Enabled = true;
        return cmd;
    }

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
            // Files are locked (session still running); leave it for a later reap.
            Logger.Instance.Log4.Warn($"SessionProvisioner: could not delete '{dir}': {e.Message}");
            return false;
        }
    }
}
