// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace MCEControl;

/// <summary>
/// MCEC 3.0's agent front door: a self-contained Model Context Protocol (MCP) server, hand-rolled as
/// JSON-RPC 2.0 over two transports — stdio (for an MCP client that launches <c>mcec.exe --mcp</c>)
/// and a localhost HTTP/JSON floor (POST a JSON-RPC request to <c>/mcp</c>). No external SDK or
/// Python/Node runtime: the same self-contained native binary, with MCP/HTTP as just one more
/// transport over the existing command core.
///
/// SECURITY: the observation tools (capture/query/find/invoke) only run when
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> is true; otherwise a tool call is reported as an
/// error. The HTTP listener binds to <see cref="AppSettings.McpBindAddress"/> (127.0.0.1 by default).
/// Every tool call is loudly audit-logged.
/// </summary>
public static class AgentServer {
    public const string ProtocolVersion = "2025-06-18";

    private static readonly object HttpLock = new();
    private static HttpListener? _listener;
    private static Thread? _httpThread;
    private static readonly object ExecLock = new();

    // -------------------------------------------------------------------------------------------
    // JSON-RPC dispatch (shared by stdio + HTTP)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Dispatches a single JSON-RPC request object and returns the response object, or null when the
    /// request is a notification (no <c>id</c>) and therefore takes no response.
    /// </summary>
    public static JsonObject? Dispatch(JsonObject request) {
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
                JsonObject callResult = CallTool(prms);
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
    /// Built-in guidance handed to an agent at connect time. Keep it short, concrete, and honest about
    /// the gating — this is the "instructions" an MCP client shows the model.
    /// </summary>
    public const string Instructions =
        "MCEC (Model Context Environment Controller) lets you see and drive native Windows apps.\n" +
        "Work the loop: observe -> target -> act -> observe.\n" +
        "1. TARGET a window by `window` (title substring), `process` (name without .exe), `className`, " +
        "or `foreground:true` — you MUST give at least one; a call with no target fails. Reuse the " +
        "`handle` a `query` returns for follow-up calls: it is stable, and a dialog you open shares the " +
        "process name, so re-resolving by process/title can match the wrong window. Open menus and other " +
        "untitled popups are not enumerated by title/process — target them by handle or `foreground:true`.\n" +
        "2. OBSERVE: `query` dumps the UI Automation tree (controlType, name, automationId, bounds, " +
        "state, value) so you can pick a control instead of guessing pixels; `capture` returns a PNG " +
        "of the window (works on composited WinUI/WPF surfaces). Check results for trouble: a `capture` " +
        "with errorCategory `capture-blank` is a black/empty frame (minimized, cloaked, occluded, or a " +
        "locked session) — restore/foreground the window and retry instead of trusting the image; a " +
        "`capture-fallback` warning means PrintWindow was refused and the picture may be wrong. If " +
        "`query` returns `truncated:true` (a `tree-truncated` warning), the tree hit the node cap — raise " +
        "`maxNodes` or target a deeper window so you don't reason over a partial tree. warnings are " +
        "non-fatal; errorCategory tells you how to recover.\n" +
        "3. ACT: prefer `invoke` (by name/automationId/classname; action invoke|toggle|setvalue|setfocus|" +
        "expand|collapse) over coordinate clicks — it is far more reliable. To click a menu item, first " +
        "`invoke` its parent menu with action `expand` (a closed menu's sub-items are not in the tree " +
        "until opened), then `invoke` the item. Invoking a control that opens a MODAL dialog (About, " +
        "Settings, message/file dialogs) returns promptly with `modalPending:true` — the action " +
        "completes when the dialog closes — so just `query`/`capture` the new window to read it, and " +
        "`invoke` its buttons to dismiss it. Use `wait-for` (or `find` with a timeout) to wait for a " +
        "control to appear before acting. " +
        "`send_command` sends any raw MCEC command (keystrokes, mouse, launch).\n" +
        "4. VERIFY with another `query` or `capture` — always confirm the act had the intended effect.\n" +
        "RESULTS: every tool returns one envelope — `{ ok, result?, warnings?, error? }`. Branch on `ok` " +
        "first: on success read `result`; on failure read `error.category` (a closed set: timeout, " +
        "ambiguous-selector, stale-element, no-target, capture-blank, focus, elevation, foreground, " +
        "internal) to choose recovery — e.g. `no-target` means broaden the selector or `query` to " +
        "discover targets, `ambiguous-selector` means add `processName`/`className`/`automationId`, " +
        "`stale-element` means re-`query`/`find` for a fresh handle. `error.detail` is human-readable and " +
        "`error.lastObservation`, when present, is the last good state before the failure.\n" +
        "SECURITY: observation tools (capture/query/find/invoke) only work when the operator has set " +
        "AgentCommandsEnabled=true; otherwise they return an error — surface that to the user rather " +
        "than retrying. Every action is audit-logged on the host.";

    // -------------------------------------------------------------------------------------------
    // Tool catalog
    // -------------------------------------------------------------------------------------------

    private static JsonObject WindowTargetProps() => new() {
        ["window"] = PropSchema("string", "Window title substring (case-insensitive) to target"),
        ["handle"] = PropSchema("integer", "Explicit window handle (HWND) to target"),
        ["process"] = PropSchema("string", "Process name (without .exe) to target"),
        ["className"] = PropSchema("string", "Window class name to target"),
        ["foreground"] = PropSchema("boolean", "Target the current foreground window"),
    };

    private static JsonObject PropSchema(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };

    private static JsonArray BuildToolsList() {
        JsonArray tools = [];

        JsonObject captureProps = WindowTargetProps();
        captureProps["x"] = PropSchema("integer", "Region left (use with width/height instead of a window)");
        captureProps["y"] = PropSchema("integer", "Region top");
        captureProps["width"] = PropSchema("integer", "Region width");
        captureProps["height"] = PropSchema("integer", "Region height");
        captureProps["file"] = PropSchema("string", "Optional path to also save the PNG to");
        tools.Add(Tool("capture",
            "Screenshot a window (PrintWindow PW_RENDERFULLCONTENT, captures WinUI/WPF surfaces) or a screen region; returns PNG. Blank/black frames are detected and reported as a capture-blank error (window) or warning (region) rather than a silent bad image.",
            captureProps, []));

        JsonObject queryProps = WindowTargetProps();
        queryProps["maxDepth"] = PropSchema("integer", "Max UI Automation tree depth (default 6)");
        queryProps["maxNodes"] = PropSchema("integer", "Max UI Automation nodes returned (default 1000); a clipped tree is flagged with a tree-truncated warning");
        tools.Add(Tool("query",
            "Dump the UI Automation tree of a window: control type, name, automation id, bounds, state. Returns nodeCount/truncated and warns when the node cap clips the tree.",
            queryProps, []));

        JsonObject findProps = WindowTargetProps();
        findProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        findProps["value"] = PropSchema("string", "Value to match");
        findProps["timeout"] = PropSchema("integer", "Milliseconds to wait for the element (0 = no wait)");
        tools.Add(Tool("find",
            "Find (or wait for, with a timeout) a UI Automation element by name / automation id / class.",
            findProps, ["value"]));

        JsonObject waitForProps = WindowTargetProps();
        waitForProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        waitForProps["value"] = PropSchema("string", "Value to match");
        waitForProps["timeout"] = PropSchema("integer", "Milliseconds to wait for the element (default 5000)");
        tools.Add(Tool("wait-for",
            "Wait for a UI Automation element to appear: polls until found or the timeout elapses (default 5s).",
            waitForProps, ["value"]));

        JsonObject invokeProps = WindowTargetProps();
        invokeProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        invokeProps["value"] = PropSchema("string", "Value to match");
        invokeProps["action"] = PropSchema("string", "invoke | toggle | setvalue | setfocus | expand | collapse (default invoke). Use expand to open a menu before invoking its items.");
        invokeProps["text"] = PropSchema("string", "Text for the setvalue action");
        tools.Add(Tool("invoke",
            "Drive a UI Automation element (Invoke/Toggle/Value/SetFocus) — more reliable than coordinate clicks.",
            invokeProps, ["value"]));

        tools.Add(Tool("send_command",
            "Send any raw MCEC command string to the existing command core (e.g. actuation commands).",
            new JsonObject { ["command"] = PropSchema("string", "The MCEC command string to enqueue") },
            ["command"]));

        return tools;
    }

    private static JsonObject Tool(string name, string description, JsonObject properties, JsonArray required) => new() {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = new JsonObject {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        },
    };

    // -------------------------------------------------------------------------------------------
    // tools/call
    // -------------------------------------------------------------------------------------------

    private static JsonObject CallTool(JsonObject? prms) {
        string name = AsString(prms?["name"]);
        JsonObject args = prms?["arguments"] as JsonObject ?? [];

        if (name == "send_command") {
            return RunSendCommand(args);
        }

        if (name is "capture" or "query" or "find" or "wait-for" or "invoke") {
            if (!AgentRuntime.AgentCommandsEnabled) {
                AgentRuntime.Audit(name, "BLOCKED — agent commands disabled");
                return ToolError("Agent commands are disabled. Set AgentCommandsEnabled=true to opt in.", "agent-commands-disabled");
            }
            return RunAgentCommand(name, args);
        }

        return ToolError($"Unknown tool: {name}", "unknown-tool");
    }

    private static JsonObject RunAgentCommand(string name, JsonObject args) {
        // Honor the per-command Enabled flag — the documented second security gate. The MCP tool only
        // runs if the corresponding command in the loaded table is enabled (built-ins ship disabled;
        // the operator opts in per-command via mcec.commands). Fail closed if the table/command is missing.
        if ((AgentRuntime.Invoker?[name] as Command)?.Enabled != true) {
            AgentRuntime.Audit(name, "BLOCKED — command is disabled in mcec.commands");
            return ToolError($"The '{name}' command is disabled. Enable it in mcec.commands (set Enabled=\"true\").", "command-disabled");
        }

        Command cmd = BuildCommand(name, args);
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

        // `invoke` can activate a control that opens a MODAL dialog (About, Settings, message/file
        // dialogs). The UIA Invoke runs the control's click handler synchronously, so the call would
        // otherwise block — and hold ExecLock — for the dialog's whole lifetime, deadlocking every
        // later tool call (the agent couldn't even query or dismiss the dialog it just opened). Run it
        // on a worker and, if it hasn't returned within a short grace, report "modal pending" and
        // return; the worker finishes when the dialog closes. capture/query/find/send_command keep the
        // simple serialized path, and the legacy TCP/serial pipeline (MainWindow.ReceivedData ->
        // CommandInvoker.ExecuteNext on the UI thread) is untouched, so home-automation sequences keep
        // their in-order, synchronous behavior.
        if (name == "invoke") {
            if (!TryRunInvokeWithModalGrace(cmd)) {
                AgentRuntime.Audit(name, "dispatched; a modal dialog appears to be open — returning without blocking");
                return McpResult(AgentToolResult.Success(InvokeModalPendingResult(), session.SessionId));
            }
        }
        else {
            lock (ExecLock) {
                cmd.Execute();
            }
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

        return McpResult(env, image);
    }

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
    /// <see cref="ExecLock"/> for an invoke: a modal opener must not block the later query/capture/invoke
    /// calls the agent needs to read or dismiss the very dialog it opened. The worker ends on its own
    /// when the dialog closes.
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
        CapturingReply reply = new();
        lock (ExecLock) {
            invoker.Enqueue(reply, command);
            invoker.ExecuteNext();
        }

        AgentSession session = AgentRuntime.Session;
        session.RecordAction("send_command");

        // The legacy command path returns opaque text, not a CommandResult; carry it forward as the
        // success payload. (Native success/failure detection for send_command is out of Phase 1 scope.)
        string captured = reply.Captured.Trim();
        JsonObject result = new() { ["output"] = string.IsNullOrEmpty(captured) ? "ok" : captured };
        return McpResult(AgentToolResult.Success(result, session.SessionId));
    }

    /// <summary>Builds and populates an agent command instance from MCP tool arguments.</summary>
    private static Command BuildCommand(string name, JsonObject args) => name switch {
        "capture" => new CaptureCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            X = Int(args, "x"),
            Y = Int(args, "y"),
            Width = Int(args, "width"),
            Height = Int(args, "height"),
            File = Str(args, "file")!,
        },
        "query" => new QueryCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            MaxDepth = Int(args, "maxDepth") is int d and > 0 ? d : 6,
            MaxNodes = Int(args, "maxNodes") is int n and > 0 ? n : 1000,
        },
        "find" or "wait-for" => new FindCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            By = Str(args, "by") ?? "name",
            Value = Str(args, "value")!,
            Timeout = Int(args, "timeout"),
        },
        _ => new InvokeCommand { // invoke
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            By = Str(args, "by") ?? "name",
            Value = Str(args, "value")!,
            Action = Str(args, "action") ?? "invoke",
            Text = Str(args, "text")!,
        },
    };

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

        string? line;
        while ((line = reader.ReadLine()) is not null) {
            if (line.Length == 0) {
                continue;
            }
            JsonObject? response;
            try {
                JsonNode? node = JsonNode.Parse(line);
                if (node is not JsonObject request) {
                    response = Error(null, -32600, "Invalid Request");
                }
                else {
                    response = Dispatch(request);
                }
            }
            catch (JsonException e) {
                response = Error(null, -32700, $"Parse error: {e.Message}");
            }
            catch (Exception e) {
                Logger.Instance.Log4.Error($"AgentServer: dispatch error: {e}");
                response = Error(null, -32603, $"Internal error: {e.Message}");
            }

            if (response is not null) {
                writer.WriteLine(response.ToJsonString(AgentJson.Options));
            }
        }
        Logger.Instance.Log4.Info("AgentServer: MCP stdio transport ended (EOF).");
    }

    // -------------------------------------------------------------------------------------------
    // HTTP transport (localhost floor)
    // -------------------------------------------------------------------------------------------

    public static void StartHttp() {
        AppSettings? settings = AgentRuntime.Settings;
        if (settings is null) {
            return;
        }
        lock (HttpLock) {
            if (_listener is not null) {
                return;
            }
            string prefix = $"http://{settings.McpBindAddress}:{settings.McpHttpPort}/";
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
            HandleHttp(context);
        }
    }

    private static void HandleHttp(HttpListenerContext context) {
        try {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) {
                WriteHttp(context, 405, new JsonObject { ["error"] = "Only POST /mcp is supported" });
                return;
            }
            string body;
            using (StreamReader sr = new(context.Request.InputStream, context.Request.ContentEncoding)) {
                body = sr.ReadToEnd();
            }
            JsonObject response;
            try {
                JsonNode? node = JsonNode.Parse(body);
                response = node is JsonObject req
                    ? Dispatch(req) ?? new JsonObject { ["jsonrpc"] = "2.0" }
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

    private static long Long(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out long l) ? l : 0;

    private static int Int(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out int i) ? i : 0;

    private static bool Bool(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out bool b) && b;
}
