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

    [Theory]
    // #113 concurrency contract: only global-input actuation serializes on InputLock. Observation runs
    // concurrently (so a long wait-for/query never blocks an unrelated capture); invoke is UIA actuation
    // dispatched on a worker with the modal grace, not under this lock.
    [InlineData("drag", true)]
    [InlineData("send_command", true)]
    [InlineData("query", false)]
    [InlineData("capture", false)]
    [InlineData("find", false)]
    [InlineData("wait-for", false)]
    [InlineData("record", false)]
    [InlineData("invoke", false)]
    public void SerializesOnInputLock_OnlyGlobalInputActuation(string tool, bool expected) {
        Assert.Equal(expected, AgentServer.SerializesOnInputLock(tool));
    }

    [Fact]
    public void Instructions_LoadsFromEmbeddedResource_CollapsedToParagraphs() {
        // The connect-time guidance is the single source of truth in src/Agent/AgentInstructions.md,
        // embedded into the exe. This proves it loads (a missing/misnamed resource throws) and is
        // collapsed to one line per blank-line-separated paragraph (the historical format).
        string s = AgentServer.Instructions;

        Assert.False(string.IsNullOrWhiteSpace(s));
        Assert.Contains("observe -> target -> act", s);   // the loop line
        Assert.Contains("COMPOSE:", s);                    // a mid section survived
        Assert.Contains("audit-logged on the host.", s);   // the last section survived
        Assert.DoesNotContain("\n\n", s);                  // paragraphs collapsed, no blank lines
    }

    [Fact]
    public void Dispatch_Initialize_IncludesTheInstructions() {
        JsonObject result = AgentServer.Dispatch(Request(1, "initialize"))!["result"]!.AsObject();
        Assert.Equal(AgentServer.Instructions, result["instructions"]!.GetValue<string>());
    }

    [Fact]
    public void RunStdioLoop_DispatchesConcurrently_ASlowRequestDoesNotBlockALaterOne() {
        // #113: the stdio transport must dispatch each request on a worker, or a slow call blocks later
        // ones. Two pipelined requests: id=1's dispatch blocks until id=2's runs and releases it. Under
        // concurrent dispatch, id=2 finishes first, so its response is written before id=1's. A serial
        // loop would run id=1 first, block waiting for id=2 (never dispatched), and time out — reversing
        // (and delaying) the order.
        using System.Threading.ManualResetEventSlim gate = new(false);
        Func<JsonObject, JsonObject?> dispatch = req => {
            long id = req["id"]!.GetValue<long>();
            if (id == 1) {
                gate.Wait(3000);
            }
            else {
                gate.Set();
            }
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = new JsonObject() };
        };
        System.IO.StringReader reader = new(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"x\"}\n" +
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"x\"}\n");
        System.IO.StringWriter writer = new();

        AgentServer.RunStdioLoop(reader, writer, dispatch);

        string output = writer.ToString();
        int p1 = output.IndexOf("\"id\":1", StringComparison.Ordinal);
        int p2 = output.IndexOf("\"id\":2", StringComparison.Ordinal);
        Assert.True(p1 >= 0 && p2 >= 0, $"both responses present; got: {output}");
        Assert.True(p2 < p1, $"id=2 (fast) must be written before id=1 (blocked) — proves concurrent dispatch; got: {output}");
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
