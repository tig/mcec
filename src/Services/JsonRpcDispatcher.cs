// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;

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
                JsonObject callResult = _executor.CallTool(prms, transport);
                return Result(idNode, callResult);

            default:
                if (isNotification) {
                    return null;
                }
                return Error(idNode, -32601, $"Method not found: {method}");
        }
    }

    private JsonObject BuildInitializeResult() => new() {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["serverInfo"] = new JsonObject {
            ["name"] = "MCEC",
            ["version"] = System.Windows.Forms.Application.ProductVersion,
        },
        // Built-in agent guidance: surfaced to the model by the MCP client so it knows how to drive
        // MCEC effectively (the observe -> target -> act loop) and understands the security model.
        ["instructions"] = _instructions(),
    };

    // -------------------------------------------------------------------------------------------
    // tools/list
    // -------------------------------------------------------------------------------------------

    private static JsonArray BuildToolsList() {
        JsonArray tools = [];

        // The gated agent tools; one descriptor per tool, schema included; live in ToolCatalog (#205).
        foreach (ToolDescriptor descriptor in ToolCatalog.All) {
            tools.Add(descriptor.BuildSchema());
        }

        // META-TOOLS: deliberately NOT in the catalog, because they do not map 1:1 onto a Command in
        // the loaded table and have their own gating. send_command is the raw pass-through into the
        // CommandInvoker queue (transport-sensitive gate, #153); provision-session/end-session are the
        // isolated-session lifecycle (#138, gated by the operator's AllowSessionProvisioning). They are
        // special-cased here and in AgentToolExecutor.CallTool, right next to the catalog dispatch.
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
            "Get a fresh, disposable, isolated MCEC instance to run from instead of enabling the installed one. Returns a directory containing mcec.exe + an agent-ready co-located config (agent commands enabled ONLY inside the copy), plus how to launch/connect and the sessionId + token to tear it down. The token is the session credential: HTTP requests to the session's mcpEndpoint must send 'Authorization: Bearer <token>', and end-session requires it. Requires the operator to have enabled AllowSessionProvisioning; the installed config is never touched. Call end-session (or delete the directory) when finished.",
            provisionProps, []));

        tools.Add(ToolCatalog.Tool("end-session",
            "Tear down a provisioned session (from provision-session) by deleting its directory. Requires the session's token; the teardown credential provision-session returned. Stop the session's mcec.exe first, or its files stay locked. MCEC also reaps stale session dirs on launch.",
            new JsonObject {
                ["sessionId"] = ToolCatalog.PropSchema("string", "The sessionId returned by provision-session"),
                ["token"] = ToolCatalog.PropSchema("string", "The token returned by provision-session (the teardown credential)"),
            },
            ["sessionId", "token"]));

        return tools;
    }

    // -------------------------------------------------------------------------------------------
    // JSON-RPC response shapes
    // -------------------------------------------------------------------------------------------

    internal static JsonObject Result(JsonNode? id, JsonNode result) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result,
    };

    internal static JsonObject Error(JsonNode? id, int code, string message) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private static string AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";
}
