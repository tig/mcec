// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// The tool-execution layer (#215): everything between a parsed <c>tools/call</c> and its #101
/// envelope; the security gates (emergency stop, <c>AgentCommandsEnabled</c>, per-command
/// <c>Enabled</c>), argument validation, <see cref="ToolCatalog"/> command building, the #113/#105
/// dispatch rules (input-gate serialization, invoke's modal grace), the meta-tools
/// (<c>send_command</c>, <c>provision-session</c>, <c>end-session</c>), and envelope/overlay
/// publication. Extracted from the old monolithic <c>AgentServer</c> as an instance type taking its
/// dependencies via constructor, so tests can exercise it without static seams; the production
/// instance is wired by the <see cref="AgentServer"/> facade against <see cref="AgentRuntime"/>.
/// </summary>
public sealed class AgentToolExecutor {
    // Grace period for an `invoke` to complete before we assume it opened a modal dialog and return.
    // Must stay above UiaService.InvokeFindTimeoutMs so an invoke's element lookup always resolves within
    // the grace; otherwise a missing element would be misreported as a pending modal (see #107).
    public const int InvokeModalGraceMs = 750;

    /// <summary>
    /// Upper bound a <c>send_command</c> call waits for its enqueued command tree to finish executing
    /// (#195). Bounded so one hung command (or a deep paced backlog ahead of it) can never wedge the
    /// MCP dispatch worker forever. 30s covers any sane paced macro (a full 50-command tree at the
    /// default 0ms–250ms pacing); a tree that legitimately runs longer keeps executing on the
    /// dispatcher; only the WAIT gives up, surfacing a timeout error to the agent.
    /// </summary>
    public const int SendCommandCompletionTimeoutMs = 30_000;

    /// <summary>Age after which an orphaned/abandoned provisioned session directory is reaped on launch/provision.</summary>
    public const int SessionReapAgeHours = 12;

    private readonly Func<AppSettings?> _settings;
    private readonly Func<CommandInvoker?> _invoker;
    private readonly Func<string?, AgentSession?> _resolveSession;
    private readonly Func<AgentSession> _startSession;
    private readonly Func<string, bool> _endSession;

