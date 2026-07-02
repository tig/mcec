// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// MCEC 3.0's agent front door: a self-contained Model Context Protocol (MCP) server, hand-rolled as
/// JSON-RPC 2.0 over two transports — stdio (for an MCP client that launches <c>mcec.exe --mcp</c>)
/// and a localhost HTTP/JSON floor (POST a JSON-RPC request to <c>/mcp</c>). No external SDK or
/// Python/Node runtime: the same self-contained native binary, with MCP/HTTP as just one more
/// transport over the existing command core.
///
/// SECURITY: the gated agent tools (the <see cref="ToolCatalog"/> set — capture, query, displays,
/// find, wait-for, invoke, drag, click, record, launch) only run when
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> is true; otherwise a tool call is reported as an
/// error. The HTTP listener binds to <see cref="AppSettings.McpBindAddress"/> (127.0.0.1 by default).
/// A loopback bind is canonicalized before it reaches <see cref="HttpListener"/> (#152, see
/// <see cref="TryGetLoopbackPrefixHost"/>) so obfuscated loopback spellings can't slip a wildcard
/// binding past validation; a non-loopback bind is a deliberate off-box exposure and is allowed only
/// when <see cref="AppSettings.McpAuthToken"/> is set (#143) — otherwise <see cref="StartHttp"/> refuses
/// to start with a loud error. Every tool call is loudly audit-logged.
/// </summary>
public static class AgentServer {
    public const string ProtocolVersion = "2025-06-18";

    /// <summary>
    /// Largest HTTP request body the /mcp endpoint accepts, in bytes (#151). Real JSON-RPC requests
    /// are a few KB; 1 MB leaves generous headroom while making a memory-exhaustion POST impossible
    /// (the old unbounded read buffered the whole body, then JsonNode.Parse built a second copy).
    /// Oversized requests are refused with HTTP 413.
    /// </summary>
    public const long MaxHttpBodyBytes = 1024 * 1024;

    /// <summary>
    /// Upper bound on HTTP requests served concurrently (#151). Each accepted request runs on its own
    /// worker task (#113); without a bound, a request flood spawns unbounded tasks each holding a
    /// buffered body. Legitimate agent traffic is a handful of in-flight calls; past this cap the
    /// server answers 503 instead of queueing.
    /// </summary>
    public const int MaxConcurrentHttpRequests = 16;

    private static readonly SemaphoreSlim HttpWorkerSlots = new(MaxConcurrentHttpRequests, MaxConcurrentHttpRequests);

    /// <summary>
    /// Test seam: when set, <see cref="HandleHttp"/> dispatches through this instead of
    /// <see cref="Dispatch"/> (mirrors the dispatch delegate <see cref="RunStdioLoop"/> takes).
    /// Production leaves it null.
    /// </summary>
    internal static volatile Func<JsonObject, JsonObject?>? HttpDispatchOverride;

    private static readonly object HttpLock = new();
    private static HttpListener? _listener;
    private static Thread? _httpThread;

    /// <summary>
    /// Whether <paramref name="tool"/> serializes on the shared input gate
    /// (<see cref="AgentRuntime.InputGate"/> — the #113 contract). Global-input actuation
    /// (<c>drag</c>, <c>send_command</c>) does — it synthesizes physical mouse/keyboard, one shared
    /// stream that concurrent requests must not interleave. <c>drag</c> actuates directly under the
    /// gate on its MCP worker (its <see cref="ToolDescriptor.SerializesOnInput"/> flag);
    /// <c>send_command</c> is a meta-tool outside the <see cref="ToolCatalog"/> and serializes
    /// INDIRECTLY (#195): it enqueues into the <see cref="CommandInvoker"/>, whose single dispatcher
    /// thread holds the gate around each queued command's Execute — so it is special-cased here.
    /// Observation (<c>query</c>/<c>capture</c>/<c>find</c>/<c>wait-for</c>/<c>record</c>) does not
    /// (it runs concurrently); <c>invoke</c> is UIA-pattern actuation dispatched on a worker with the
    /// modal grace, not under this lock (#105).
    /// </summary>
    public static bool SerializesOnInputLock(string tool) =>
        tool == "send_command" || (ToolCatalog.TryGet(tool, out ToolDescriptor descriptor) && descriptor.SerializesOnInput);

    // -------------------------------------------------------------------------------------------
    // JSON-RPC dispatch (shared by stdio + HTTP)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Dispatches a single JSON-RPC request object and returns the response object, or null when the
    /// request is a notification (no <c>id</c>) and therefore takes no response. <paramref name="transport"/>
    /// defaults to <see cref="AgentTransport.Stdio"/> — the local, opt-in-preserving path; the HTTP floor
    /// passes <see cref="AgentTransport.Http"/> so <c>send_command</c> honors the network gate (#153).
    /// </summary>
    public static JsonObject? Dispatch(JsonObject request, AgentTransport transport = AgentTransport.Stdio) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        JsonNode? idNode = request["id"];
        string method = AsString(request["method"]);
        JsonObject? prms = request["params"] as JsonObject;

        // Notifications (no id) get no response.
        bool isNotification = idNode is null;

