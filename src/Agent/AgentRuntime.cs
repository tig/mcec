// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// <see cref="AgentCommandsEnabled"/>; a SEPARATE opt-in from the existing actuation enable.
/// Enabling "press keys" must not silently enable "screenshot my screen". Every agent action is
/// logged loudly via <see cref="Audit"/>.
/// </summary>
public static class AgentRuntime {
    public static AppSettings? Settings { get; set; }

    public static CommandInvoker? Invoker { get; set; }

    /// <summary>
    /// The host-capability half of the seam (#209): outbound lines, shutdown, and the message-window
    /// handle; the few genuinely host-specific things engine code needs. GUI mode registers
    /// <c>MainWindow</c> (in its settings-apply path, alongside <see cref="Settings"/>); headless
    /// <c>--mcp</c> registers <see cref="HeadlessAppHost"/>. Engine code calls the wrappers below,
    /// never <c>MainWindow</c>; touching <c>MainWindow.Instance</c> below the UI layer throws.
    /// </summary>
    public static IAppHost? Host { get; set; }

    /// <summary>
    /// Sends a line to every connected transport via the registered <see cref="Host"/>. With no host
    /// registered (early startup, tests) the line is dropped with a log trace; never an exception,
    /// because callers include the activity monitor's background dispatch path.
    /// </summary>
    public static void SendLine(string line) {
        IAppHost? host = Host;
        if (host is null) {
            Logger.Instance.Log4.Debug($"AgentRuntime: SendLine with no host registered; dropped: {line}");
            return;
        }
        host.SendLine(line);
    }

    /// <summary>
    /// Requests an orderly application shutdown via the registered <see cref="Host"/> (GUI:
    /// <c>MainWindow.ShutDown()</c>; headless: clean process exit after replies flush). With no host
    /// registered this logs and returns; a stray <c>mcec:exit</c> in a test process must not kill the
    /// test runner.
    /// </summary>
    public static void RequestShutdown() {
        IAppHost? host = Host;
        if (host is null) {
            Logger.Instance.Log4.Warn("AgentRuntime: RequestShutdown with no host registered; ignored.");
            return;
        }
        host.RequestShutdown();
    }

    /// <summary>
    /// A window handle for OS notification registration (see <see cref="IAppHost.MessageWindowHandle"/>).
    /// Throws with a pointed message when no host is registered; better than the silent Form
    /// construction a stray <c>MainWindow.Instance.Handle</c> used to cause.
    /// </summary>
    public static IntPtr MessageWindowHandle =>
        (Host ?? throw new InvalidOperationException(
            "AgentRuntime.MessageWindowHandle requires a registered IAppHost (AgentRuntime.Host); " +
            "GUI mode registers MainWindow; headless --mcp has no message window."))
        .MessageWindowHandle;

    /// <summary>
    /// The single gate over the ONE physical desktop input stream (#113/#195). Two actuation paths
    /// synthesize global input and must never interleave: (1) the <see cref="CommandInvoker"/>
    /// dispatcher thread, which holds this around a queued command's <c>Execute</c> when the command
    /// can synthesize input (<c>Command.SynthesizesInput</c>; e.g. <c>pause</c> opts out so a long
    /// sleep can't starve a drag), and (2) <c>AgentServer</c>'s <c>drag</c> tool, which actuates a
    /// press→move→release gesture directly on an MCP worker under this lock. It lives here; not in
    /// <c>AgentServer</c>; because both the command engine and the agent front door need it, and
    /// the engine must work headless.
    /// <para>
    /// LOCK ORDERING: this is a LEAF lock. Never acquire another lock, block on the command queue, or
    /// wait on a task/thread while holding it; a command's <c>Execute</c> may take seconds (paced
    /// macros), and anything that waits on the dispatcher while holding this deadlocks it.
    /// Observation (query/capture/find/wait-for/record) deliberately never takes it.
    /// ONE deliberate exception (#307): <c>request-command-access</c> holds this gate for the whole
    /// lifetime of the operator consent prompt (bounded by the prompt's own timeout), precisely SO
    /// synthesized input cannot land while the dialog is up; only physical input can answer it. Safe
    /// against the deadlock rule because the prompt's UI thread never takes this gate and waits on
    /// nothing that does; actuation stalls behind it by design.
    /// </para>
    /// <para>
    /// KNOWN HAZARD (queue-path modal invoke): the agent's <c>invoke</c> TOOL runs on a worker with
    /// the #105 modal grace and never holds this gate, but an <c>invoke</c>-style command executed
    /// FROM THE QUEUE (in a macro, or raw via <c>send_command</c>) has no such grace; if it opens a
    /// modal dialog its Execute blocks the dispatcher (and holds this gate) until the dialog is
    /// dismissed. Agent callers are bounded by <c>send_command</c>'s 30s completion wait; the queue
    /// itself stalls until the operator closes the dialog. Prefer the <c>invoke</c> tool for
    /// anything that may open a modal.
    /// </para>
    /// </summary>
    internal static readonly object InputGate = new();

