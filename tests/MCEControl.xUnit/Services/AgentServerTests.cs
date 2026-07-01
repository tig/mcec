// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

[Collection("AgentSerial")]
public class AgentServerTests {
    private static JsonObject Request(int id, string method, JsonObject? prms = null) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["method"] = method,
        ["params"] = prms ?? [],
    };

    [Fact]
    public void Dispatch_Initialize_ReturnsProtocolVersionAndServerInfo() {
        JsonObject resp = AgentServer.Dispatch(Request(1, "initialize"))!;

        Assert.NotNull(resp);
        JsonObject result = resp["result"]!.AsObject();

        string protocolVersion = result["protocolVersion"]!.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(protocolVersion));

        JsonObject serverInfo = result["serverInfo"]!.AsObject();
        Assert.Equal("MCEC", serverInfo["name"]!.GetValue<string>());
    }

    [Fact]
    public void Dispatch_ToolsList_IncludesAllAgentTools() {
        JsonObject resp = AgentServer.Dispatch(Request(2, "tools/list"))!;

        JsonArray tools = resp["result"]!.AsObject()["tools"]!.AsArray();

        List<string> names = [];
        foreach (JsonNode? tool in tools) {
            string? name = tool?["name"]?.GetValue<string>();
            if (name is not null) {
                names.Add(name);
            }
        }

        Assert.Contains("capture", names);
        Assert.Contains("query", names);
        Assert.Contains("find", names);
        Assert.Contains("wait-for", names);
        Assert.Contains("invoke", names);
        Assert.Contains("record", names);
        Assert.Contains("drag", names);
        Assert.Contains("send_command", names);
    }

    [Fact]
    public void Dispatch_ToolsList_DragTool_DeclaresFromToEndpoints() {
        JsonObject resp = AgentServer.Dispatch(Request(2, "tools/list"))!;
        JsonArray tools = resp["result"]!.AsObject()["tools"]!.AsArray();

        JsonObject? drag = null;
        foreach (JsonNode? tool in tools) {
            if (tool?["name"]?.GetValue<string>() == "drag") {
                drag = tool.AsObject();
                break;
            }
        }

        Assert.NotNull(drag);
        JsonObject schema = drag!["inputSchema"]!.AsObject();
        JsonObject props = schema["properties"]!.AsObject();
        Assert.True(props.ContainsKey("from"));
        Assert.True(props.ContainsKey("to"));
        Assert.True(props.ContainsKey("path"));

        List<string> required = [];
        foreach (JsonNode? r in schema["required"]!.AsArray()) {
            required.Add(r!.GetValue<string>());
        }
        Assert.Contains("from", required);
        Assert.Contains("to", required);
    }

    [Fact]
    public void Dispatch_ToolsCall_Capture_WhenAgentDisabled_ReportsError() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            JsonObject prms = new() {
                ["name"] = "capture",
                ["arguments"] = new JsonObject(),
            };

            JsonObject resp = AgentServer.Dispatch(Request(3, "tools/call", prms))!;
            string raw = resp.ToJsonString();

            // Tolerate either JSON-RPC shape: a top-level "error" object, an MCP result with
            // isError == true, or a result whose payload reports the blocked/failed capture.
            bool reportedError =
                resp["error"] is not null
                || (resp["result"] is JsonObject r && r["isError"]?.GetValueKind() == JsonValueKind.True)
                || raw.Contains("disabled", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("\"success\":false", StringComparison.Ordinal)
                || raw.Contains("\"success\": false", StringComparison.Ordinal);

            Assert.True(reportedError, $"Expected a blocked/failed tools/call result, got: {raw}");
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_WhenAgentDisabled_EmitsEnvelopeInTextContent() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            JsonObject prms = new() {
                ["name"] = "capture",
                ["arguments"] = new JsonObject(),
            };

            JsonObject resp = AgentServer.Dispatch(Request(4, "tools/call", prms))!;
            JsonObject result = resp["result"]!.AsObject();

            // MCP isError mirrors the envelope (isError = !ok).
            Assert.True(result["isError"]!.GetValue<bool>());

            // The first text content block is the #101 envelope: ok:false + a complete error.
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(result))!.AsObject();
            Assert.False(envelope["ok"]!.GetValue<bool>());
            JsonObject error = envelope["error"]!.AsObject();
            Assert.Equal("internal", error["category"]!.GetValue<string>());
            Assert.Equal("agent-commands-disabled", error["code"]!.GetValue<string>());
            Assert.False(envelope.ContainsKey("result"));
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_ResultCarriesAmbientSessionId() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // exercise the disabled path; it still rides a session
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            JsonObject prms = new() {
                ["name"] = "capture",
                ["arguments"] = new JsonObject(),
            };

            JsonObject resp = AgentServer.Dispatch(Request(5, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            // Every result — even a refused one — names the session it ran inside (#86).
            Assert.Equal(AgentRuntime.Session.SessionId, envelope["sessionId"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_Drag_IncompletePixelEndpoint_ReportsBadArguments() {
        // Regression: an endpoint with x but no y (or neither value nor coords) must be rejected, not
        // silently turned into (x, 0) and dragged — this tool generates real mouse input.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            JsonObject prms = new() {
                ["name"] = "drag",
                ["arguments"] = new JsonObject {
                    ["from"] = new JsonObject { ["x"] = 300 }, // missing y
                    ["to"] = new JsonObject { ["x"] = 400, ["y"] = 400 },
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(6, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.Equal("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_Drag_CompleteEndpoints_PassValidation() {
        // A well-formed drag (element + pixel endpoints) must get PAST argument validation. With no
        // Invoker wired in the test host it stops at the per-command enable gate — not bad-arguments —
        // which proves validation accepted the endpoints.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = null;
        try {
            JsonObject prms = new() {
                ["name"] = "drag",
                ["arguments"] = new JsonObject {
                    ["window"] = "Whatever",
                    ["from"] = new JsonObject { ["by"] = "name", ["value"] = "Slider" },
                    ["to"] = new JsonObject { ["x"] = 400, ["y"] = 400 },
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(7, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.NotEqual("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void InvokeFindTimeout_StaysBelowModalGrace_SoMissesAreNotReportedAsPendingModals() {
        // #107: invoke runs on a worker and is declared "modal pending" if it outlives the grace. If the
        // element lookup could take longer than the grace, an ordinary lookup miss (misspelled name, menu
        // not expanded) would be misreported as a pending modal success. The find must resolve within the
        // grace, so a worker still running afterward can only be a genuinely blocking action.
        Assert.True(
            UiaService.InvokeFindTimeoutMs < AgentServer.InvokeModalGraceMs,
            $"invoke find timeout ({UiaService.InvokeFindTimeoutMs}ms) must be < modal grace ({AgentServer.InvokeModalGraceMs}ms)");
    }

    private static string FirstTextBlock(JsonObject toolResult) {
        foreach (JsonNode? block in toolResult["content"]!.AsArray()) {
            if (block?["type"]?.GetValue<string>() == "text") {
                return block["text"]!.GetValue<string>();
            }
        }
        Assert.Fail("no text content block in tool result");
        return "";
    }
}
