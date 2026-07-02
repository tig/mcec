// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
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
    private readonly Func<AgentSession> _session;

    /// <param name="settings">Accessor for the live settings (gates + provisioning opt-in).</param>
    /// <param name="invoker">Accessor for the loaded command table/engine.</param>
    /// <param name="session">Accessor for the ambient agent session (#86).</param>
    public AgentToolExecutor(Func<AppSettings?> settings, Func<CommandInvoker?> invoker, Func<AgentSession> session) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

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
                "emergency-stopped");
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
                    "agent-commands-disabled");
            }
            return RunSendCommand(args);
        }

        if (name == "provision-session") {
            return RunProvisionSession(args);
        }

        if (name == "end-session") {
            return RunEndSession(args);
        }

        // The gated agent tools are exactly the ToolCatalog membership (#205); no hand-synced whitelist.
        if (ToolCatalog.Contains(name)) {
            if (!AgentCommandsEnabled) {
                AgentRuntime.Audit(name, "BLOCKED; agent commands disabled");
                return ToolError("Agent commands are disabled. Set AgentCommandsEnabled=true to opt in.", "agent-commands-disabled");
            }
            // `drag`/`click` generate real mouse input from their endpoints, and a missing pixel field would
            // otherwise default to 0 and actuate at a bogus coordinate. Reject an ill-formed endpoint up
            // front rather than actuating it.
            if (name == "drag" && DragArgsError(args) is string dragError) {
                return ToolError(dragError, "bad-arguments", AgentErrorCategory.InvalidArgument);
            }
            if (name == "click" && ClickArgsError(args) is string clickError) {
                return ToolError(clickError, "bad-arguments", AgentErrorCategory.InvalidArgument);
            }
            return RunAgentCommand(name, args);
        }

        return ToolError($"Unknown tool: {name}", "unknown-tool");
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

    // Internal so tests can exercise the unknown-tool refusal for a name that passed the
    // tools/call gate but has no argument mapping (InternalsVisibleTo). See #201.
    internal JsonObject RunAgentCommand(string name, JsonObject args) {
        // #201: if a name passes the gate but has no command mapping, refuse with a structured error
        //; the old default arm silently mapped unknown names onto InvokeCommand (an ACTUATION) with
        // garbage selector args. Since #205 the gate and the mapping are the SAME ToolCatalog, so
        // this can only trip for a caller that bypassed the gate (tests exercise it directly).
        if (BuildCommand(name, args) is not Command cmd) {
            AgentRuntime.Audit(name, "BLOCKED; tool has no argument mapping; refusing to run it as another command");
            return ToolError($"Unknown tool: {name}", "unknown-tool");
        }

        // Honor the per-command Enabled flag; the documented second security gate. The MCP tool only
        // runs if the corresponding command in the loaded table is enabled (built-ins ship disabled;
        // the operator opts in per-command via mcec.commands). Fail closed if the table/command is missing.
        if ((_invoker()?[name] as Command)?.Enabled != true) {
            AgentRuntime.Audit(name, "BLOCKED; command is disabled in mcec.commands");
            return ToolError($"The '{name}' command is disabled. Enable it in mcec.commands (set Enabled=\"true\").", "command-disabled");
        }

        CapturingReply reply = new();
        cmd.Reply = reply;
        cmd.Enabled = true;
        cmd.Cmd = name;

        AgentRuntime.Audit(name, args.ToJsonString(AgentJson.Options));

        // The ambient session (#86) carries sessionId onto this result and remembers the target/
        // observation/action/error. Snapshot the prior observation now, before this call records its own,
        // so a failure can carry the last good state into error.lastObservation.
        AgentSession session = _session();
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
        if (reply.Result is not CommandResult commandResult) {
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

    private JsonObject RunSendCommand(JsonObject args) {
        string command = args["command"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(command)) {
            return ToolError("send_command requires a non-empty 'command' argument.", "bad-arguments", AgentErrorCategory.InvalidArgument);
        }

        CommandInvoker? invoker = _invoker();
        if (invoker is null) {
            return ToolError("Command engine is not available.", "engine-unavailable");
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

        AgentSession session = _session();
        session.RecordAction("send_command");

        if (enqueued == CommandEnqueueResult.UnknownCommand) {
            return ToolError(
                $"Unknown command: '{command}' is not in the loaded command table. Nothing was executed.",
                "unknown-command");
        }
        if (enqueued != CommandEnqueueResult.Enqueued || completion is null) {
            return ToolError(
                $"The command '{command}' was dropped whole and never executed: it exceeds the queue bounds " +
                "(over the embedded-expansion limit, or the execute queue is full) or the command engine is shutting down. " +
                "Send less at once and let the queue drain before retrying.",
                "command-dropped");
        }

        if (!completion.Wait(SendCommandCompletionTimeoutMs)) {
            return ToolError(
                $"send_command timed out after {SendCommandCompletionTimeoutMs / 1000}s waiting for '{command}' to execute. " +
                "The command queue is still draining it (a long macro, pause, or a hung command); it was not cancelled.",
                "send-command-timeout");
        }
        if (!completion.Result) {
            return ToolError(
                "The queue was dropped before this command finished (emergency stop engaged or the command engine shut down). " +
                "It may have PARTIALLY executed; verify the desktop state with query/capture before assuming nothing ran.",
                "emergency-stopped");
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
    private JsonObject RunProvisionSession(JsonObject args) {
        if (_settings()?.AllowSessionProvisioning != true) {
            AgentRuntime.Audit("provision-session", "BLOCKED; session provisioning is not authorized");
            return ToolError(
                "Session provisioning is not authorized. The operator must enable AllowSessionProvisioning to opt in; it cannot be self-served.",
                "provisioning-not-authorized");
        }

        bool mcpServer = args["mcpServer"] is not JsonValue mv || !mv.TryGetValue(out bool m) || m; // default true
        List<string>? commands = StrArray(args["commands"] as JsonArray);

        try {
            SessionProvisioner.ReapOrphans(TimeSpan.FromHours(SessionReapAgeHours));
            ProvisionedSession session = SessionProvisioner.Provision(mcpServer, commands);
            return McpResult(AgentToolResult.Success(session.ToJsonObject(), _session().SessionId));
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: provision-session failed: {e.Message}");
            return ToolError($"Failed to provision a session: {e.Message}", "provisioning-failed");
        }
    }

    /// <summary>
    /// Tears down a provisioned session directory by id (#138). Since #215 the caller must also
    /// present the session's <c>token</c>; the credential <c>provision-session</c> issued and wrote
    /// into the session's co-located config. end-session is reachable WITHOUT the provisioning
    /// authorization gate (teardown must always be possible), so before the token this tool let any
    /// MCP caller delete any session it could name; now only the token holder can.
    /// </summary>
    private JsonObject RunEndSession(JsonObject args) {
        string? sessionId = Str(args, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return ToolError("end-session requires a non-empty 'sessionId' argument.", "bad-arguments", AgentErrorCategory.InvalidArgument);
        }
        string? token = Str(args, "token");
        if (string.IsNullOrWhiteSpace(token)) {
            return ToolError(
                "end-session requires the session 'token' returned by provision-session (the teardown credential).",
                "bad-arguments", AgentErrorCategory.InvalidArgument);
        }
        switch (SessionProvisioner.ValidateTeardownToken(sessionId, token)) {
            case SessionTokenValidation.TokenMismatch:
                AgentRuntime.Audit("end-session", $"REJECTED; token does not match session '{sessionId.Trim()}'");
                return ToolError(
                    "The token does not match this session (or the session's config could not be verified). " +
                    "Use the exact token provision-session returned; a session you did not provision is not yours to tear down. " +
                    "Orphaned sessions are reaped automatically.",
                    "session-token-invalid");
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
        return McpResult(AgentToolResult.Success(result, _session().SessionId));
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
    /// policy refusals the agent cannot recover from on its own map to the default <c>internal</c>
    /// category; argument-validation refusals pass <see cref="AgentErrorCategory.InvalidArgument"/>
    /// (#191; the recovery is to fix the request, not report a bug). <paramref name="code"/>
    /// distinguishes the specific cause. The ambient session's id is attached so even a refused call
    /// tells the agent which session it belonged to.
    /// </summary>
    private JsonObject ToolError(string message, string code = "internal-error", AgentErrorCategory category = AgentErrorCategory.Internal) =>
        McpResult(AgentToolResult.Failure(new AgentError(code, category, message), _session().SessionId));

    private static JsonObject TextContent(string text) => new() {
        ["type"] = "text",
        ["text"] = text,
    };

    private static string AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    private static string? Str(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