    /// <summary>
    /// True when running as the headless MCP server (<c>--mcp</c>). In this mode the engine's
    /// load/save paths MUST NOT show modal dialogs (nothing may block protocol startup, and stdout is
    /// the protocol stream), so they suppress their <c>MessageBox</c> prompts when this is set. The
    /// one deliberate UI exception is the operator safety surface (<see cref="HeadlessOperatorUi"/>:
    /// overlay + e-stop re-arm prompt), which runs on its own pump thread and blocks nothing.
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
        Logger.Instance.Log4.Info($"AGENT-AUDIT: {action}; {detail}");

    // -------------------------------------------------------------------------------------------
    // Emergency stop latch (#135)
    // -------------------------------------------------------------------------------------------

    private static volatile bool _emergencyStopped;

    /// <summary>
    /// True while the operator's emergency stop (#135) is engaged. It <b>latches</b>: once set, every
    /// actuation dispatch is refused until the operator explicitly re-arms; the panic override must not be
    /// silently cleared by the next tool call. The actuation gate checks this alongside
    /// <see cref="AgentCommandsEnabled"/>; <see cref="EmergencyStop"/> sets and clears it. <c>volatile</c>
    /// because it is read on the dispatch thread and written from the global-hook thread.
    /// </summary>
    public static bool EmergencyStopped => _emergencyStopped;

    /// <summary>Sets the emergency-stop latch. Called only by <see cref="EmergencyStop"/>.</summary>
    internal static void SetEmergencyStopped(bool value) => _emergencyStopped = value;

    // -------------------------------------------------------------------------------------------
    // Session runtime (#86)
    // -------------------------------------------------------------------------------------------

    private static readonly Lock _sessionGate = new();

    /// <summary>
    /// Live sessions keyed by <see cref="AgentSession.SessionId"/> (Phase 3). Both the implicit default
    /// session and every explicitly <c>session-start</c>ed one are registered here, so a call can be
    /// routed to any of them by echoing its id. Sessions are removed by <c>session-end</c> (or a test
    /// reset); there is no time-based reaping in-process (a session is cheap; its only heap cost is the
    /// small last-target/observation/action/error state, and capture bytes already spill to disk).
    /// </summary>
    private static readonly Dictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// The implicit default session: the one a call with no <c>sessionId</c> runs in, so the common
    /// single-agent / stdio case "just works" with one ambient session (Phase 3, decision 1). Created on
    /// first use and also registered in <see cref="_sessions"/> under its id, so an agent that reads the
    /// default's id from <c>session-status</c> can then address it explicitly and get the same instance.
    /// </summary>
    private static AgentSession? _defaultSession;
    private static string? _artifactRoot;

