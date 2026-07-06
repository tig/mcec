// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The MCP protocol layer (#215): dispatches one JSON-RPC 2.0 request object to its method handler;
/// <c>initialize</c>, <c>ping</c>, <c>tools/list</c>, <c>tools/call</c>; and shapes the response.
/// Shared by both transports (<see cref="McpStdioTransport"/> and <see cref="McpHttpTransport"/>);
/// tool execution (gate → catalog → build → dispatch → envelope) is delegated to the injected
/// <see cref="AgentToolExecutor"/>. Extracted from the old monolithic <c>AgentServer</c>, which
/// remains as the thin static facade wiring the production instances.
/// </summary>
public sealed class JsonRpcDispatcher {
    public const string ProtocolVersion = "2025-06-18";

    private readonly AgentToolExecutor _executor;
    private readonly Func<string> _instructions;

    /// <param name="executor">Runs <c>tools/call</c> requests (gating, catalog dispatch, envelope).</param>
    /// <param name="instructions">Supplies the connect-time agent guidance for <c>initialize</c>
    /// (production: <see cref="AgentServer.Instructions"/>, the embedded AgentInstructions.md).</param>
    public JsonRpcDispatcher(AgentToolExecutor executor, Func<string> instructions) {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
    }

    /// <summary>
    /// Dispatches a single JSON-RPC request object and returns the response object, or null when the
    /// request is a notification (no <c>id</c>) and therefore takes no response. <paramref name="transport"/>
    /// identifies the calling transport so <c>tools/call</c> can apply transport-sensitive gates;
    /// <c>send_command</c> honors the network gate over HTTP (#153).
    /// </summary>
    public JsonObject? Dispatch(JsonObject request, AgentTransport transport) {
        ArgumentNullException.ThrowIfNull(request);

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
                JsonObject callResult = _executor.CallTool(prms, transport);
                return Result(idNode, callResult);

            default:
                if (isNotification) {
                    return null;
                }
                return Error(idNode, -32601, $"Method not found: {method}");
        }
    }

    /// <summary>
    /// Connect-time guidance served instead of the full playbook when only the provisioning bootstrap
    /// is available (#296, <see cref="Program.ProvisioningBootstrapOnly"/>): the full observe/target/act
    /// instructions would teach tools this server refuses, so teach the two-step handoff instead.
    /// </summary>
    internal const string BootstrapInstructions =
        "This is MCEC's installed copy serving the provisioning BOOTSTRAP only: the sole tools are " +
        "provision-session and end-session; every other tool is refused with error.code:bootstrap-only. " +
        "To drive the desktop, call provision-session (requires the operator's 'Allow agents to provision " +
        "disposable instances' opt-in on File > Settings > Agent) to get a fresh, disposable, isolated MCEC " +
        "instance; it returns the directory, exePath, sessionId, and token. Launch \"<exePath>\" --mcp as a " +
        "stdio MCP server (or POST JSON-RPC to its HTTP mcpEndpoint with 'Authorization: Bearer <token>') " +
        "and do ALL observation and actuation against THAT instance; it serves the full tool surface and " +
        "the full connect-time instructions. When finished, stop the instance's mcec.exe, then call " +
        "end-session here with the sessionId AND token to delete it.";

    private JsonObject BuildInitializeResult() => new() {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["serverInfo"] = new JsonObject {
            ["name"] = "MCEC",
            ["version"] = Application.ProductVersion,
        },
        // Built-in agent guidance: surfaced to the model by the MCP client so it knows how to drive
        // MCEC effectively (the observe -> target -> act loop) and understands the security model. In
        // bootstrap mode (#296) the playbook would teach refused tools, so serve the handoff recipe.
        ["instructions"] = Program.ProvisioningBootstrapOnly ? BootstrapInstructions : _instructions(),
    };

    // -------------------------------------------------------------------------------------------
    // tools/list
    // -------------------------------------------------------------------------------------------

    private static JsonArray BuildToolsList() {
        // Provisioning bootstrap (#296): from the installed copy, list exactly what dispatch will
        // accept (AgentToolExecutor.CallTool refuses everything else with bootstrap-only), so an MCP
        // client's tool list never advertises a tool this server refuses.
        if (Program.ProvisioningBootstrapOnly) {
            return [BuildProvisionSessionTool(), BuildEndSessionTool()];
        }

        JsonArray tools = [];

        // The gated agent tools; one descriptor per tool, schema included; live in ToolCatalog (#205).
        // Each also accepts the optional per-call session-routing arg (#86 Phase 3), injected here in one
        // place rather than repeated in every catalog schema.
        foreach (ToolDescriptor descriptor in ToolCatalog.All) {
            JsonObject schema = descriptor.BuildSchema();
            AddSessionArg(schema);
            tools.Add(schema);
        }

        // META-TOOLS: deliberately NOT in the catalog, because they do not map 1:1 onto a Command in
        // the loaded table and have their own gating. send_command is the raw pass-through into the
        // CommandInvoker queue (transport-sensitive gate, #153); provision-session/end-session are the
        // isolated-session lifecycle (#138, gated by the operator's AllowSessionProvisioning);
        // session-start/status/end are the in-process agent-session lifecycle (#86 Phase 3). They are
        // special-cased here and in AgentToolExecutor.CallTool, right next to the catalog dispatch.
        JsonObject sendCommandSchema = ToolCatalog.Tool("send_command",
            "Send any raw MCEC command string to the existing command core (e.g. actuation commands).",
            new JsonObject { ["command"] = ToolCatalog.PropSchema("string", "The MCEC command string to enqueue") },
            ["command"]);
        AddSessionArg(sendCommandSchema); // send_command is routable like the catalog tools (#86)
        tools.Add(sendCommandSchema);

        // In-process agent-session lifecycle (#86 Phase 3). session-start hands back a fresh addressable
        // sessionId; session-status/end read `sessionId` as the TARGET session (not call routing), so they
        // are NOT given the injected routing arg above. NOTE: hyphenated names (session-start, not
        // session/start) because MCP/Anthropic tool names must match ^[a-zA-Z0-9_-]{1,64}$; '/' is invalid.
        tools.Add(ToolCatalog.Tool("session-start",
            "Start a new agent session and get its sessionId. Echo that sessionId on later tool calls to run them in this session (its own active target, last observation/action/error, and artifact directory); omit sessionId on a call to use the default session. Returns the new session's status. Use this to run independent multi-step tasks that must not share state.",
            [], []));
        tools.Add(ToolCatalog.Tool("session-status",
            "Report a session's state: active target window, last observation, last action, last error, artifact directory, and any emergency stop. Pass 'sessionId' to inspect a specific session (from session-start), or omit it for the default session.",
            new JsonObject { ["sessionId"] = ToolCatalog.PropSchema("string", "The session to inspect (from session-start); omit for the default session") },
            []));
        tools.Add(ToolCatalog.Tool("session-end",
            "End an agent session started with session-start, freeing its server-side state. Idempotent: ending an unknown/already-ended id reports ended:false rather than erroring. After this, a tool call echoing that sessionId is refused with unknown-session.",
            new JsonObject { ["sessionId"] = ToolCatalog.PropSchema("string", "The session to end (from session-start)") },
            ["sessionId"]));

        // Isolated session provisioning (#138). Requires the operator to have opted in
        // (AllowSessionProvisioning); it never mutates the installed config.
        tools.Add(BuildProvisionSessionTool());
        tools.Add(BuildEndSessionTool());

        return tools;
    }

    /// <summary>The <c>provision-session</c> tool schema (#138); shared by the full list and the bootstrap list (#296).</summary>
    private static JsonObject BuildProvisionSessionTool() {
        JsonObject provisionProps = new() {
            ["mcpServer"] = ToolCatalog.PropSchema("boolean", "Enable the provisioned instance's localhost MCP/HTTP server (default true)"),
            ["commands"] = new JsonObject {
                ["type"] = "array",
                ["description"] = "Command names to enable in the session (default: the agent observation/action set)",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        };
        return ToolCatalog.Tool("provision-session",
            "Get a fresh, disposable, isolated MCEC instance to run from instead of enabling the installed one. Returns a directory containing mcec.exe + an agent-ready co-located config (agent commands enabled ONLY inside the copy), plus how to launch/connect and the sessionId + token to tear it down. The token is the session credential: HTTP requests to the session's mcpEndpoint must send 'Authorization: Bearer <token>', and end-session requires it. Requires the operator to have enabled AllowSessionProvisioning; the installed config is never touched. Call end-session (or delete the directory) when finished.",
            provisionProps, []);
    }

    /// <summary>The <c>end-session</c> tool schema (#138); shared by the full list and the bootstrap list (#296).</summary>
    private static JsonObject BuildEndSessionTool() =>
        ToolCatalog.Tool("end-session",
            "Tear down a provisioned session (from provision-session) by deleting its directory. Requires the session's token; the teardown credential provision-session returned. Stop the session's mcec.exe first, or its files stay locked. MCEC also reaps stale session dirs on launch.",
            new JsonObject {
                ["sessionId"] = ToolCatalog.PropSchema("string", "The sessionId returned by provision-session"),
                ["token"] = ToolCatalog.PropSchema("string", "The token returned by provision-session (the teardown credential)"),
            },
            ["sessionId", "token"]);

    /// <summary>
    /// Adds the optional <c>sessionId</c> routing property (#86 Phase 3) to a tool's
    /// <c>inputSchema.properties</c>. It stays optional (never added to <c>required</c>) so omitting it
    /// keeps using the default session. Best-effort: a schema without the expected shape is left untouched.
    /// </summary>
    private static void AddSessionArg(JsonObject schema) {
        if (schema["inputSchema"] is JsonObject input && input["properties"] is JsonObject props) {
            props["sessionId"] = ToolCatalog.SessionArgProp();
        }
    }

    // -------------------------------------------------------------------------------------------
    // JSON-RPC response shapes
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

    private static string AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";
}