        switch (method) {
            case "initialize":
                return Result(idNode, BuildInitializeResult());

            case "notifications/initialized":
            case "notifications/cancelled":
                return null;

            case "ping":
                return Result(idNode, new JsonObject());

            case "tools/list":
                return Result(idNode, new JsonObject { ["tools"] = BuildToolsList() });

            case "tools/call":
                JsonObject callResult = CallTool(prms, transport);
                return Result(idNode, callResult);

            default:
                if (isNotification) {
                    return null;
                }
                return Error(idNode, -32601, $"Method not found: {method}");
        }
    }

    private static JsonObject BuildInitializeResult() => new() {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["serverInfo"] = new JsonObject {
            ["name"] = "MCEC",
            ["version"] = System.Windows.Forms.Application.ProductVersion,
        },
        // Built-in agent guidance: surfaced to the model by the MCP client so it knows how to drive
        // MCEC effectively (the observe -> target -> act loop) and understands the security model.
        ["instructions"] = Instructions,
    };

    /// <summary>
    /// Built-in guidance handed to an agent at connect time (the MCP client shows this to the model). It
    /// is authored in <c>src/Agent/AgentInstructions.md</c> — the single source of truth — and embedded
    /// into the exe at build time; this loads it once, collapsing each blank-line-separated paragraph to
    /// a single line (the historical connect-time format).
    /// </summary>
    public static string Instructions => LazyInstructions.Value;

    private static readonly Lazy<string> LazyInstructions = new(LoadInstructions);

    private static string LoadInstructions() {
        using Stream? stream = typeof(AgentServer).Assembly.GetManifestResourceStream("MCEControl.AgentInstructions.md");
        if (stream is null) {
            throw new InvalidOperationException(
                "Embedded resource 'MCEControl.AgentInstructions.md' not found — check the <EmbeddedResource> item in MCEControl.csproj.");
        }
        using StreamReader reader = new(stream);
        string raw = reader.ReadToEnd().Replace("\r\n", "\n");
        // Authored wrapped for readability; the model receives one line per blank-line-separated paragraph.
        string[] paragraphs = raw.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < paragraphs.Length; i++) {
            string[] lines = paragraphs[i].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < lines.Length; j++) {
                lines[j] = lines[j].Trim();
            }
            paragraphs[i] = string.Join(" ", lines);
        }
        return string.Join("\n", paragraphs);
    }

    // -------------------------------------------------------------------------------------------
    // Tool catalog
    // -------------------------------------------------------------------------------------------

    private static JsonArray BuildToolsList() {
        JsonArray tools = [];

        // The gated agent tools — one descriptor per tool, schema included — live in ToolCatalog (#205).
        foreach (ToolDescriptor descriptor in ToolCatalog.All) {
            tools.Add(descriptor.BuildSchema());
        }

        // META-TOOLS: deliberately NOT in the catalog, because they do not map 1:1 onto a Command in
        // the loaded table and have their own gating. send_command is the raw pass-through into the
        // CommandInvoker queue (transport-sensitive gate, #153); provision-session/end-session are the
        // isolated-session lifecycle (#138, gated by the operator's AllowSessionProvisioning). They are
        // special-cased here and in CallTool, right next to the catalog dispatch.
        tools.Add(ToolCatalog.Tool("send_command",
            "Send any raw MCEC command string to the existing command core (e.g. actuation commands).",
            new JsonObject { ["command"] = ToolCatalog.PropSchema("string", "The MCEC command string to enqueue") },
            ["command"]));

        // Isolated session provisioning (#138). Requires the operator to have opted in
        // (AllowSessionProvisioning); it never mutates the installed config.
        JsonObject provisionProps = new() {
            ["mcpServer"] = ToolCatalog.PropSchema("boolean", "Enable the provisioned instance's localhost MCP/HTTP server (default true)"),
            ["commands"] = new JsonObject {
                ["type"] = "array",
                ["description"] = "Command names to enable in the session (default: the agent observation/action set)",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        };
        tools.Add(ToolCatalog.Tool("provision-session",
            "Get a fresh, disposable, isolated MCEC instance to run from instead of enabling the installed one. Returns a directory containing mcec.exe + an agent-ready co-located config (agent commands enabled ONLY inside the copy), plus how to launch/connect and the sessionId to tear it down. Requires the operator to have enabled AllowSessionProvisioning; the installed config is never touched. Call end-session (or delete the directory) when finished.",
            provisionProps, []));

        tools.Add(ToolCatalog.Tool("end-session",
            "Tear down a provisioned session (from provision-session) by deleting its directory. Stop the session's mcec.exe first, or its files stay locked. MCEC also reaps stale session dirs on launch.",
            new JsonObject { ["sessionId"] = ToolCatalog.PropSchema("string", "The sessionId returned by provision-session") },
            ["sessionId"]));

        return tools;
    }

    // -------------------------------------------------------------------------------------------
    // tools/call
    // -------------------------------------------------------------------------------------------

    private static JsonObject CallTool(JsonObject? prms, AgentTransport transport) {
        string name = AsString(prms?["name"]);
        JsonObject args = prms?["arguments"] as JsonObject ?? [];

        // Emergency stop (#135): once the operator engages the panic hotkey, EVERY tool call is refused
        // (actuation, observation, and raw send_command alike) until they explicitly re-arm — the human
        // override latches and is checked before anything else. A distinct error code tells the agent to
        // stop and surface it, not retry.
        if (AgentRuntime.EmergencyStopped) {
            AgentRuntime.Audit(name, "BLOCKED — emergency stop engaged; operator must re-arm");
            return ToolError(
                "Emergency stop is engaged — the operator halted this session. All tool calls are refused until they re-arm. Stop and tell the user; do not retry.",
                "emergency-stopped");
        }

        if (name == "send_command") {
            // #153: send_command is a raw command-injection surface. Over the network-facing HTTP floor it
            // must NOT be reachable unless the operator opted into the agent surface — otherwise enabling
            // McpServerEnabled alone (with AgentCommandsEnabled=false) leaves a CSRF/DNS-rebinding-reachable
            // (#143) raw pass-through. So over HTTP it honors the SAME AgentCommandsEnabled gate as every
            // other tool. Over local stdio (the operator launched mcec.exe --mcp — no CSRF surface) the
            // documented raw pass-through stays available; the per-command Enabled table still applies below.
            if (transport == AgentTransport.Http && !AgentRuntime.AgentCommandsEnabled) {
                AgentRuntime.Audit("send_command", "BLOCKED — agent commands disabled; send_command over HTTP requires AgentCommandsEnabled");
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

        // The gated agent tools are exactly the ToolCatalog membership (#205) — no hand-synced whitelist.
        if (ToolCatalog.Contains(name)) {
            if (!AgentRuntime.AgentCommandsEnabled) {
                AgentRuntime.Audit(name, "BLOCKED — agent commands disabled");
                return ToolError("Agent commands are disabled. Set AgentCommandsEnabled=true to opt in.", "agent-commands-disabled");
            }
            // `drag`/`click` generate real mouse input from their endpoints, and a missing pixel field would
            // otherwise default to 0 and actuate at a bogus coordinate. Reject an ill-formed endpoint up
            // front rather than actuating it.
            if (name == "drag" && DragArgsError(args) is string dragError) {
                return ToolError(dragError, "bad-arguments");
            }
            if (name == "click" && ClickArgsError(args) is string clickError) {
                return ToolError(clickError, "bad-arguments");
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
    internal static JsonObject RunAgentCommand(string name, JsonObject args) {
        // #201: if a name passes the gate but has no command mapping, refuse with a structured error
        // — the old default arm silently mapped unknown names onto InvokeCommand (an ACTUATION) with
        // garbage selector args. Since #205 the gate and the mapping are the SAME ToolCatalog, so
        // this can only trip for a caller that bypassed the gate (tests exercise it directly).
        if (BuildCommand(name, args) is not Command cmd) {
            AgentRuntime.Audit(name, "BLOCKED — tool has no argument mapping; refusing to run it as another command");
            return ToolError($"Unknown tool: {name}", "unknown-tool");
        }

        // Honor the per-command Enabled flag — the documented second security gate. The MCP tool only
        // runs if the corresponding command in the loaded table is enabled (built-ins ship disabled;
        // the operator opts in per-command via mcec.commands). Fail closed if the table/command is missing.
        if ((AgentRuntime.Invoker?[name] as Command)?.Enabled != true) {
            AgentRuntime.Audit(name, "BLOCKED — command is disabled in mcec.commands");
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
        AgentSession session = AgentRuntime.Session;
        JsonObject? priorObservation = session.LastObservation;
        session.RecordAction(name);

        // Dispatch under the #113 concurrency contract. `invoke` can activate a control that opens a
        // MODAL dialog (About, Settings, message/file dialogs); the UIA Invoke runs the click handler
        // synchronously, so the call would block for the dialog's whole lifetime and — if it held a lock
        // — deadlock every later tool call (the agent couldn't even query or dismiss the dialog it just
        // opened, #105). So run `invoke` on a worker and, if it hasn't returned within a short grace,
        // report "modal pending" and return; the worker finishes when the dialog closes. `drag`
        // synthesizes physical input and serializes on AgentRuntime.InputGate — the SAME gate the
        // CommandInvoker's dispatcher thread holds around every queued command's Execute (#195), so a
        // drag gesture can never interleave with queue-driven input from the legacy TCP/serial
        // pipeline or send_command (both are producers into that one dispatcher-drained queue).
        // Observation (query/capture/find/wait-for) and `record` (its own bounded background thread)
        // run UNLOCKED, so a long/blocking read never stalls another tool call.
        if (name == "invoke") {
            if (!TryRunInvokeWithModalGrace(cmd)) {
                AgentRuntime.Audit(name, "dispatched; a modal dialog appears to be open — returning without blocking");
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

        // Translate the legacy CommandResult the command wrote into its reply into the #101 envelope.
        string captured = reply.Captured.Trim();
        if (string.IsNullOrEmpty(captured)) {
            AgentError noOutput = new("no-output", AgentErrorCategory.Internal, $"The '{name}' command produced no output.", priorObservation);
            session.RecordError(noOutput.ToJsonObject());
            return McpResult(AgentToolResult.Failure(noOutput, session.SessionId));
        }

        if (TryParse(captured) is not JsonObject legacy) {
            // An agent command always emits CommandResult JSON; a non-JSON reply is unexpected. Carry the
            // raw text forward rather than fabricating a structured error.
            return McpResult(AgentToolResult.Success(new JsonObject { ["output"] = captured }, session.SessionId));
        }

        AgentToolResult env = AgentToolResult.FromLegacy(legacy, name, session.SessionId, priorObservation);

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
    /// Publishes a command-event for the on-screen overlay (#119). Best-effort and decoupled — the hub
    /// swallows subscriber faults — so it can never affect the tool result. The overlay window (and its
    /// <c>CommandOverlayEnabled</c> gate) lives on the subscriber side.
    /// </summary>
    private static void PublishOverlay(string name, JsonObject args, CommandOutcome outcome, string? detail, string? sessionId) =>
        CommandEventHub.Publish(new CommandEvent(name, CommandTersifier.ForAgentTool(name, args, outcome, detail), outcome, sessionId));

    // Grace period for an `invoke` to complete before we assume it opened a modal dialog and return.
    // Must stay above UiaService.InvokeFindTimeoutMs so an invoke's element lookup always resolves within
    // the grace — otherwise a missing element would be misreported as a pending modal (see #107).
    public const int InvokeModalGraceMs = 750;

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

    /// <summary>
    /// Upper bound a <c>send_command</c> call waits for its enqueued command tree to finish executing
    /// (#195). Bounded so one hung command (or a deep paced backlog ahead of it) can never wedge the
    /// MCP dispatch worker forever. 30s covers any sane paced macro (a full 50-command tree at the
    /// default 0ms–250ms pacing); a tree that legitimately runs longer keeps executing on the
    /// dispatcher — only the WAIT gives up, surfacing a timeout error to the agent.
    /// </summary>
    public const int SendCommandCompletionTimeoutMs = 30_000;

    private static JsonObject RunSendCommand(JsonObject args) {
        string command = args["command"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(command)) {
            return ToolError("send_command requires a non-empty 'command' argument.", "bad-arguments");
        }

        CommandInvoker? invoker = AgentRuntime.Invoker;
        if (invoker is null) {
            return ToolError("Command engine is not available.", "engine-unavailable");
        }

        AgentRuntime.Audit("send_command", command);

        // #195: enqueue-and-await. send_command is a PRODUCER into the invoker's single
        // dispatcher-drained queue (the same queue the legacy TCP/serial pipeline feeds); the
        // dispatcher thread executes the command under AgentRuntime.InputGate (the #113
        // no-interleaving invariant) and the completion task fires only after it ran — so reading
        // reply.Captured below no longer races the execution. A command that never entered the queue
        // (unknown name, or dropped whole by the #154 bounds) is a failure, not the old always-"ok"
        // (the richer taxonomy stays #206; these two codes are the minimum honest signal).
        CapturingReply reply = new();
        CommandEnqueueResult enqueued = invoker.TryEnqueueWithCompletion(reply, command, out Task<bool>? completion);

        AgentSession session = AgentRuntime.Session;
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
                "It may have PARTIALLY executed — verify the desktop state with query/capture before assuming nothing ran.",
                "emergency-stopped");
        }

        // The legacy command path returns opaque text, not a CommandResult; carry it forward as the
        // success payload. (A richer send_command success/failure taxonomy is #206.)
        string captured = reply.Captured.Trim();
        JsonObject result = new() { ["output"] = string.IsNullOrEmpty(captured) ? "ok" : captured };
        CommandEventHub.Publish(new CommandEvent("send_command", CommandTersifier.ForRawCommand(command), CommandOutcome.Ok, session.SessionId));
        return McpResult(AgentToolResult.Success(result, session.SessionId));
    }

    /// <summary>
    /// Provisions a fresh, disposable, isolated MCEC instance (#138) and returns the handoff. Gated behind
    /// the operator's <see cref="AppSettings.AllowSessionProvisioning"/> opt-in — the one thing that cannot
    /// be self-served — and never touches the installed config. Also reaps stale session dirs opportunistically.
    /// </summary>
    private static JsonObject RunProvisionSession(JsonObject args) {
        if (AgentRuntime.Settings?.AllowSessionProvisioning != true) {
            AgentRuntime.Audit("provision-session", "BLOCKED — session provisioning is not authorized");
            return ToolError(
                "Session provisioning is not authorized. The operator must enable AllowSessionProvisioning to opt in; it cannot be self-served.",
                "provisioning-not-authorized");
        }

        bool mcpServer = args["mcpServer"] is not JsonValue mv || !mv.TryGetValue(out bool m) || m; // default true
        List<string>? commands = StrArray(args["commands"] as JsonArray);

        try {
            SessionProvisioner.ReapOrphans(TimeSpan.FromHours(SessionReapAgeHours));
            ProvisionedSession session = SessionProvisioner.Provision(mcpServer, commands);
            return McpResult(AgentToolResult.Success(session.ToJsonObject(), AgentRuntime.Session.SessionId));
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: provision-session failed: {e.Message}");
            return ToolError($"Failed to provision a session: {e.Message}", "provisioning-failed");
        }
    }

    /// <summary>Tears down a provisioned session directory by id (#138).</summary>
    private static JsonObject RunEndSession(JsonObject args) {
        string? sessionId = Str(args, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId)) {
            return ToolError("end-session requires a non-empty 'sessionId' argument.", "bad-arguments");
        }
        bool removed = SessionProvisioner.Teardown(sessionId);
        JsonObject result = new() {
            ["sessionId"] = sessionId,
            ["removed"] = removed,
        };
        if (!removed) {
            result["note"] = "The session directory could not be deleted — its mcec.exe may still be running. Stop it and retry, or MCEC will reap it on a later launch.";
        }
        return McpResult(AgentToolResult.Success(result, AgentRuntime.Session.SessionId));
    }

    /// <summary>Age after which an orphaned/abandoned provisioned session directory is reaped on launch/provision.</summary>
    public const int SessionReapAgeHours = 12;

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
            : null; // unknown name — the caller reports unknown-tool; never guess a command (#201)

    // -------------------------------------------------------------------------------------------
    // stdio transport
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs the newline-delimited JSON-RPC loop over the given streams (stdin/stdout for <c>--mcp</c>).
    /// Returns when the input stream reaches EOF.
    /// </summary>
    public static void RunStdio(Stream input, Stream output) {
        using StreamReader reader = new(input, new UTF8Encoding(false));
        using StreamWriter writer = new(output, new UTF8Encoding(false)) { AutoFlush = true };
        Logger.Instance.Log4.Info("AgentServer: MCP stdio transport started.");
        RunStdioLoop(reader, writer, req => Dispatch(req, AgentTransport.Stdio));
        Logger.Instance.Log4.Info("AgentServer: MCP stdio transport ended (EOF).");
    }

    /// <summary>
    /// The stdio read/dispatch/write loop, factored so it is testable. Each request line is dispatched on
    /// a worker so a slow call (a long <c>wait-for</c>, a deep <c>query</c>) never blocks later requests
    /// (#113). JSON-RPC responses carry the request <c>id</c>, so out-of-order completion is valid;
    /// writes are serialized so response lines never interleave.
    /// </summary>
    public static void RunStdioLoop(TextReader reader, TextWriter writer, Func<JsonObject, JsonObject?> dispatch) {
        object writeLock = new();
        List<Task> pending = [];
        string? line;
        while ((line = reader.ReadLine()) is not null) {
            if (line.Length == 0) {
                continue;
            }
            string requestLine = line;
            pending.Add(Task.Run(() => {
                JsonObject? response = ProcessStdioLine(requestLine, dispatch);
                if (response is not null) {
                    lock (writeLock) {
                        writer.WriteLine(response.ToJsonString(AgentJson.Options));
                    }
                }
            }));
        }
        try {
            Task.WaitAll([.. pending]);
        }
        catch (AggregateException) {
            // Per-request faults are already turned into JSON-RPC error responses below; nothing to do.
        }
    }

    private static JsonObject? ProcessStdioLine(string line, Func<JsonObject, JsonObject?> dispatch) {
        try {
            JsonNode? node = JsonNode.Parse(line);
            if (node is not JsonObject request) {
                return Error(null, -32600, "Invalid Request");
            }
            return dispatch(request);
        }
        catch (JsonException e) {
            return Error(null, -32700, $"Parse error: {e.Message}");
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: dispatch error: {e}");
            return Error(null, -32603, $"Internal error: {e.Message}");
        }
    }

    // -------------------------------------------------------------------------------------------
    // HTTP transport (localhost floor)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Validates <paramref name="address"/> as a loopback bind address AND canonicalizes it into the
    /// exact host string <see cref="StartHttp"/> puts in the <see cref="HttpListener"/> prefix (#152).
    /// Returns true only for the literal hostname <c>localhost</c> or a literal IP that
    /// <see cref="IPAddress.IsLoopback"/> confirms is loopback; on success <paramref name="prefixHost"/>
    /// is the CANONICAL form — <c>localhost</c>, a dotted IPv4 literal (<c>127.0.0.1</c>), or a bracketed
    /// IPv6 literal (<c>[::1]</c>).
    /// <para>
    /// Canonicalizing is load-bearing, not cosmetic. <see cref="IPAddress.TryParse"/> also blesses
    /// obfuscated loopback spellings — <c>0x7f.0.0.1</c>, <c>127.1</c>, <c>2130706433</c>,
    /// <c>127.00.00.01</c>, <c>::ffff:127.0.0.1</c> — as loopback, but <c>http.sys</c> parses those RAW
    /// strings differently: some register as hostname/wildcard bindings that, under an elevated MCEC,
    /// bind non-loopback, so a LAN attacker with a matching <c>Host</c> header would reach the
    /// unauthenticated (#143) endpoint. Building the prefix from <c>ip.ToString()</c> instead collapses
    /// every accepted form to a literal <c>http.sys</c> binds to loopback (an IPv4-mapped IPv6 loopback
    /// is folded to its IPv4 literal). Everything else is rejected: the HttpListener wildcards <c>+</c>
    /// and <c>*</c>, the all-interfaces addresses <c>0.0.0.0</c> and <c>::</c>, any non-loopback IP, and
    /// any hostname other than <c>localhost</c>. Hostnames are deliberately NOT resolved via DNS — a
    /// name could resolve to a non-loopback interface, so only the one literal name is trusted.
    /// </para>
    /// </summary>
    internal static bool TryGetLoopbackPrefixHost(string? address, out string prefixHost) {
        prefixHost = string.Empty;
        if (string.IsNullOrWhiteSpace(address)) {
            return false;
        }
        string candidate = address.Trim();
        if (string.Equals(candidate, "localhost", StringComparison.OrdinalIgnoreCase)) {
            prefixHost = "localhost";
            return true;
        }
        // An IPv6 literal may be written bracketed ([::1]), as it appears inside a URL/prefix.
        if (candidate.Length >= 2 && candidate[0] == '[' && candidate[^1] == ']') {
            candidate = candidate[1..^1];
        }
        if (!IPAddress.TryParse(candidate, out IPAddress? ip) || !IPAddress.IsLoopback(ip)) {
            return false;
        }
        // Fold an IPv4-mapped IPv6 loopback (::ffff:127.0.0.1) to its IPv4 literal so the prefix
        // http.sys sees is an unambiguous 127.x.y.z, not a form it may treat as a hostname registration.
        if (ip.IsIPv4MappedToIPv6) {
            ip = ip.MapToIPv4();
        }
        prefixHost = ip.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString();
        return true;
    }

    /// <summary>
    /// Whether <paramref name="address"/> is a bind address <see cref="StartHttp"/> will accept (#152):
    /// the thin predicate over <see cref="TryGetLoopbackPrefixHost"/>. See that method for the accepted
    /// set and why an accepted address is canonicalized before it ever reaches <see cref="HttpListener"/>.
    /// </summary>
    internal static bool IsLoopbackBindAddress(string? address) => TryGetLoopbackPrefixHost(address, out _);

    public static void StartHttp() {
        AppSettings? settings = AgentRuntime.Settings;
        if (settings is null) {
            return;
        }
        lock (HttpLock) {
            if (_listener is not null) {
                return;
            }
            // SECURITY (#152 + #143): two composed rules decide the bind.
            //
            // A LOOPBACK bind is the safe default and needs no auth, but the raw settings string is
            // canonicalized first (#152) — built into the prefix as a normalized loopback literal rather
            // than passed through verbatim — so obfuscated spellings (e.g. "0x7f.0.0.1", "2130706433",
            // "::ffff:127.0.0.1") that http.sys would otherwise treat as wildcard hostname registrations
            // can't slip a non-loopback bind past validation.
            //
            // A NON-LOOPBACK bind is a deliberate off-box exposure and is only allowed with a bearer token
            // (#143): the Host/Origin gate defeats browser CSRF/DNS-rebinding but the Host header is
            // attacker-controlled and is not a network control, so an unauthenticated off-box bind would
            // hand UI automation + screen capture to the network. Without a token we refuse to start.
            string prefix;
            if (TryGetLoopbackPrefixHost(settings.McpBindAddress, out string prefixHost)) {
                prefix = $"http://{prefixHost}:{settings.McpHttpPort}/";
            }
            else if (!string.IsNullOrEmpty(settings.McpAuthToken)) {
                // Deliberate, authenticated off-box bind (#143). The raw operator-chosen address is used
                // as-is; canonicalization only applies to the loopback path.
                prefix = $"http://{settings.McpBindAddress}:{settings.McpHttpPort}/";
            }
            else {
                Logger.Instance.Log4.Error(
                    $"AgentServer: REFUSING to start the MCP HTTP transport: McpBindAddress '{settings.McpBindAddress}' " +
                    "is not a loopback address and no McpAuthToken is set. An unauthenticated non-loopback bind would " +
                    "expose UI automation and screen capture to the network. Either set <McpBindAddress> to " +
                    "\"localhost\", \"127.0.0.1\", \"::1\", or another literal loopback IP (127.x.y.z), or set " +
                    "<McpAuthToken> to deliberately expose the door off-box. Wildcards (\"+\", \"*\") and " +
                    "all-interfaces addresses (\"0.0.0.0\", \"::\") are never loopback and still require a token.");
                return;
            }
            HttpListener listener = new();
            listener.Prefixes.Add(prefix);
            try {
                listener.Start();
            }
            catch (HttpListenerException e) {
                Logger.Instance.Log4.Error($"AgentServer: could not start HTTP listener on {prefix}: {e.Message}");
                return;
            }
            _listener = listener;
            Logger.Instance.Log4.Info($"AgentServer: MCP HTTP transport listening on {prefix}mcp");
            _httpThread = new Thread(() => HttpLoop(listener)) { IsBackground = true, Name = "MCEC-AgentHttp" };
            _httpThread.Start();
        }
    }

    public static void StopHttp() {
        lock (HttpLock) {
            if (_listener is null) {
                return;
            }
            Logger.Instance.Log4.Info("AgentServer: stopping HTTP transport.");
            _listener.Close();
            _listener = null;
            _httpThread = null;
        }
    }

    private static void HttpLoop(HttpListener listener) {
        while (listener.IsListening) {
            HttpListenerContext context;
            try {
                context = listener.GetContext();
            }
            catch (HttpListenerException) {
                break; // listener stopped
            }
            catch (InvalidOperationException) {
                break;
            }
            // Serve each request on a worker so a slow call (a long wait-for, a deep query) doesn't block
            // accepting or serving other requests (#113). Each context owns its own response stream, so no
            // cross-request correlation is needed. The fan-out is bounded (#151): past
            // MaxConcurrentHttpRequests in-flight requests the server answers 503 instead of spawning
            // unbounded tasks. (WriteHttp here is safe on the accept thread: http.sys buffers the small
            // response in the kernel, so a slow client can't stall the accept loop.)
            if (!HttpWorkerSlots.Wait(0)) {
                try {
                    WriteHttp(context, 503, new JsonObject { ["error"] = $"Server busy: more than {MaxConcurrentHttpRequests} requests in flight" });
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Error($"AgentServer: failed to write HTTP 503: {e.Message}");
                }
                continue;
            }
            _ = Task.Run(() => {
                try {
                    HandleHttp(context);
                }
                finally {
                    HttpWorkerSlots.Release();
                }
            });
        }
    }

    /// <summary>
    /// Dispatches an HTTP request through the test override when one is set, otherwise through
    /// <see cref="Dispatch"/> tagged <see cref="AgentTransport.Http"/> so the transport-sensitive
    /// <c>send_command</c> gate (#153) sees the network transport.
    /// </summary>
    private static JsonObject? DispatchHttp(JsonObject req) =>
        HttpDispatchOverride is { } over ? over(req) : Dispatch(req, AgentTransport.Http);

    private static void HandleHttp(HttpListenerContext context) {
        try {
            // SECURITY (#143): a localhost HTTP service is reachable by any web page the operator visits
            // (browser CSRF) and by a rebinding attacker (DNS rebinding). Validate Host/Origin/token and
            // the path BEFORE reading the body or dispatching, so a failed request never actuates a tool.
            HttpListenerRequest request = context.Request;
            AppSettings? settings = AgentRuntime.Settings;
            HttpGateDecision decision = GateHttpRequest(
                request.HttpMethod,
                request.Url?.AbsolutePath,
                request.UserHostName,
                request.Headers["Origin"],
                request.Headers["Authorization"],
                settings?.McpHttpPort ?? 0,
                settings?.McpAuthToken);
            if (decision != HttpGateDecision.Allow) {
                RejectHttp(context, decision, request);
                return;
            }
            // #151: refuse an oversized body from the header alone — never buffer it. ContentLength64
            // is -1 for chunked transfer, which passes here and is caught by the bounded read below.
            if (context.Request.ContentLength64 > MaxHttpBodyBytes) {
                WriteHttp(context, 413, new JsonObject { ["error"] = $"Request body exceeds {MaxHttpBodyBytes} bytes" });
                return;
            }
            // Bounded read, so a chunked (or lying) client without an honest Content-Length still
            // cannot make the server buffer more than the cap.
            if (!TryReadBoundedBody(context.Request.InputStream, context.Request.ContentEncoding, out string body)) {
                WriteHttp(context, 413, new JsonObject { ["error"] = $"Request body exceeds {MaxHttpBodyBytes} bytes" });
                return;
            }
            JsonObject response;
            try {
                JsonNode? node = JsonNode.Parse(body);
                response = node is JsonObject req
                    ? DispatchHttp(req) ?? new JsonObject { ["jsonrpc"] = "2.0" }
                    : Error(null, -32600, "Invalid Request")!;
            }
            catch (JsonException e) {
                response = Error(null, -32700, $"Parse error: {e.Message}")!;
            }
            WriteHttp(context, 200, response);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: HTTP handler error: {e}");
            try {
                WriteHttp(context, 500, new JsonObject { ["error"] = e.Message });
            }
            catch (Exception inner) {
                Logger.Instance.Log4.Error($"AgentServer: failed to write HTTP error: {inner.Message}");
            }
        }
    }

    /// <summary>
    /// Validates an inbound MCP/HTTP request against the localhost front-door policy (#143):
    /// POST only, path exactly <c>/mcp</c>, a loopback <c>Host</c> (defeats DNS rebinding), an
    /// absent-or-loopback <c>Origin</c> (defeats browser CSRF), and — when a token is configured —
    /// a matching <c>Authorization: Bearer</c> header. Pure so it can be unit tested without a socket.
    /// </summary>
    internal static HttpGateDecision GateHttpRequest(
            string httpMethod,
            string? absolutePath,
            string? hostHeader,
            string? originHeader,
            string? authorizationHeader,
            int port,
            string? requiredToken) {
        if (!string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase)) {
            return HttpGateDecision.RejectMethod;
        }
        if (!string.Equals(absolutePath, "/mcp", StringComparison.Ordinal)) {
            return HttpGateDecision.RejectPath;
        }
        if (!IsLoopbackAuthority(hostHeader, port)) {
            return HttpGateDecision.RejectHost;
        }
        if (!IsAllowedOrigin(originHeader)) {
            return HttpGateDecision.RejectOrigin;
        }
        if (!string.IsNullOrEmpty(requiredToken) && !BearerTokenMatches(authorizationHeader, requiredToken)) {
            return HttpGateDecision.RejectAuth;
        }
        return HttpGateDecision.Allow;
    }

    /// <summary>True if <paramref name="hostHeader"/> names a loopback host and (if a port is present) that port.</summary>
    private static bool IsLoopbackAuthority(string? hostHeader, int port) {
        if (string.IsNullOrWhiteSpace(hostHeader)) {
            return false;
        }
        string host = hostHeader.Trim();
        string hostName;
        string? portPart = null;
        if (host.StartsWith('[')) {
            // IPv6 literal: [::1] or [::1]:port
            int end = host.IndexOf(']');
            if (end < 0) {
                return false;
            }
            hostName = host.Substring(1, end - 1);
            if (end + 1 < host.Length && host[end + 1] == ':') {
                portPart = host[(end + 2)..];
            }
        }
        else {
            int colon = host.IndexOf(':');
            if (colon >= 0) {
                hostName = host[..colon];
                portPart = host[(colon + 1)..];
            }
            else {
                hostName = host;
            }
        }
        if (portPart is not null && (!int.TryParse(portPart, out int p) || p != port)) {
            return false;
        }
        return IsLoopbackName(hostName);
    }

    /// <summary>True if the Origin header is absent (typical non-browser MCP client) or names a loopback host.</summary>
    private static bool IsAllowedOrigin(string? originHeader) {
        if (string.IsNullOrEmpty(originHeader)) {
            return true;
        }
        // A literal "null" origin (sandboxed iframe / file://) or any un-parseable value is not loopback.
        return Uri.TryCreate(originHeader, UriKind.Absolute, out Uri? uri) && IsLoopbackName(uri.Host);
    }

    private static bool IsLoopbackName(string hostName) =>
        string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(hostName, out IPAddress? ip) && IPAddress.IsLoopback(ip));

    /// <summary>
    /// True when binding the HTTP listener to <paramref name="bindAddress"/> would expose it off-box
    /// (anything that is not a loopback address), so a bearer token must be required (#143). An empty or
    /// unparseable bind address is treated as loopback (the default is 127.0.0.1 and the listener would
    /// fail to start on garbage anyway).
    /// </summary>
    internal static bool BindRequiresAuthToken(string? bindAddress) {
        if (string.IsNullOrWhiteSpace(bindAddress)) {
            return false;
        }
        string trimmed = bindAddress.Trim();
        if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        if (!IPAddress.TryParse(trimmed, out IPAddress? ip)) {
            return false;
        }
        return !IPAddress.IsLoopback(ip);
    }

    /// <summary>Constant-time check of an <c>Authorization: Bearer &lt;token&gt;</c> header against the expected token.</summary>
    private static bool BearerTokenMatches(string? authorizationHeader, string expectedToken) {
        if (string.IsNullOrEmpty(authorizationHeader)) {
            return false;
        }
        const string scheme = "Bearer ";
        if (!authorizationHeader.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        string provided = authorizationHeader[scheme.Length..].Trim();
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(expectedToken);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void RejectHttp(HttpListenerContext context, HttpGateDecision decision, HttpListenerRequest request) {
        (int status, string message) = decision switch {
            HttpGateDecision.RejectMethod => (405, "Only POST /mcp is supported"),
            HttpGateDecision.RejectPath => (404, "Not found: only POST /mcp is supported"),
            HttpGateDecision.RejectHost => (403, "Forbidden: Host must be a loopback authority"),
            HttpGateDecision.RejectOrigin => (403, "Forbidden: cross-origin requests are not allowed"),
            HttpGateDecision.RejectAuth => (401, "Unauthorized: a valid bearer token is required"),
            _ => (403, "Forbidden"),
        };
        // Audit rejected drive-by/rebinding attempts so an operator can see them.
        Logger.Instance.Log4.Warn(
            $"AGENT-AUDIT: rejected HTTP request ({decision}) method={request.HttpMethod} " +
            $"path={request.Url?.AbsolutePath} host={request.UserHostName} origin={request.Headers["Origin"]} " +
            $"remote={request.RemoteEndPoint}");
        WriteHttp(context, status, new JsonObject { ["error"] = message });
    }

    /// <summary>
    /// Reads <paramref name="input"/> to its end into <paramref name="body"/>, refusing to buffer more
    /// than <see cref="MaxHttpBodyBytes"/> (#151). Returns false — with <paramref name="body"/> empty
    /// and the stream abandoned — the moment the cap is crossed, so a chunked or lying client cannot
    /// bypass the Content-Length check and exhaust memory.
    /// </summary>
    internal static bool TryReadBoundedBody(Stream input, Encoding encoding, out string body) {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[64 * 1024];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0) {
            if (buffer.Length + read > MaxHttpBodyBytes) {
                body = "";
                return false;
            }
            buffer.Write(chunk, 0, read);
        }
        // Unlike the StreamReader this replaced, GetString does not strip a leading BOM; a
        // BOM-prefixed body now fails JSON parsing with a normal JSON-RPC parse error.
        body = encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        return true;
    }

    private static void WriteHttp(HttpListenerContext context, int status, JsonObject payload) {
        byte[] bytes = Encoding.UTF8.GetBytes(payload.ToJsonString(AgentJson.Options));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    // -------------------------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------------------------

    private static JsonObject Result(JsonNode? id, JsonNode result) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result,
    };

    private static JsonObject Error(JsonNode? id, int code, string message) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

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
    /// A tool-level failure (a security gate or a bad request) reported as the #101 envelope. These are
    /// MCEC-side refusals the agent cannot recover from on its own, so they map to the <c>internal</c>
    /// category; <paramref name="code"/> distinguishes the specific cause. The ambient session's id is
    /// attached so even a refused call tells the agent which session it belonged to.
    /// </summary>
    private static JsonObject ToolError(string message, string code = "internal-error") =>
        McpResult(AgentToolResult.Failure(new AgentError(code, AgentErrorCategory.Internal, message), AgentRuntime.Session.SessionId));

    private static JsonObject TextContent(string text) => new() {
        ["type"] = "text",
        ["text"] = text,
    };

    private static JsonNode? TryParse(string json) {
        try {
            return JsonNode.Parse(json);
        }
        catch (JsonException) {
            return null;
        }
    }

    private static string AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    private static string? Str(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