    /// <summary>
    /// Root directory under which each session reserves its artifact subdirectory. Defaults to a
    /// <c>sessions</c> folder beside MCEC's settings/logs (or a temp fallback if that can't be resolved);
    /// overridable by the host or tests. Setting it clears every live session so the next ones use it.
    /// </summary>
    public static string ArtifactRoot {
        get {
            lock (_sessionGate) {
                return _artifactRoot ??= DefaultArtifactRoot();
            }
        }
        set {
            lock (_sessionGate) {
                _artifactRoot = value;
                _sessions.Clear();
                _defaultSession = null;
            }
        }
    }

    /// <summary>
    /// The implicit default agent session (#86), created and registered on first use. A tool call that
    /// carries no <c>sessionId</c> runs here; explicit <c>session-start</c> creates additional addressable
    /// sessions alongside it (see <see cref="StartSession"/>/<see cref="TryResolveSession"/>). The session
    /// carries <c>sessionId</c> onto every result and the last target/observation/action/error for
    /// debugging and replay.
    /// </summary>
    public static AgentSession Session {
        get {
            lock (_sessionGate) {
                return _defaultSession ??= CreateAndRegister();
            }
        }
    }

    /// <summary>
    /// Phase 3 (<c>session-start</c>): creates, registers, and returns a fresh addressable session. Unlike
    /// <see cref="Session"/> it never becomes the default; the agent routes to it by echoing the returned
    /// <see cref="AgentSession.SessionId"/> as the <c>sessionId</c> argument on later calls.
    /// </summary>
    public static AgentSession StartSession() {
        lock (_sessionGate) {
            return CreateAndRegister();
        }
    }

    /// <summary>
    /// Resolves the session a call runs in (Phase 3, decision 1): the session named by
    /// <paramref name="sessionId"/>, or the implicit default when it is null/empty. Returns false with
    /// <paramref name="session"/> null when a non-empty id names no live session, so the caller can refuse
    /// the call rather than silently forking a new session's state under a stale id.
    /// </summary>
    public static bool TryResolveSession(string? sessionId, [MaybeNullWhen(false)] out AgentSession session) {
        lock (_sessionGate) {
            // Only an absent/empty id means "default"; a whitespace-only id is non-empty content, so it is
            // NOT treated as missing (it will simply match no live session and be refused). Otherwise a
            // stray " " would silently route into the default session and fork the state an explicit
            // session was meant to isolate.
            if (string.IsNullOrEmpty(sessionId)) {
                session = _defaultSession ??= CreateAndRegister();
                return true;
            }
            return _sessions.TryGetValue(sessionId.Trim(), out session);
        }
    }

    /// <summary>
    /// Phase 3 (<c>session-end</c>): removes a session by id, returning whether it existed. Ending the
    /// default session drops the ambient pointer so the next default access starts a fresh one; a
    /// subsequent call with that (now dead) id resolves to nothing and is refused.
    /// </summary>
    public static bool EndSession(string sessionId) {
        lock (_sessionGate) {
            string id = sessionId?.Trim() ?? "";
            bool removed = _sessions.Remove(id);
            if (_defaultSession?.SessionId == id) {
                _defaultSession = null;
            }
            return removed;
        }
    }

    /// <summary>Drops every live session so the next access starts fresh (used by tests; a <c>session-end</c> on a specific id is <see cref="EndSession"/>).</summary>
    public static void ResetSession() {
        lock (_sessionGate) {
            _sessions.Clear();
            _defaultSession = null;
        }
    }

    /// <summary>Creates a session against the current artifact root and registers it in <see cref="_sessions"/>. Caller holds <see cref="_sessionGate"/>.</summary>
    private static AgentSession CreateAndRegister() {
        AgentSession session = AgentSession.Create(_artifactRoot ??= DefaultArtifactRoot());
        _sessions[session.SessionId] = session;
        return session;
    }

    private static string DefaultArtifactRoot() {
        try {
            string baseDir = SettingsStore.GetSettingsPath(Application.StartupPath);
            return Path.Combine(baseDir, "sessions");
        }
        catch (Exception e) {
            Logger.Instance.Log4.Warn($"AgentRuntime: falling back to temp artifact root: {e.Message}");
            return Path.Combine(Path.GetTempPath(), "MCEC", "sessions");
        }
    }
}
