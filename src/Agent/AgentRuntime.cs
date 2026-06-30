// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;

namespace MCEControl;

/// <summary>
/// The UI-agnostic seam between MCEC's command engine and whatever is hosting it. In GUI mode
/// <see cref="MainWindow"/> populates <see cref="Settings"/> and <see cref="Invoker"/> after load;
/// in headless MCP mode (<c>--mcp</c>) the bootstrap in <see cref="Program"/> does the same. Agent
/// code reads gating and the command table from here rather than reaching into <c>MainWindow</c>,
/// so the engine works with no window (the "headless first" requirement from the proposal).
///
/// SECURITY: the agent observation commands (capture/query/find/invoke) are gated by
/// <see cref="AgentCommandsEnabled"/> — a SEPARATE opt-in from the existing actuation enable.
/// Enabling "press keys" must not silently enable "screenshot my screen". Every agent action is
/// logged loudly via <see cref="Audit"/>.
/// </summary>
public static class AgentRuntime {
    public static AppSettings? Settings { get; set; }

    public static CommandInvoker? Invoker { get; set; }

    /// <summary>
    /// True when running as the headless MCP server (<c>--mcp</c>). In this mode the engine MUST NOT
    /// show modal dialogs (there is no operator at a screen and stdout is the protocol stream), so the
    /// settings/commands load paths suppress their <c>MessageBox</c> prompts when this is set.
    /// </summary>
    public static bool Headless { get; set; }

    /// <summary>
    /// True only when the user has explicitly opted in to the agent observation/targeting commands.
    /// Defaults to false (disabled) when no settings are loaded.
    /// </summary>
    public static bool AgentCommandsEnabled => Settings?.AgentCommandsEnabled ?? false;

    /// <summary>
    /// Loud, structured audit log for every observation/agent action: what was captured/queried,
    /// when, and with what target. Deliberately logged at Info so it shows in the GUI log view.
    /// </summary>
    public static void Audit(string action, string detail) =>
        Logger.Instance.Log4.Info($"AGENT-AUDIT: {action} — {detail}");

    // -------------------------------------------------------------------------------------------
    // Session runtime (#86)
    // -------------------------------------------------------------------------------------------

    private static readonly object SessionGate = new();
    private static AgentSession? _session;
    private static string? _artifactRoot;

    /// <summary>
    /// Root directory under which each session reserves its artifact subdirectory. Defaults to a
    /// <c>sessions</c> folder beside MCEC's settings/logs (or a temp fallback if that can't be resolved);
    /// overridable by the host or tests. Setting it clears the ambient session so the next one uses it.
    /// </summary>
    public static string ArtifactRoot {
        get {
            lock (SessionGate) {
                return _artifactRoot ??= DefaultArtifactRoot();
            }
        }
        set {
            lock (SessionGate) {
                _artifactRoot = value;
                _session = null;
            }
        }
    }

    /// <summary>
    /// The ambient agent session, created on first use. Phase 2 (#86) runs a single runner-owned session;
    /// explicit <c>session/start|status|end</c> lifecycle and per-call routing arrive in Phase 3. The
    /// session carries <c>sessionId</c> onto every result and the last target/observation/action/error
    /// for debugging and replay.
    /// </summary>
    public static AgentSession Session {
        get {
            lock (SessionGate) {
                return _session ??= AgentSession.Create(_artifactRoot ??= DefaultArtifactRoot());
            }
        }
    }

    /// <summary>Drops the ambient session so the next access starts a fresh one (used by tests and a future <c>session/end</c>).</summary>
    public static void ResetSession() {
        lock (SessionGate) {
            _session = null;
        }
    }

    private static string DefaultArtifactRoot() {
        try {
            string baseDir = AppSettings.GetSettingsPath(System.Windows.Forms.Application.StartupPath);
            return Path.Combine(baseDir, "sessions");
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"AgentRuntime: falling back to temp artifact root: {e.Message}");
            return Path.Combine(Path.GetTempPath(), "MCEC", "sessions");
        }
    }
}
