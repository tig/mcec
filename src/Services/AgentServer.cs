// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
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

    private static JsonObject WindowTargetProps() => new() {
        ["window"] = PropSchema("string", "Window title substring (case-insensitive) to target"),
        ["handle"] = PropSchema("integer", "Explicit window handle (HWND) to target"),
        ["process"] = PropSchema("string", "Process name (without .exe) to target"),
        ["className"] = PropSchema("string", "Window class name to target"),
        ["foreground"] = PropSchema("boolean", "Target the current foreground window"),
    };

    private static JsonObject PropSchema(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };

    /// <summary>Schema for a drag endpoint: either an element ({ by, value }) or a pixel ({ x, y }).</summary>
    private static JsonObject EndpointSchema(string description) => new() {
        ["type"] = "object",
        ["description"] = description,
        ["properties"] = new JsonObject {
            ["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)"),
            ["value"] = PropSchema("string", "Element value to match (omit for a pixel endpoint)"),
            ["x"] = PropSchema("integer", "Endpoint X in absolute screen pixels (omit for an element endpoint)"),
            ["y"] = PropSchema("integer", "Endpoint Y in absolute screen pixels (omit for an element endpoint)"),
        },
    };

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

        tools.Add(Tool("displays",
            "Report display geometry: every monitor's pixel bounds, working area, primary flag, and DPI/scale, plus the union virtualBounds. Use it to interpret the absolute-pixel bounds query/find return and to place pixel clicks/drags — no arguments.",
            [], []));

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

        JsonObject dragProps = WindowTargetProps();
        dragProps["from"] = EndpointSchema("Drag start: an element ({ by, value }) in the target window, or a pixel ({ x, y }).");
        dragProps["to"] = EndpointSchema("Drag end: an element ({ by, value }) in the target window, or a pixel ({ x, y }).");
        dragProps["path"] = new JsonObject {
            ["type"] = "array",
            ["description"] = "Optional intermediate waypoints (absolute screen pixels) between from and to.",
            ["items"] = new JsonObject {
                ["type"] = "object",
                ["properties"] = new JsonObject {
                    ["x"] = PropSchema("integer", "Waypoint X (screen pixels)"),
                    ["y"] = PropSchema("integer", "Waypoint Y (screen pixels)"),
                },
            },
        };
        tools.Add(Tool("drag",
            "Press → move along a path → release, dispatched atomically (no interleaving). Endpoints are an element (by/value, dragged from/to its centre) or an absolute screen pixel; add path waypoints for a curved/multi-stop drag. Covers window resize/move by chrome, sliders, marquee select, drag-reorder. Give a window target when either endpoint is an element.",
            dragProps, ["from", "to"]));

        JsonObject clickProps = WindowTargetProps();
        clickProps["at"] = EndpointSchema("Where to click: an element ({ by, value }) in the target window (its centre) or an absolute screen pixel ({ x, y }).");
        clickProps["button"] = PropSchema("string", "Button: left | right | middle (default left)");
        clickProps["count"] = PropSchema("integer", "Click count: 1 = single, 2 = double (default 1)");
        tools.Add(Tool("click",
            "Click at a point — an element (by/value, clicked at its centre) or an absolute screen pixel (the space query/find bounds report). Move+click is dispatched atomically. Prefer invoke for buttons/menus; use click for element types invoke cannot drive or when you must target a pixel. Give a window target when 'at' is an element.",
            clickProps, ["at"]));

        JsonObject recordProps = WindowTargetProps();
        recordProps["x"] = PropSchema("integer", "Region left (use with width/height instead of a window)");
        recordProps["y"] = PropSchema("integer", "Region top");
        recordProps["width"] = PropSchema("integer", "Region width");
        recordProps["height"] = PropSchema("integer", "Region height");
        recordProps["action"] = PropSchema("string", "start | stop | oneshot (default: oneshot if durationMs given, else start)");
        recordProps["fps"] = PropSchema("integer", "Frames per second (default 5, clamped to the operator limit)");
        recordProps["durationMs"] = PropSchema("integer", "For a one-shot: how long to record (clamped to the operator limit)");
        recordProps["maxWidth"] = PropSchema("integer", "Downscale frames so width fits this (default 1280)");
        recordProps["file"] = PropSchema("string", "Output .gif path (a temp path is generated if omitted)");
        tools.Add(Tool("record",
            "Record a window or region to an animated GIF over time (start/stop or a bounded one-shot). Use only to show CHANGE over time; for a single state check use capture.",
            recordProps, []));

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

        if (name is "capture" or "query" or "displays" or "find" or "wait-for" or "invoke" or "record" or "drag" or "click") {
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
                PublishOverlay(name, args, CommandOutcome.Pending, null, session.SessionId);
                return McpResult(AgentToolResult.Success(InvokeModalPendingResult(), session.SessionId));
            }
        }
        else if (name == "record") {
            // `record` manages its own background capture thread; a one-shot blocks the caller for the
            // whole recording duration. Do NOT hold ExecLock for that span or it would stall every
            // other tool call. Frame grabbing runs off-lock on the recorder thread regardless.
            cmd.Execute();
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
        CommandEventHub.Publish(new CommandEvent("send_command", CommandTersifier.ForRawCommand(command), CommandOutcome.Ok, session.SessionId));
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
        "record" => new RecordCommand {
            Action = Str(args, "action")!,
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            X = Int(args, "x"),
            Y = Int(args, "y"),
            Width = Int(args, "width"),
            Height = Int(args, "height"),
            Fps = Int(args, "fps"),
            DurationMs = Int(args, "durationMs"),
            MaxWidth = Int(args, "maxWidth"),
            File = Str(args, "file")!,
        },
        "drag" => BuildDragCommand(args),
        "click" => BuildClickCommand(args),
        "displays" => new DisplaysCommand(),
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

    /// <summary>Maps the drag tool's nested <c>from</c>/<c>to</c>/<c>path</c> arguments onto a <see cref="DragCommand"/>.</summary>
    private static DragCommand BuildDragCommand(JsonObject args) {
        JsonObject from = args["from"] as JsonObject ?? [];
        JsonObject to = args["to"] as JsonObject ?? [];
        return new DragCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            FromBy = Str(from, "by") ?? "name",
            FromValue = Str(from, "value")!,
            FromX = Int(from, "x"),
            FromY = Int(from, "y"),
            ToBy = Str(to, "by") ?? "name",
            ToValue = Str(to, "value")!,
            ToX = Int(to, "x"),
            ToY = Int(to, "y"),
            PathSpec = BuildPathSpec(args["path"] as JsonArray),
        };
    }

    /// <summary>Maps the click tool's nested <c>at</c> endpoint and button/count onto a <see cref="ClickCommand"/>.</summary>
    private static ClickCommand BuildClickCommand(JsonObject args) {
        JsonObject at = args["at"] as JsonObject ?? [];
        return new ClickCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            By = Str(at, "by") ?? "name",
            Value = Str(at, "value")!,
            X = Int(at, "x"),
            Y = Int(at, "y"),
            Button = Str(args, "button") ?? "left",
            Count = Int(args, "count") is int c and > 0 ? c : 1,
        };
    }

    /// <summary>Flattens the drag tool's <c>path</c> array of <c>{ x, y }</c> points into DragCommand's <c>x,y;x,y</c> spec.</summary>
    private static string BuildPathSpec(JsonArray? path) {
        if (path is null || path.Count == 0) {
            return string.Empty;
        }
        List<string> pairs = [];
        foreach (JsonNode? node in path) {
            if (node is JsonObject p) {
                pairs.Add($"{Int(p, "x")},{Int(p, "y")}");
            }
        }
        return string.Join(";", pairs);
    }

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