    /// <param name="settings">Accessor for the live settings (gates + provisioning opt-in).</param>
    /// <param name="invoker">Accessor for the loaded command table/engine.</param>
    /// <param name="resolveSession">Routes a call to its session (#86 Phase 3): returns the session named
    /// by the id, the implicit default when the id is null/empty, or null when a non-empty id names no
    /// live session.</param>
    /// <param name="startSession">Creates and registers a fresh addressable session (<c>session-start</c>).</param>
    /// <param name="endSession">Ends a session by id (<c>session-end</c>); returns whether it existed.</param>
    public AgentToolExecutor(
        Func<AppSettings?> settings,
        Func<CommandInvoker?> invoker,
        Func<string?, AgentSession?> resolveSession,
        Func<AgentSession> startSession,
        Func<string, bool> endSession) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _resolveSession = resolveSession ?? throw new ArgumentNullException(nameof(resolveSession));
        _startSession = startSession ?? throw new ArgumentNullException(nameof(startSession));
        _endSession = endSession ?? throw new ArgumentNullException(nameof(endSession));
    }

    /// <summary>Routes a call to its session; false with <paramref name="session"/> null when a non-empty id names no live session.</summary>
    private bool TryResolveRoutedSession(string? sessionId, [MaybeNullWhen(false)] out AgentSession session) {
        session = _resolveSession(sessionId);
        return session is not null;
    }

    /// <summary>The implicit default session's id, for stamping an envelope on a call that has no routed session of its own (a refusal, or a global meta-op).</summary>
    private string DefaultSessionId() => _resolveSession(null)!.SessionId;

    /// <summary>The agent observation/actuation opt-in (#74) as seen through the injected settings.</summary>
    private bool AgentCommandsEnabled => _settings()?.AgentCommandsEnabled ?? false;

    /// <summary>
    /// Whether <paramref name="tool"/> serializes on the shared input gate
    /// (<see cref="AgentRuntime.InputGate"/>; the #113 contract). Global-input actuation
    /// (<c>drag</c>, <c>send_command</c>) does; it synthesizes physical mouse/keyboard, one shared
    /// stream that concurrent requests must not interleave. <c>drag</c> actuates directly under the
    /// gate on its MCP worker (its <see cref="ToolDescriptor.SerializesOnInput"/> flag);
    /// <c>send_command</c> is a meta-tool outside the <see cref="ToolCatalog"/> and serializes
    /// INDIRECTLY (#195): it enqueues into the <see cref="CommandInvoker"/>, whose single dispatcher
    /// thread holds the gate around each queued command's Execute; so it is special-cased here.
    /// Observation (<c>query</c>/<c>capture</c>/<c>find</c>/<c>wait-for</c>/<c>record</c>) does not
    /// (it runs concurrently); <c>invoke</c> is UIA-pattern actuation dispatched on a worker with the
    /// modal grace, not under this lock (#105).
    /// </summary>
    public static bool SerializesOnInputLock(string tool) =>
        tool == "send_command" || (ToolCatalog.TryGet(tool, out ToolDescriptor descriptor) && descriptor.SerializesOnInput);

    /// <summary>
    /// Whether <paramref name="tool"/> is still served while an operator consent prompt is open
    /// (#307): exactly the <see cref="ToolDescriptor.ServedDuringConsent"/> catalog members (pure
    /// observation). Derived, not hand-listed (#308 review; the #205 lesson): a new tool defaults to
    /// FROZEN, and the meta-tools (never in the catalog; <c>provision-session</c> especially, which
    /// could mint a second, unfrozen instance to answer the prompt) are frozen by construction.
    /// </summary>
    internal static bool ServedWhileConsentPending(string tool) =>
        ToolCatalog.TryGet(tool, out ToolDescriptor descriptor) && descriptor.ServedDuringConsent;

    /// <summary>The served-during-consent tool names for refusal text, from the same catalog flag.</summary>
    private static string ConsentServedNames() =>
        string.Join("/", ToolCatalog.All.Where(d => d.ServedDuringConsent).Select(d => d.Name));

    /// <summary>
    /// The consent-freeze refusal (#307), or null when the call may proceed (no prompt is open, or
    /// <paramref name="name"/> is served while one is).
    /// </summary>
    private JsonObject? ConsentPendingRefusal(string name) {
        if (!AgentConsent.IsPending || ServedWhileConsentPending(name)) {
            return null;
        }
        AgentRuntime.Audit(name, "BLOCKED; an operator consent prompt (request-command-access) is open");
        return ToolError(
            "An operator consent prompt (request-command-access) is open; actuation is paused until the operator answers. " +
            $"Only observation tools ({ConsentServedNames()}) are served right now. " +
            "Wait for the pending request-command-access call to return, then retry.",
            "consent-pending", AgentErrorCategory.Internal, DefaultSessionId());
    }

    // -------------------------------------------------------------------------------------------
    // tools/call
    // -------------------------------------------------------------------------------------------

    public JsonObject CallTool(JsonObject? prms, AgentTransport transport) {
        string name = AsString(prms?["name"]);
        JsonObject args = prms?["arguments"] as JsonObject ?? [];

        // Emergency stop (#135): once the operator engages the panic hotkey, EVERY tool call is refused
        // (actuation, observation, and raw send_command alike) until they explicitly re-arm; the human
        // override latches and is checked before anything else. A distinct error code tells the agent to
        // stop and surface it, not retry.
        if (AgentRuntime.EmergencyStopped) {
            AgentRuntime.Audit(name, "BLOCKED; emergency stop engaged; operator must re-arm");
            return ToolError(
                "Emergency stop is engaged; the operator halted this session. All tool calls are refused until they re-arm. Stop and tell the user; do not retry.",
                "emergency-stopped", AgentErrorCategory.Internal, DefaultSessionId());
        }

        // Provisioning bootstrap (#296): the installed (Program Files) copy serves ONLY the
        // isolated-install lifecycle (provision-session/end-session), so a fresh install's agent can
        // mint a disposable instance without the operator hand-editing configs. Everything else;
        // observation, actuation, raw send_command (which is otherwise ungated over stdio), and the
        // in-process session lifecycle; stays refused from the install; the full surface is served by
        // the provisioned copy the agent gets back. Checked before every branch below so no tool,
        // present or future, can slip past the restriction.
        if (Program.ProvisioningBootstrapOnly && name is not ("provision-session" or "end-session")) {
            AgentRuntime.Audit(name, "BLOCKED; the installed copy serves only the provisioning bootstrap (provision-session/end-session)");
            return ToolError(
                "This is MCEC's installed copy serving the provisioning bootstrap: only provision-session and " +
                "end-session are available here. Call provision-session to get a fresh, disposable, isolated " +
                "MCEC instance (it returns the directory, exePath, sessionId, and token), launch that " +
                "instance's mcec.exe --mcp (or use its HTTP mcpEndpoint), and drive it instead.",
                "bootstrap-only", AgentErrorCategory.Internal, DefaultSessionId());
        }

        // Operator consent in progress (#307): while a request-command-access prompt is on the
        // operator's screen, everything except the flagged observation tools is refused; BEFORE every
        // meta-tool branch below, so nothing dispatchable can reach or help answer the open prompt.
        // This is one of the three layers that keep the agent from answering its own prompt: `invoke`
        // is UIA-pattern actuation that deliberately never takes the input gate (#105), and
        // provision-session could mint a SECOND instance whose input none of this process's
        // protections block (#308 review); this dispatch refusal covers both.
        if (ConsentPendingRefusal(name) is { } consentRefusal) {
            return consentRefusal;
        }

        // Session lifecycle (#86 Phase 3): session-start/status/end. These read `sessionId` as an explicit
        // TARGET (which session to start/inspect/end), not as call routing, so they're handled before the
        // generic routing refusal below. They are part of the agent surface, so they honor the same
        // AgentCommandsEnabled opt-in as every other tool.
        if (name is "session-start" or "session-status" or "session-end") {
            if (!AgentCommandsEnabled) {
                AgentRuntime.Audit(name, "BLOCKED; agent commands disabled");
                return ToolError("Agent commands are disabled. Set AgentCommandsEnabled=true to opt in.", "agent-commands-disabled", AgentErrorCategory.Internal, DefaultSessionId());
            }
            return name switch {
                "session-start" => RunSessionStart(),
                "session-status" => RunSessionStatus(Str(args, "sessionId")),
                _ => RunSessionEnd(Str(args, "sessionId")),
            };
        }

        // Isolated-INSTALL lifecycle (#138): provision-session / end-session. Their `sessionId` names a
        // provisioned MCEC *install* on disk, NOT an in-process agent session, so they must NOT be routed
        // through the resolver below; they carry their own gates (provisioning opt-in / teardown token) and
        // just stamp the default session id onto their envelope.
        if (name == "provision-session") {
            return RunProvisionSession(args, _resolveSession(null)!);
        }
        if (name == "end-session") {
            return RunEndSession(args, _resolveSession(null)!);
        }

        // Route this call to its session (#86 Phase 3, decision 1): an explicit sessionId (from
        // session-start) runs the call in that session and is echoed on the result; absent, the implicit
        // default session is used so the single-agent / stdio case just works. A non-empty id that names
        // no live session is refused rather than silently spawning a new one, so a stale id can't fork
        // state unnoticed. Every branch below stamps the resolved session's id onto its envelope.
        string? routeId = Str(args, "sessionId");
        if (!TryResolveRoutedSession(routeId, out AgentSession? session)) {
            AgentRuntime.Audit(name, $"BLOCKED; unknown sessionId '{routeId}'");
            // Stamp the REJECTED id, not the default session: a client that carries envelope.sessionId
            // forward after an error must not be silently handed the default session (which would fork the
            // isolated task's state). We only reach here for a non-empty id (empty routes to default above).
            return ToolError(
                $"Unknown sessionId '{routeId}'. It was never started or has ended. Call session-start to get a fresh sessionId, or omit sessionId to use the default session.",
                "unknown-session", AgentErrorCategory.InvalidArgument, routeId!);
        }

        // Command-access consent (#307): the agent asks, the OPERATOR decides, on MCEC's own dialog.
        if (name == "request-command-access") {
            return RunRequestCommandAccess(args, transport, session);
        }

        if (name == "send_command") {
            // #153: send_command is a raw command-injection surface. Over the network-facing HTTP floor it
            // must NOT be reachable unless the operator opted into the agent surface; otherwise enabling
            // McpServerEnabled alone (with AgentCommandsEnabled=false) leaves a CSRF/DNS-rebinding-reachable
            // (#143) raw pass-through. So over HTTP it honors the SAME AgentCommandsEnabled gate as every
            // other tool. Over local stdio (the operator launched mcec.exe --mcp; no CSRF surface) the
            // documented raw pass-through stays available; the per-command Enabled table still applies below.
            if (transport == AgentTransport.Http && !AgentCommandsEnabled) {
                AgentRuntime.Audit("send_command", "BLOCKED; agent commands disabled; send_command over HTTP requires AgentCommandsEnabled");
                return ToolError(
                    "send_command over HTTP requires AgentCommandsEnabled=true. Enable the agent surface to opt in, or drive send_command over the local stdio transport (mcec.exe --mcp).",
                    "agent-commands-disabled", AgentErrorCategory.Internal, session.SessionId);
            }
            return RunSendCommand(args, session);
        }

        // The gated agent tools are exactly the ToolCatalog membership (#205); no hand-synced whitelist.
        if (ToolCatalog.Contains(name)) {
            if (!AgentCommandsEnabled) {
                AgentRuntime.Audit(name, "BLOCKED; agent commands disabled");
                return ToolError("Agent commands are disabled. Set AgentCommandsEnabled=true to opt in.", "agent-commands-disabled", AgentErrorCategory.Internal, session.SessionId);
            }
            // `drag`/`click` generate real mouse input from their endpoints, and a missing pixel field would
            // otherwise default to 0 and actuate at a bogus coordinate. Reject an ill-formed endpoint up
            // front rather than actuating it.
            if (name == "drag" && DragArgsError(args) is { } dragError) {
                return ToolError(dragError, "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
            }
            if (name == "click" && ClickArgsError(args) is { } clickError) {
                return ToolError(clickError, "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
            }
            if (name == "focus" && FocusArgsError(args) is { } focusError) {
                return ToolError(focusError, "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
            }
            return RunAgentCommand(name, args, session);
        }

        return ToolError($"Unknown tool: {name}", "unknown-tool", AgentErrorCategory.Internal, session.SessionId);
    }

    // -------------------------------------------------------------------------------------------
    // Session lifecycle tools (#86 Phase 3)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>session-start</c>: creates a fresh addressable session and returns its status. The agent routes
    /// later calls to it by echoing the returned <c>sessionId</c>; a call that omits <c>sessionId</c> keeps
    /// using the implicit default session, which this never becomes.
    /// </summary>
    private JsonObject RunSessionStart() {
        AgentSession session = _startSession();
        AgentRuntime.Audit("session-start", session.SessionId);
        return McpResult(AgentToolResult.Success(session.ToStatusJson(), session.SessionId));
    }

    /// <summary>
    /// <c>session-status</c>: returns the debug/replay snapshot (active target, last observation/action/
    /// error, artifact dir) of the session named by <paramref name="sessionId"/>, or the default session
    /// when it is omitted. An id that names no live session is refused with <c>unknown-session</c>.
    /// </summary>
    private JsonObject RunSessionStatus(string? sessionId) {
        if (!TryResolveRoutedSession(sessionId, out AgentSession? session)) {
            // Echo the rejected id, not the default (see the routing refusal in CallTool). Non-empty here:
            // an omitted/empty id resolves to the default session and never enters this branch.
            return ToolError(
                $"Unknown sessionId '{sessionId}'. It was never started or has ended. Call session-start, or omit sessionId for the default session.",
                "unknown-session", AgentErrorCategory.InvalidArgument, sessionId!);
        }
        AgentRuntime.Audit("session-status", session.SessionId);
        return McpResult(AgentToolResult.Success(session.ToStatusJson(), session.SessionId));
    }

    /// <summary>
    /// <c>session-end</c>: ends the session named by <paramref name="sessionId"/> (required). Idempotent;
    /// ending an id that is already gone returns <c>ended:false</c> rather than an error, so a retry is
    /// safe. The envelope is stamped with the ended id (metadata; consistent with <c>session-status</c>
    /// stamping its inspected session), so a client can correlate the result with the session it ended.
    /// </summary>
    private JsonObject RunSessionEnd(string? sessionId) {
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return ToolError("session-end requires a non-empty 'sessionId' argument (the session to end).", "bad-arguments", AgentErrorCategory.InvalidArgument, DefaultSessionId());
        }
        string id = sessionId.Trim();
        bool ended = _endSession(id);
        AgentRuntime.Audit("session-end", $"{id}; ended={ended}");
        JsonObject result = new() {
            ["sessionId"] = id,
            ["ended"] = ended,
        };
        if (!ended) {
            result["note"] = "No live session had that id (already ended or never started); nothing to do.";
        }
        return McpResult(AgentToolResult.Success(result, id));
    }

    /// <summary>
    /// Validates the <c>drag</c> tool's <c>from</c>/<c>to</c> arguments: each must be an object that is
    /// EITHER an element (<c>value</c> set) OR a full pixel (<c>x</c> and <c>y</c> both present). Returns
    /// a human-readable error, or <c>null</c> when both endpoints are well-formed.
    /// </summary>
    private static string? DragArgsError(JsonObject args) {
        foreach (string key in (string[])["from", "to"]) {
            if (args[key] is not JsonObject endpoint) {
                return $"drag '{key}' must be an object: an element {{ by?, value }} or a pixel {{ x, y }}.";
            }
            bool hasElement = !string.IsNullOrEmpty(Str(endpoint, "value"));
            bool hasX = endpoint["x"] is JsonValue vx && vx.TryGetValue(out int _);
            bool hasY = endpoint["y"] is JsonValue vy && vy.TryGetValue(out int _);
            if (!hasElement && !(hasX && hasY)) {
                return $"drag '{key}' needs an element 'value' or both 'x' and 'y' pixel coordinates.";
            }
        }
        return null;
    }

    /// <summary>
    /// Validates the <c>click</c> tool's <c>at</c> argument: it must be an object that is EITHER an element
    /// (<c>value</c> set) OR a full pixel (<c>x</c> and <c>y</c> both present). Returns a human-readable
    /// error, or <c>null</c> when the endpoint is well-formed. Mirrors <see cref="DragArgsError"/>.
    /// </summary>
    private static string? ClickArgsError(JsonObject args) {
        if (args["at"] is not JsonObject at) {
            return "click 'at' must be an object: an element { by?, value } or a pixel { x, y }.";
        }
        bool hasElement = !string.IsNullOrEmpty(Str(at, "value"));
        bool hasX = at["x"] is JsonValue vx && vx.TryGetValue(out int _);
        bool hasY = at["y"] is JsonValue vy && vy.TryGetValue(out int _);
        if (!hasElement && !(hasX && hasY)) {
            return "click 'at' needs an element 'value' or both 'x' and 'y' pixel coordinates.";
        }
        return null;
    }

    /// <summary>
    /// Validates the <c>focus</c> tool's optional <c>at</c> argument (#272 CR). Unlike <c>click</c>, focus
    /// allows NO endpoint (a bare window focus). But a PRESENT endpoint must be well-formed: an element
    /// (<c>value</c> set) OR a full pixel (<c>x</c> and <c>y</c> both present AND integer). A partial pixel
    /// (`{ x: 400 }`) or a non-integer coordinate would otherwise default the missing field to 0 and
    /// synthesize a real click at a bogus point. Returns a human-readable error, or <c>null</c> when the
    /// arguments are acceptable. Mirrors <see cref="ClickArgsError"/>.
    /// </summary>
    private static string? FocusArgsError(JsonObject args) {
        if (!args.ContainsKey("at") || args["at"] is null) {
            return null; // no endpoint: a valid window-only focus (foreground + confirm).
        }
        if (args["at"] is not JsonObject at) {
            return "focus 'at' must be an object: an element { by?, value } or a pixel { x, y }; omit 'at' to focus just the window.";
        }
        bool hasElement = !string.IsNullOrEmpty(Str(at, "value"));
        if (hasElement) {
            return null;
        }
        bool mentionsPixel = at.ContainsKey("x") || at.ContainsKey("y");
        bool hasX = at["x"] is JsonValue vx && vx.TryGetValue(out int _);
        bool hasY = at["y"] is JsonValue vy && vy.TryGetValue(out int _);
        if (mentionsPixel && !(hasX && hasY)) {
            return "focus 'at' pixel endpoint needs both integer 'x' and 'y' (or an element 'value', or omit 'at' to focus just the window).";
        }
        if (!mentionsPixel) {
            return "focus 'at' must be an element { by?, value } or a pixel { x, y }; omit 'at' to focus just the window.";
        }
        return null;
    }

    // Internal so tests can exercise the unknown-tool refusal for a name that passed the
    // tools/call gate but has no argument mapping (InternalsVisibleTo). See #201.
    internal JsonObject RunAgentCommand(string name, JsonObject args, AgentSession session) {
        // #201: if a name passes the gate but has no command mapping, refuse with a structured error
        //; the old default arm silently mapped unknown names onto InvokeCommand (an ACTUATION) with
        // garbage selector args. Since #205 the gate and the mapping are the SAME ToolCatalog, so
        // this can only trip for a caller that bypassed the gate (tests exercise it directly).
        if (BuildCommand(name, args) is not { } cmd) {
            AgentRuntime.Audit(name, "BLOCKED; tool has no argument mapping; refusing to run it as another command");
            return ToolError($"Unknown tool: {name}", "unknown-tool", AgentErrorCategory.Internal, session.SessionId);
        }

        // Honor the per-command Enabled flag; the documented second security gate. The MCP tool only
        // runs if the corresponding command in the loaded table is enabled (built-ins ship disabled;
        // the operator opts in per-command via mcec.commands). Fail closed if the table/command is missing.
        if ((_invoker()?[name] as Command)?.Enabled != true) {
            AgentRuntime.Audit(name, "BLOCKED; command is disabled in mcec.commands");
            // #307: the recovery is asking the OPERATOR, never editing config files; an agent must
            // not widen its own permissions, and in a provisioned session the config is off-limits.
            return ToolError(
                $"The '{name}' command is disabled. Call request-command-access with the command name and a one-line reason; " +
                "the operator will be asked on screen. Do not edit mcec.commands or any config file.",
                "command-disabled", AgentErrorCategory.Internal, session.SessionId);
        }

        CapturingReply reply = new();
        cmd.Reply = reply;
        cmd.Enabled = true;
        cmd.Cmd = name;

        AgentRuntime.Audit(name, args.ToJsonString(AgentJson.Options));

        // The routed session (#86) carries sessionId onto this result and remembers the target/
        // observation/action/error. Snapshot the prior observation now, before this call records its own,
        // so a failure can carry the last good state into error.lastObservation.
        JsonObject? priorObservation = session.LastObservation;
        session.RecordAction(name);

        // Dispatch under the #113 concurrency contract. `invoke` can activate a control that opens a
        // MODAL dialog (About, Settings, message/file dialogs); the UIA Invoke runs the click handler
        // synchronously, so the call would block for the dialog's whole lifetime and; if it held a lock
        //; deadlock every later tool call (the agent couldn't even query or dismiss the dialog it just
        // opened, #105). So run `invoke` on a worker and, if it hasn't returned within a short grace,
        // report "modal pending" and return; the worker finishes when the dialog closes. `drag`
        // synthesizes physical input and serializes on AgentRuntime.InputGate; the SAME gate the
        // CommandInvoker's dispatcher thread holds around every queued command's Execute (#195), so a
        // drag gesture can never interleave with queue-driven input from the legacy TCP/serial
        // pipeline or send_command (both are producers into that one dispatcher-drained queue).
        // Observation (query/capture/find/wait-for) and `record` (its own bounded background thread)
        // run UNLOCKED, so a long/blocking read never stalls another tool call.
        if (name == "invoke") {
            if (!TryRunInvokeWithModalGrace(cmd)) {
                AgentRuntime.Audit(name, "dispatched; a modal dialog appears to be open; returning without blocking");
                PublishOverlay(name, args, CommandOutcome.Pending, null, session.SessionId);
                return McpResult(AgentToolResult.Success(InvokeModalPendingResult(), session.SessionId));
            }
        }
        else if (SerializesOnInputLock(name)) {
            lock (AgentRuntime.InputGate) {
                cmd.Execute();
            }
        }
        else {
            cmd.Execute();
        }

        // Consume the CommandResult OBJECT the command handed to the CapturingReply (#206); no
        // serialize → JsonNode.Parse round-trip of our own output (which used to materialize a
        // capture's base64 PNG three to four times), and no "non-JSON output is success" fallback:
        // an agent command that produced no typed result is a structured internal failure.
        if (reply.Result is not { } commandResult) {
            AgentError noOutput = new("no-output", AgentErrorCategory.Internal,
                $"The '{name}' command produced no structured result.", priorObservation);
            session.RecordError(noOutput.ToJsonObject());
            return McpResult(AgentToolResult.Failure(noOutput, session.SessionId));
        }

        AgentToolResult env = AgentToolResult.FromCommandResult(commandResult, name, session.SessionId, priorObservation);

        // For capture, additionally surface the PNG as an MCP image content block so image-aware clients
        // render it. The base64 stays in the envelope's result so text-only agents (which do not consume
        // MCP image blocks) still get the bytes, as the result contract requires.
        JsonObject? image = name == "capture" && env.Ok ? CaptureContent.TryBuildImageBlock(env.Result) : null;

        // Record this call's outcome so the next call's sessionId/lastObservation reflect it. Every
        // observation tool (wait-for included) records its observation + the resolved window as the target.
        session.RecordToolOutcome(name, env);

        PublishOverlay(name, args, env.Ok ? CommandOutcome.Ok : CommandOutcome.Failed, env.Error?.CategoryWire, session.SessionId);
        return McpResult(env, image);
    }

    /// <summary>
    /// Publishes a command-event for the on-screen overlay (#119). Best-effort and decoupled; the hub
    /// swallows subscriber faults; so it can never affect the tool result. The overlay window (and its
    /// <c>CommandOverlayEnabled</c> gate) lives on the subscriber side.
    /// </summary>
    private static void PublishOverlay(string name, JsonObject args, CommandOutcome outcome, string? detail, string? sessionId) =>
        CommandEventHub.Publish(new CommandEvent(name, CommandTersifier.ForAgentTool(name, args, outcome, detail), outcome, sessionId));

    private static JsonObject InvokeModalPendingResult() => new() {
        ["invoked"] = true,
        ["modalPending"] = true,
        ["note"] = "The invoke opened a modal dialog (or a long-running action); it completes when the " +
            "dialog closes. Query or capture the new window to read or dismiss it.",
    };

    /// <summary>
    /// Runs an <c>invoke</c> on a background worker and waits up to <see cref="InvokeModalGraceMs"/> ms.
    /// Returns true if it finished (its result is in the command's reply); false if it is still running,
    /// which means the invoked control opened a modal dialog. We deliberately do NOT hold
    /// <see cref="AgentRuntime.InputGate"/> for an invoke: a modal opener must not block the later
    /// query/capture/invoke calls the agent needs to read or dismiss the very dialog it opened. The
    /// worker ends on its own when the dialog closes.
    /// </summary>
    private static bool TryRunInvokeWithModalGrace(Command cmd) {
        Thread worker = new(() => {
            try {
                cmd.Execute();
            }
            catch (Exception e) {
                Logger.Instance.Log4.Error($"AgentServer: invoke worker failed: {e.Message}");
            }
        }) {
            IsBackground = true,
            Name = "mcec-agent-invoke",
        };
        worker.Start();
        return worker.Join(InvokeModalGraceMs);
    }

    private JsonObject RunSendCommand(JsonObject args, AgentSession session) {
        string command = args["command"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(command)) {
            return ToolError("send_command requires a non-empty 'command' argument.", "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }

        CommandInvoker? invoker = _invoker();
        if (invoker is null) {
            return ToolError("Command engine is not available.", "engine-unavailable", AgentErrorCategory.Internal, session.SessionId);
        }

        // #307: fail fast (and honestly) on a DISABLED command. Without this the clone enqueued fine
        // and the dispatcher silently skipped it (the per-command gate), so the call reported "ok"
        // while nothing executed; the agent had no command-disabled signal to recover from.
        if (DisabledSendCommandKey(invoker, command) is { } disabledKey) {
            AgentRuntime.Audit("send_command", $"BLOCKED; the '{disabledKey}' command is disabled in mcec.commands");
            return ToolError(
                $"The '{disabledKey}' command is disabled; nothing was executed. Call request-command-access with the " +
                "command name and a one-line reason; the operator will be asked on screen. Do not edit mcec.commands or any config file.",
                "command-disabled", AgentErrorCategory.Internal, session.SessionId);
        }

        AgentRuntime.Audit("send_command", command);

        // #195: enqueue-and-await. send_command is a PRODUCER into the invoker's single
        // dispatcher-drained queue (the same queue the legacy TCP/serial pipeline feeds); the
        // dispatcher thread executes the command under AgentRuntime.InputGate (the #113
        // no-interleaving invariant) and the completion task fires only after it ran; so reading
        // reply.Captured below no longer races the execution. A command that never entered the queue
        // (unknown name, or dropped whole by the #154 bounds) is a failure, not the old always-"ok"
        // (the richer taxonomy stays #206; these two codes are the minimum honest signal).
        CapturingReply reply = new();
        CommandEnqueueResult enqueued = invoker.TryEnqueueWithCompletion(reply, command, out Task<bool>? completion);

        session.RecordAction("send_command");

        if (enqueued == CommandEnqueueResult.UnknownCommand) {
            return ToolError(
                $"Unknown command: '{command}' is not in the loaded command table. Nothing was executed.",
                "unknown-command", AgentErrorCategory.Internal, session.SessionId);
        }
        if (enqueued != CommandEnqueueResult.Enqueued || completion is null) {
            return ToolError(
                $"The command '{command}' was dropped whole and never executed: it exceeds the queue bounds " +
                "(over the embedded-expansion limit, or the execute queue is full) or the command engine is shutting down. " +
                "Send less at once and let the queue drain before retrying.",
                "command-dropped", AgentErrorCategory.Internal, session.SessionId);
        }

        if (!completion.Wait(SendCommandCompletionTimeoutMs)) {
            return ToolError(
                $"send_command timed out after {SendCommandCompletionTimeoutMs / 1000}s waiting for '{command}' to execute. " +
                "The command queue is still draining it (a long macro, pause, or a hung command); it was not cancelled.",
                "send-command-timeout", AgentErrorCategory.Internal, session.SessionId);
        }
        if (!completion.Result) {
            return ToolError(
                "The queue was dropped before this command finished (emergency stop engaged or the command engine shut down). " +
                "It may have PARTIALLY executed; verify the desktop state with query/capture before assuming nothing ran.",
                "emergency-stopped", AgentErrorCategory.Internal, session.SessionId);
        }

        // The legacy command path returns opaque text, not a CommandResult; carry it forward as the
        // success payload. (An agent command routed through send_command hands over a typed result
        // instead; CapturingReply.Captured lazily serializes it, so the output is unchanged.)
        string captured = reply.Captured.Trim();
        JsonObject result = new() { ["output"] = string.IsNullOrEmpty(captured) ? "ok" : captured };
        CommandEventHub.Publish(new CommandEvent("send_command", CommandTersifier.ForRawCommand(command), CommandOutcome.Ok, session.SessionId));
        return McpResult(AgentToolResult.Success(result, session.SessionId));
    }

    /// <summary>
    /// Provisions a fresh, disposable, isolated MCEC instance (#138) and returns the handoff. Gated behind
    /// the operator's <see cref="AppSettings.AllowSessionProvisioning"/> opt-in; the one thing that cannot
    /// be self-served; and never touches the installed config. Also reaps stale session dirs opportunistically.
    /// </summary>
    private JsonObject RunProvisionSession(JsonObject args, AgentSession session) {
        if (_settings()?.AllowSessionProvisioning != true) {
            AgentRuntime.Audit("provision-session", "BLOCKED; session provisioning is not authorized");
            return ToolError(
                "Session provisioning is not authorized. The operator must enable AllowSessionProvisioning to opt in; it cannot be self-served.",
                "provisioning-not-authorized", AgentErrorCategory.Internal, session.SessionId);
        }

        bool mcpServer = args["mcpServer"] is not JsonValue mv || !mv.TryGetValue(out bool m) || m; // default true
        List<string>? commands = StrArray(args["commands"] as JsonArray);

        try {
            SessionProvisioner.ReapOrphans(TimeSpan.FromHours(SessionReapAgeHours));
            ProvisionedSession provisioned = SessionProvisioner.Provision(mcpServer, commands);
            return McpResult(AgentToolResult.Success(provisioned.ToJsonObject(), session.SessionId));
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: provision-session failed: {e.Message}");
            return ToolError($"Failed to provision a session: {e.Message}", "provisioning-failed", AgentErrorCategory.Internal, session.SessionId);
        }
    }

    /// <summary>
    /// Tears down a provisioned session directory by id (#138). Since #215 the caller must also
    /// present the session's <c>token</c>; the credential <c>provision-session</c> issued and wrote
    /// into the session's co-located config. end-session is reachable WITHOUT the provisioning
    /// authorization gate (teardown must always be possible), so before the token this tool let any
    /// MCP caller delete any session it could name; now only the token holder can.
    /// </summary>
    private static JsonObject RunEndSession(JsonObject args, AgentSession session) {
        string? sessionId = Str(args, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return ToolError("end-session requires a non-empty 'sessionId' argument.", "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }
        string? token = Str(args, "token");
        if (string.IsNullOrWhiteSpace(token)) {
            return ToolError(
                "end-session requires the session 'token' returned by provision-session (the teardown credential).",
                "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }
        switch (SessionProvisioner.ValidateTeardownToken(sessionId, token)) {
            case SessionTokenValidation.TokenMismatch:
                AgentRuntime.Audit("end-session", $"REJECTED; token does not match session '{sessionId.Trim()}'");
                return ToolError(
                    "The token does not match this session (or the session's config could not be verified). " +
                    "Use the exact token provision-session returned; a session you did not provision is not yours to tear down. " +
                    "Orphaned sessions are reaped automatically.",
                    "session-token-invalid", AgentErrorCategory.Internal, session.SessionId);
            case SessionTokenValidation.InvalidId:
            case SessionTokenValidation.SessionGone:
            case SessionTokenValidation.Valid:
            default:
                break; // Teardown itself re-validates the id shape and handles a gone directory idempotently.
        }
        bool removed = SessionProvisioner.Teardown(sessionId);
        JsonObject result = new() {
            ["sessionId"] = sessionId,
            ["removed"] = removed,
        };
        if (!removed) {
            result["note"] = "The session directory could not be deleted; its mcec.exe may still be running. Stop it and retry, or MCEC will reap it on a later launch.";
        }
        return McpResult(AgentToolResult.Success(result, session.SessionId));
    }

    // -------------------------------------------------------------------------------------------
    // request-command-access (#307)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>request-command-access</c>: the agent asks the OPERATOR to enable disabled command(s); the
    /// legitimate mid-session acquisition path the <c>command-disabled</c> refusal points at (an agent
    /// must never widen its own permissions by editing config files). Shows MCEC's own consent dialog
    /// on the operator's desktop via <see cref="AgentConsent.Prompter"/> and blocks this MCP worker
    /// for the answer (bounded by the dialog's own timeout). Grants flip <see cref="Command.Enabled"/>
    /// on the LOADED table only; in-memory, this process only, never persisted; and every grant
    /// (including auto-grants under a standing "allow any" choice) is audited and narrated on the
    /// overlay. A deny is sticky for the process (anti-nag); a timeout is not (the operator did not
    /// decide). While the prompt is up this call holds <see cref="AgentRuntime.InputGate"/> so no
    /// queued or synthesized input can reach the dialog; only physical input can answer it.
    /// </summary>
    private JsonObject RunRequestCommandAccess(JsonObject args, AgentTransport transport, AgentSession session) {
        // Mirrors send_command's transport-sensitive gate (#153): over the network-facing HTTP floor
        // it requires the AgentCommandsEnabled opt-in; over local stdio it is available alongside the
        // documented send_command pass-through (whose per-command Enabled refusals are exactly what
        // this tool exists to recover from).
        if (transport == AgentTransport.Http && !AgentCommandsEnabled) {
            AgentRuntime.Audit("request-command-access", "BLOCKED; agent commands disabled; request-command-access over HTTP requires AgentCommandsEnabled");
            return ToolError(
                "request-command-access over HTTP requires AgentCommandsEnabled=true. Enable the agent surface to opt in, or drive it over the local stdio transport (mcec.exe --mcp).",
                "agent-commands-disabled", AgentErrorCategory.Internal, session.SessionId);
        }

        CommandInvoker? invoker = _invoker();
        if (invoker is null) {
            return ToolError("Command engine is not available.", "engine-unavailable", AgentErrorCategory.Internal, session.SessionId);
        }

        // STRICT parsing (#308 review): unlike StrArray (which tolerantly skips junk for
        // provision-session's optional list), a malformed entry here must refuse the WHOLE request;
        // silently dropping one would let the operator adjudicate a subset the agent never sees.
        List<string>? names = StrictCommandNames(args["commands"], out string? namesError);
        if (namesError is not null) {
            return ToolError(
                $"request-command-access 'commands' is malformed: {namesError} Every entry must be a non-empty command-name string; nothing was asked of the operator.",
                "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }
        if (names is null) {
            return ToolError(
                "request-command-access requires a non-empty 'commands' array (the command names to enable).",
                "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }
        if (names.Count > AgentConsent.MaxCommandsPerRequest) {
            return ToolError(
                $"request-command-access accepts at most {AgentConsent.MaxCommandsPerRequest} commands per request (got {names.Count}); ask for what this step needs.",
                "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }
        string reason = SanitizeReason(Str(args, "reason"));
        if (reason.Length == 0) {
            return ToolError(
                "request-command-access requires a non-empty 'reason'; the operator reads it to decide.",
                "bad-arguments", AgentErrorCategory.InvalidArgument, session.SessionId);
        }

        // Resolve every requested name against the loaded table (the same name/prefix rules the
        // invoker applies), refusing the WHOLE request on any unknown name; the operator must never
        // be asked to adjudicate a typo.
        Dictionary<string, Command> resolved = new(StringComparer.OrdinalIgnoreCase);
        List<string> unknown = [];
        foreach (string requested in names) {
            string? key = ResolveTableKey(invoker, requested);
            if (key is null || invoker[key] is not Command cmd) {
                unknown.Add(requested);
                continue;
            }
            resolved[key] = cmd;
        }
        if (unknown.Count > 0) {
            return ToolError(
                $"Unknown command(s): {string.Join(", ", unknown)}. They are not in the loaded command table; nothing was asked of the operator. Fix the names and retry.",
                "unknown-command", AgentErrorCategory.InvalidArgument, session.SessionId);
        }

        List<string> alreadyEnabled = [.. resolved.Where(p => p.Value.Enabled).Select(p => p.Key)];
        List<KeyValuePair<string, Command>> needed = [.. resolved.Where(p => !p.Value.Enabled)];
        if (needed.Count == 0) {
            AgentRuntime.Audit("request-command-access", $"no-op; already enabled: [{string.Join(",", alreadyEnabled)}]");
            return McpResult(AgentToolResult.Success(AccessResult([], alreadyEnabled, "requested"), session.SessionId));
        }

        // Sticky deny (#307): the operator already said no to one of these this process; do not
        // re-prompt (consent fatigue is how consent UIs get worn down into approval).
        List<string> denied = [.. needed.Select(p => p.Key).Where(AgentConsent.IsDenied)];
        if (denied.Count > 0) {
            AgentRuntime.Audit("request-command-access", $"BLOCKED; operator already denied: [{string.Join(",", denied)}]");
            return ToolError(
                $"The operator already denied: {string.Join(", ", denied)}. A deny is final for this instance; do not ask again. If you believe it is essential, tell the user why and let them enable it themselves.",
                "consent-denied", AgentErrorCategory.Internal, session.SessionId);
        }

        // Standing "allow any later requests" grant: auto-approve WITHOUT a prompt, but still one
        // audit line and one overlay narration per command; the operator sees every grant land.
        if (AgentConsent.AnyCommandGrantActive) {
            EnableGranted(needed, "auto-granted under the operator's allow-any choice", session.SessionId);
            return McpResult(AgentToolResult.Success(AccessResult([.. needed.Select(p => p.Key)], alreadyEnabled, "any"), session.SessionId));
        }

        Func<CommandAccessRequest, CommandAccessDecision?>? prompter = AgentConsent.Prompter;
        if (prompter is null) {
            AgentRuntime.Audit("request-command-access", "BLOCKED; no operator prompt surface is available (fail closed)");
            return ToolError(
                "No operator consent prompt can be shown here (no interactive operator surface). The request fails closed; ask the user to run the command-enable themselves.",
                "consent-unavailable", AgentErrorCategory.Internal, session.SessionId);
        }
        if (!AgentConsent.TryBeginPrompt()) {
            return ToolError(
                "Another request-command-access prompt is already on the operator's screen; one at a time. Wait for it to resolve, then retry.",
                "consent-pending", AgentErrorCategory.Internal, session.SessionId);
        }

        CommandAccessDecision? decision;
        try {
            CommandAccessRequest request = new() {
                Commands = [.. needed.Select(p => p.Key)],
                DisplayLines = [.. needed.Select(p => DescribeCommand(p.Key, p.Value))],
                Reason = reason,
            };
            AgentRuntime.Audit("request-command-access", $"asking the operator to enable [{string.Join(",", request.Commands)}]; agent's reason: {reason}");
            CommandEventHub.Publish(new CommandEvent("request-command-access",
                $"asking you: enable {string.Join(", ", request.Commands)}?", CommandOutcome.Pending, session.SessionId));

            // Hold the input gate for the prompt's whole lifetime (#307): the same gate every queued
            // command's Execute and every direct actuation takes, so synthesized input cannot land on
            // the dialog; only physical input can. A deliberate, documented exception to the
            // "leaf lock, never wait while holding it" rule; see AgentRuntime.InputGate.
            lock (AgentRuntime.InputGate) {
                decision = prompter(request);
            }
        }
        finally {
            AgentConsent.EndPrompt();
        }

        if (decision is null) {
            AgentRuntime.Audit("request-command-access", "BLOCKED; the operator prompt could not be shown (fail closed)");
            return ToolError(
                "The operator consent prompt could not be shown (no interactive desktop, or the operator surface is gone). The request fails closed.",
                "consent-unavailable", AgentErrorCategory.Internal, session.SessionId);
        }

        AgentConsent.RecordDecision(decision.Value, needed.Select(p => p.Key));
        return ApplyConsentDecision(decision.Value, needed, alreadyEnabled, session);
    }

    /// <summary>Turns the operator's decision into the tool result (grants applied, refusals shaped).</summary>
    private static JsonObject ApplyConsentDecision(
        CommandAccessDecision decision, List<KeyValuePair<string, Command>> needed, List<string> alreadyEnabled, AgentSession session) {
        switch (decision) {
            case CommandAccessDecision.AllowRequested:
            case CommandAccessDecision.AllowAny:
                EnableGranted(needed, decision == CommandAccessDecision.AllowAny
                    ? "granted by the operator (and any-command auto-grant armed)"
                    : "granted by the operator", session.SessionId);
                return McpResult(AgentToolResult.Success(
                    AccessResult([.. needed.Select(p => p.Key)], alreadyEnabled, decision == CommandAccessDecision.AllowAny ? "any" : "requested"),
                    session.SessionId));

            case CommandAccessDecision.TimedOut:
                // The panic hotkey engaging mid-prompt dismisses the dialog as TimedOut (not an
                // operator ANSWER, so never a sticky deny; #308 review). Report it as the standard
                // emergency-stopped signal so the agent stops entirely instead of re-asking.
                if (AgentRuntime.EmergencyStopped) {
                    AgentRuntime.Audit("request-command-access", $"DISMISSED by the emergency stop; not recorded as a deny for [{string.Join(",", needed.Select(p => p.Key))}]");
                    return ToolError(
                        "Emergency stop is engaged; the operator halted this session while your consent prompt was open (that is not a deny). All tool calls are refused until they re-arm. Stop and tell the user; do not retry.",
                        "emergency-stopped", AgentErrorCategory.Internal, session.SessionId);
                }
                AgentRuntime.Audit("request-command-access", $"TIMEOUT; the operator did not answer for [{string.Join(",", needed.Select(p => p.Key))}]");
                CommandEventHub.Publish(new CommandEvent("request-command-access", "consent request timed out (denied)", CommandOutcome.Failed, session.SessionId));
                return ToolError(
                    "The operator did not answer the consent prompt in time; the request is denied. They may be away; tell the user what you need and why, and retry only if they say so.",
                    "consent-timeout", AgentErrorCategory.Internal, session.SessionId);

            case CommandAccessDecision.Denied:
            default:
                AgentRuntime.Audit("request-command-access", $"DENIED by the operator: [{string.Join(",", needed.Select(p => p.Key))}]");
                CommandEventHub.Publish(new CommandEvent("request-command-access", $"you denied: {string.Join(", ", needed.Select(p => p.Key))}", CommandOutcome.Failed, session.SessionId));
                return ToolError(
                    $"The operator denied enabling: {string.Join(", ", needed.Select(p => p.Key))}. This deny is final for this instance; do not ask again for these commands.",
                    "consent-denied", AgentErrorCategory.Internal, session.SessionId);
        }
    }

    /// <summary>
    /// Applies a grant: flips <see cref="Command.Enabled"/> on each LOADED-table instance (the enqueue
    /// path clones from the table, and the per-command gates read the table, so future calls see it),
    /// with one audit line and one overlay narration per command so every grant is operator-visible.
    /// In-memory only; nothing is ever written back to mcec.commands.
    /// </summary>
    private static void EnableGranted(List<KeyValuePair<string, Command>> granted, string how, string sessionId) {
        foreach ((string key, Command cmd) in granted) {
            cmd.Enabled = true;
            // Recorded so persistence shields it: CommandInvoker.Save serializes a consent-granted
            // command as disabled, keeping the grant process-lifetime-only (#308 review).
            AgentConsent.RecordGrantedKey(key);
            AgentRuntime.Audit("request-command-access", $"ENABLED '{key}' ({how}); in-memory, this instance only");
            CommandEventHub.Publish(new CommandEvent("request-command-access", $"enabled {key} ({how})", CommandOutcome.Ok, sessionId));
        }
    }

    private static JsonObject AccessResult(List<string> granted, List<string> alreadyEnabled, string scope) {
        JsonObject result = new() {
            ["granted"] = new JsonArray([.. granted.Select(n => (JsonNode)n)]),
            ["alreadyEnabled"] = new JsonArray([.. alreadyEnabled.Select(n => (JsonNode)n)]),
            ["scope"] = scope,
        };
        if (scope == "any") {
            result["note"] = "The operator's allow-any grant is active for this instance; future request-command-access calls auto-approve (each grant is still audited).";
        }
        return result;
    }

    /// <summary>
    /// Resolves a requested command name to the loaded-table key the invoker will actually gate on,
    /// via <see cref="CommandInvoker.ResolveGateKey"/>; the ONE resolver Enqueue's rules and both
    /// consumers here share (#308 review), so a grant can never land on an entry (a full spelling
    /// like <c>mcec:exit</c>, or a single character that rides <c>chars:</c>) the gate never reads.
    /// </summary>
    private static string? ResolveTableKey(CommandInvoker invoker, string requested) =>
        invoker.ResolveGateKey(requested);

    /// <summary>
    /// The loaded-table key a raw <c>send_command</c> string resolves to when that command exists but
    /// is DISABLED; null when it would run, or is unknown (Enqueue reports that honestly already).
    /// </summary>
    private static string? DisabledSendCommandKey(CommandInvoker invoker, string command) =>
        invoker.ResolveGateKey(command) is { } key && invoker[key] is Command { Enabled: false } ? key : null;

    /// <summary>
    /// Parses the <c>request-command-access</c> 'commands' argument STRICTLY (#308 review): every
    /// entry must be a non-empty string, and any junk entry sets <paramref name="error"/> (naming the
    /// offender) so the whole request is refused rather than silently adjudicated on a subset the
    /// agent thinks was covered. Returns null (with no error) when the array is absent or empty.
    /// </summary>
    private static List<string>? StrictCommandNames(JsonNode? node, out string? error) {
        error = null;
        if (node is null) {
            return null;
        }
        if (node is not JsonArray array) {
            error = "'commands' must be an array of command-name strings.";
            return null;
        }
        List<string> names = [];
        for (int i = 0; i < array.Count; i++) {
            if (array[i] is not JsonValue v || !v.TryGetValue(out string? s)) {
                error = $"entry {i} ({array[i]?.ToJsonString(AgentJson.Options) ?? "null"}) is not a string.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(s)) {
                error = $"entry {i} is empty.";
                return null;
            }
            names.Add(s.Trim());
        }
        return names.Count > 0 ? names : null;
    }

    /// <summary>
    /// One consent-dialog display line for a command: its name plus what enabling it permits (the
    /// catalog tool's description when it is an agent tool, else its command type), so the operator
    /// judges the capability rather than the name.
    /// </summary>
    private static string DescribeCommand(string key, Command cmd) {
        if (ToolCatalog.TryGet(key, out ToolDescriptor descriptor)
            && descriptor.BuildSchema()["description"] is JsonValue v && v.TryGetValue(out string? desc)
            && !string.IsNullOrWhiteSpace(desc)) {
            int cut = desc.IndexOf(". ", StringComparison.Ordinal);
            return $"{key}: {(cut > 0 ? desc[..(cut + 1)] : desc)}";
        }
        return $"{key}: a raw MCEC command ({cmd.GetType().Name})";
    }

    /// <summary>
    /// Sanitizes the agent's stated reason for operator display: control characters, Unicode line/
    /// paragraph separators (U+2028/U+2029; Label honors them as line breaks), and format characters
    /// (Cf; bidi overrides like U+202E RLO that could visually reorder text out of the dialog's
    /// quoting; #308 review) all collapse to spaces so the reason stays ONE honest visual line;
    /// embedded double quotes soften so the dialog's own quoting stays unambiguous; length is capped.
    /// </summary>
    private static string SanitizeReason(string? reason) {
        if (string.IsNullOrWhiteSpace(reason)) {
            return "";
        }
        StringBuilder sb = new(Math.Min(reason.Length, AgentConsent.MaxReasonLength + 1));
        foreach (char c in reason) {
            UnicodeCategory category = char.GetUnicodeCategory(c);
            bool neutralized = char.IsControl(c)
                || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
            sb.Append(neutralized ? ' ' : c == '"' ? '\'' : c);
            if (sb.Length > AgentConsent.MaxReasonLength) {
                break;
            }
        }
        string clean = string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return clean.Length > AgentConsent.MaxReasonLength ? clean[..AgentConsent.MaxReasonLength] + "..." : clean;
    }

    /// <summary>Reads a JSON array of strings into a list, or null when the node is absent/empty.</summary>
    private static List<string>? StrArray(JsonArray? array) {
        if (array is null || array.Count == 0) {
            return null;
        }
        List<string> items = [];
        foreach (JsonNode? node in array) {
            if (node is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s)) {
                items.Add(s);
            }
        }
        return items.Count > 0 ? items : null;
    }

    /// <summary>
    /// Builds and populates an agent command instance from MCP tool arguments, dispatching through the
    /// tool's <see cref="ToolDescriptor.BuildCommand"/> in <see cref="ToolCatalog"/> (#205). Exhaustive
    /// over the agent tool names: an unknown name returns <c>null</c> (the caller refuses it as
    /// <c>unknown-tool</c>) rather than falling through to a default command (#201). Internal so
    /// tests can pin the mapping (InternalsVisibleTo).
    /// </summary>
    internal static Command? BuildCommand(string name, JsonObject args) =>
        ToolCatalog.TryGet(name, out ToolDescriptor descriptor)
            ? descriptor.BuildCommand(args)
            : null; // unknown name; the caller reports unknown-tool; never guess a command (#201)

    // -------------------------------------------------------------------------------------------
    // envelope helpers
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Wraps an <see cref="AgentToolResult"/> envelope in the MCP <c>tools/call</c> transport: the
    /// envelope rides in a text content block (preceded by an optional image block, e.g. capture's PNG),
    /// and MCP <c>isError</c> mirrors the envelope (<c>isError = !ok</c>) per the #101 contract.
    /// </summary>
    private static JsonObject McpResult(AgentToolResult env, JsonObject? imageContent = null) {
        JsonArray content = [];
        if (imageContent is not null) {
            content.Add(imageContent);
        }
        content.Add(TextContent(env.ToJson()));
        return new JsonObject {
            ["content"] = content,
            ["isError"] = !env.Ok,
        };
    }

    /// <summary>
    /// A tool-level failure (a security gate or a bad request) reported as the #101 envelope. Security/
    /// policy refusals the agent cannot recover from on its own map to the <c>internal</c> category;
    /// argument-validation refusals pass <see cref="AgentErrorCategory.InvalidArgument"/> (#191; the
    /// recovery is to fix the request, not report a bug). <paramref name="code"/> distinguishes the
    /// specific cause. <paramref name="sessionId"/> (the routed session, or the default when the call
    /// never resolved one) is attached so even a refused call tells the agent which session it belonged to.
    /// </summary>
    private static JsonObject ToolError(string message, string code, AgentErrorCategory category, string sessionId) =>
        McpResult(AgentToolResult.Failure(new AgentError(code, category, message), sessionId));

    private static JsonObject TextContent(string text) => new() {
        ["type"] = "text",
        ["text"] = text,
    };

    private static string AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    private static string? Str(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
