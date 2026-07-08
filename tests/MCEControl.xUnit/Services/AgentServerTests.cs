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
    [InlineData("focus", true)]
    [InlineData("send_command", true)]
    [InlineData("query", false)]
    [InlineData("capture", false)]
    [InlineData("find", false)]
    [InlineData("wait-for", false)]
    [InlineData("record", false)]
    [InlineData("invoke", false)]
    [InlineData("click", false)]
    [InlineData("displays", false)]
    [InlineData("launch", false)]
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
    public void Dispatch_Initialize_IncludesCommandAccessDefaults() {
        // #324: connect-time discovery of the gated set, so an agent can batch one request-command-access
        // instead of probing command-disabled per command. The map is derived from the ToolCatalog's
        // ProvisionedByDefault flags (single source of truth), so today only `launch` is gated among tools.
        JsonObject result = AgentServer.Dispatch(Request(1, "initialize"))!["result"]!.AsObject();
        JsonObject access = result["commandAccess"]!.AsObject();

        List<string> enabled = Names(access["enabledTools"]!.AsArray());
        List<string> gated = Names(access["gatedTools"]!.AsArray());
        Assert.Contains("capture", enabled);
        Assert.Contains("invoke", enabled);
        Assert.Contains("launch", gated);
        Assert.DoesNotContain("launch", enabled);
        // No raw send_command built-in (chars:, winr, …) is provisioned by default, so the enabled-raw list
        // is empty at connect time.
        Assert.Empty(Names(access["enabledRawCommands"]!.AsArray()));
        Assert.False(string.IsNullOrWhiteSpace(access["note"]!.GetValue<string>()));

        // Every gated tool the map names is a real advertised tool the agent could request (#324): the
        // list must be derived, never a hand-typed set that could name a tool that no longer exists.
        foreach (string name in gated) {
            Assert.True(ToolCatalog.Contains(name), $"gated tool '{name}' should be a real catalog tool");
        }
    }

    private static List<string> Names(JsonArray arr) {
        List<string> names = [];
        foreach (JsonNode? n in arr) {
            if (n?.GetValue<string>() is { } s) {
                names.Add(s);
            }
        }
        return names;
    }

    [Fact]
    public void StdioLoop_DispatchesRequestsConcurrently_NotOneAtATime() {
        // #113: the stdio transport must dispatch each request on its own worker, or a slow call blocks
        // later ones. Two requests rendezvous: each signals its arrival in dispatch and waits for the
        // other. If the loop dispatched serially, the first would wait alone and time out (met=1); only
        // concurrent dispatch lets both meet (met=2). Deterministic; the count gates on an actual
        // rendezvous, not on write order or wall-clock speed.
        using ManualResetEventSlim aArrived = new(false);
        using ManualResetEventSlim bArrived = new(false);
        int metTheOther = 0;
        JsonObject? Dispatch(JsonObject req) {
            long id = req["id"]!.GetValue<long>();
            bool sawOther;
            if (id == 1) {
                aArrived.Set();
                sawOther = bArrived.Wait(3000);
            }
            else {
                sawOther = aArrived.Wait(3000);
                bArrived.Set();
            }
            if (sawOther) {
                Interlocked.Increment(ref metTheOther);
            }
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = new JsonObject() };
        }
        StringReader reader = new(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"x\"}\n" +
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"x\"}\n");
        StringWriter writer = new();

        new McpStdioTransport(Dispatch).Run(reader, writer);

        string output = writer.ToString();
        Assert.Equal(2, metTheOther); // both dispatches were in flight at once => concurrent
        Assert.Contains("\"id\":1", output, StringComparison.Ordinal);
        Assert.Contains("\"id\":2", output, StringComparison.Ordinal);
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
        Assert.Contains("displays", names);
        Assert.Contains("find", names);
        Assert.Contains("wait-for", names);
        Assert.Contains("invoke", names);
        Assert.Contains("record", names);
        Assert.Contains("drag", names);
        Assert.Contains("click", names);
        Assert.Contains("clipboard", names);
        Assert.Contains("send_command", names);
    }

    [Fact]
    public void Dispatch_ToolsList_ClickTool_DeclaresAtEndpoint() {
        JsonObject resp = AgentServer.Dispatch(Request(2, "tools/list"))!;
        JsonArray tools = resp["result"]!.AsObject()["tools"]!.AsArray();

        JsonObject? click = null;
        foreach (JsonNode? tool in tools) {
            if (tool?["name"]?.GetValue<string>() == "click") {
                click = tool.AsObject();
                break;
            }
        }

        Assert.NotNull(click);
        JsonObject schema = click["inputSchema"]!.AsObject();
        JsonObject props = schema["properties"]!.AsObject();
        Assert.True(props.ContainsKey("at"));
        Assert.True(props.ContainsKey("button"));
        Assert.True(props.ContainsKey("count"));
        Assert.Contains("window-relative", props["at"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);

        List<string> required = [];
        foreach (JsonNode? r in schema["required"]!.AsArray()) {
            required.Add(r!.GetValue<string>());
        }
        Assert.Contains("at", required);
    }

    [Fact]
    public void Dispatch_Click_IncompletePixelEndpoint_ReportsBadArguments() {
        // Regression mirror of the drag guard: an 'at' with x but no y (or neither value nor coords) must
        // be rejected, not silently turned into (x, 0) and clicked; this tool generates real mouse input.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            JsonObject prms = new() {
                ["name"] = "click",
                ["arguments"] = new JsonObject {
                    ["at"] = new JsonObject { ["x"] = 300 }, // missing y
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(8, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.Equal("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_Focus_IncompletePixelEndpoint_ReportsBadArguments() {
        // #272 CR (P2): focus synthesizes a real click, so a pixel `at` with x but no y must be rejected
        // up front, not turned into (x, 0) and clicked. `at` is optional for focus, but a PRESENT pixel
        // endpoint must be complete.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            JsonObject prms = new() {
                ["name"] = "focus",
                ["arguments"] = new JsonObject {
                    ["window"] = "Whatever",
                    ["at"] = new JsonObject { ["x"] = 400 }, // missing y
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(20, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.Equal("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_Focus_NoEndpoint_PassesValidation() {
        // Omitting `at` is a valid window-only focus (foreground + confirm), NOT a malformed endpoint; it
        // must get PAST argument validation and stop at the per-command enable gate.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = null;
        try {
            JsonObject prms = new() {
                ["name"] = "focus",
                ["arguments"] = new JsonObject { ["window"] = "Whatever" },
            };

            JsonObject resp = AgentServer.Dispatch(Request(21, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.NotEqual("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_Focus_CompletePixelEndpoint_PassesValidation() {
        // A full integer pixel endpoint is well-formed and must pass validation (stops at the enable gate).
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = null;
        try {
            JsonObject prms = new() {
                ["name"] = "focus",
                ["arguments"] = new JsonObject {
                    ["window"] = "Whatever",
                    ["at"] = new JsonObject { ["x"] = 400, ["y"] = 250 },
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(22, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.NotEqual("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_Click_CompleteEndpoint_PassesValidation() {
        // A well-formed click (element endpoint) must get PAST argument validation. With no Invoker wired
        // in the test host it stops at the per-command enable gate (not bad-arguments), which proves
        // validation accepted the endpoint (and never actuates real input).
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = null;
        try {
            JsonObject prms = new() {
                ["name"] = "click",
                ["arguments"] = new JsonObject {
                    ["window"] = "Whatever",
                    ["at"] = new JsonObject { ["by"] = "name", ["value"] = "OK" },
                },
            };

            JsonObject resp = AgentServer.Dispatch(Request(9, "tools/call", prms))!;
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

            Assert.False(envelope["ok"]!.GetValue<bool>());
            Assert.NotEqual("bad-arguments", envelope["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
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
        JsonObject schema = drag["inputSchema"]!.AsObject();
        JsonObject props = schema["properties"]!.AsObject();
        Assert.True(props.ContainsKey("from"));
        Assert.True(props.ContainsKey("to"));
        Assert.True(props.ContainsKey("path"));
        Assert.Contains("window-relative", props["from"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("window-relative", props["to"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);

        List<string> required = [];
        foreach (JsonNode? r in schema["required"]!.AsArray()) {
            required.Add(r!.GetValue<string>());
        }
        Assert.Contains("from", required);
        Assert.Contains("to", required);
    }

    [Fact]
    public void Dispatch_ToolsList_CaptureTool_DeclaresPathOnlyAndDownscaleOptions() {
        JsonObject resp = AgentServer.Dispatch(Request(2, "tools/list"))!;
        JsonArray tools = resp["result"]!.AsObject()["tools"]!.AsArray();

        JsonObject? capture = null;
        foreach (JsonNode? tool in tools) {
            if (tool?["name"]?.GetValue<string>() == "capture") {
                capture = tool.AsObject();
                break;
            }
        }
        Assert.NotNull(capture);
        JsonObject props = capture["inputSchema"]!.AsObject()["properties"]!.AsObject();

        Assert.True(props.ContainsKey("maxWidth"));
        Assert.True(props.ContainsKey("scale"));
        Assert.True(props.ContainsKey("returnImage"));
        Assert.True(props.ContainsKey("pathOnly"));
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

            // Every result; even a refused one; names the session it ran inside (#86).
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
        // silently turned into (x, 0) and dragged; this tool generates real mouse input.
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
        // Invoker wired in the test host it stops at the per-command enable gate (not bad-arguments);
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
        //
        // #262 review (Codex P2): the invoke lookup enumerates matches (FindAllDescendants) to detect an
        // ambiguous selector. The poll loop never STARTS an attempt past InvokeFindTimeoutMs, so the
        // worst-case overshoot is a single in-flight scan; the grace must clear the find timeout by more
        // than that scan can take, or a slow scan on a large tree gets misreported as a pending modal.
        // Pin a minimum headroom so the two constants can never drift close together.
        const int MinScanHeadroomMs = 200;
        Assert.True(
            AgentServer.InvokeModalGraceMs - UiaService.InvokeFindTimeoutMs >= MinScanHeadroomMs,
            $"modal grace ({AgentServer.InvokeModalGraceMs}ms) must exceed the invoke find timeout " +
            $"({UiaService.InvokeFindTimeoutMs}ms) by >= {MinScanHeadroomMs}ms so one in-flight " +
            "FindAll scan cannot outlast the grace and be misreported as a pending modal (#262).");
    }

    // --- #74: MCP tools honor the per-command Enabled gate in mcec.commands (a SECOND gate, independent
    // of AgentCommandsEnabled). Enabling the observation surface must not enable every individual command.

    private static string ToolErrorCode(JsonObject dispatchResponse) {
        JsonObject envelope = JsonNode.Parse(FirstTextBlock(dispatchResponse["result"]!.AsObject()))!.AsObject();
        return envelope["error"]?["code"]?.GetValue<string>() ?? "";
    }

    [Fact]
    public void Dispatch_ToolsCall_WhenCommandDisabledInTable_IsRefused_EvenWithAgentCommandsEnabled() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = new CommandInvoker { ["capture"] = new CaptureCommand { Cmd = "capture", Enabled = false } };
        try {
            JsonObject resp = AgentServer.Dispatch(Request(20, "tools/call",
                new JsonObject { ["name"] = "capture", ["arguments"] = new JsonObject() }))!;

            // AgentCommandsEnabled is on, but the per-command gate is off; the call must be refused.
            Assert.Equal("command-disabled", ToolErrorCode(resp));
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_WhenCommandEnabledInTable_PassesThePerCommandGate() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = new CommandInvoker { ["capture"] = new CaptureCommand { Cmd = "capture", Enabled = true } };
        try {
            JsonObject resp = AgentServer.Dispatch(Request(21, "tools/call",
                new JsonObject { ["name"] = "capture", ["arguments"] = new JsonObject() }))!;

            // With the command enabled the tool runs; with no target it fails no-target; the point is it
            // gets PAST the gate, so the error is not the gate refusal.
            Assert.NotEqual("command-disabled", ToolErrorCode(resp));
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_SendCommand_OverStdio_IsNotGatedByAgentCommandsEnabled() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // AgentCommandsEnabled = false
        try {
            // #153: over the LOCAL stdio transport (the default), send_command keeps its documented raw
            // pass-through; it is not gated by AgentCommandsEnabled. An empty command therefore returns
            // bad-arguments; proving it reached RunSendCommand; not agent-commands-disabled. (The raw
            // command it runs is still gated by that command's own Enabled flag in the table.)
            JsonObject resp = AgentServer.Dispatch(Request(22, "tools/call",
                new JsonObject { ["name"] = "send_command", ["arguments"] = new JsonObject { ["command"] = "" } }))!;

            Assert.Equal("bad-arguments", ToolErrorCode(resp));
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_SendCommand_OverHttp_WhenAgentDisabled_IsRefused() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // AgentCommandsEnabled = false
        AgentRuntime.Invoker = [];
        try {
            // #153: over the network-facing HTTP transport, send_command honors the AgentCommandsEnabled
            // gate. A NON-empty command with a live invoker present would execute (and pass the empty-arg
            // check) if the gate were absent; instead the gate refuses it up front with agent-commands-disabled.
            JsonObject resp = AgentServer.Dispatch(Request(23, "tools/call",
                new JsonObject { ["name"] = "send_command", ["arguments"] = new JsonObject { ["command"] = "chars:pwnd" } }),
                AgentTransport.Http)!;

            Assert.Equal("agent-commands-disabled", ToolErrorCode(resp));
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_SendCommand_OverHttp_WhenAgentEnabled_ReachesPassThrough() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = [];
        try {
            // #153: with the agent surface opted in, send_command over HTTP gets past the gate and runs the
            // pass-through. An empty command now surfaces bad-arguments (from RunSendCommand); NOT the gate
            // refusal; proving the request reached execution.
            JsonObject resp = AgentServer.Dispatch(Request(24, "tools/call",
                new JsonObject { ["name"] = "send_command", ["arguments"] = new JsonObject { ["command"] = "" } }),
                AgentTransport.Http)!;

            Assert.Equal("bad-arguments", ToolErrorCode(resp));
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    // --- #201: BuildCommand's arg-mapping switch is exhaustive. A tool name that passed the
    // tools/call gate but has no mapping must be refused with a structured unknown-tool error;
    // historically the switch's default arm silently mapped it onto InvokeCommand (an ACTUATION)
    // with garbage selector args.

    [Fact]
    public void RunAgentCommand_NameWithNoArgMapping_ReportsUnknownTool_NotInvokeExecution() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        // Even with a matching, ENABLED entry in the command table; i.e. a name that passes every
        // gate; a name the switch does not map must be refused, never run as another command.
        AgentRuntime.Invoker = new CommandInvoker { ["hover"] = new CaptureCommand { Cmd = "hover", Enabled = true } };
        try {
            JsonObject resp = AgentServer.RunAgentCommand("hover", []);

            Assert.True(resp["isError"]!.GetValue<bool>());
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp))!.AsObject();
            Assert.False(envelope["ok"]!.GetValue<bool>());
            JsonObject error = envelope["error"]!.AsObject();
            Assert.Equal("unknown-tool", error["code"]!.GetValue<string>());
            Assert.Equal("internal", error["category"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    // --- #206: RunAgentCommand consumes the CommandResult OBJECT the command handed to its
    // CapturingReply; no JsonNode.Parse of its own output, and no "non-JSON output is success"
    // fallback. `displays` is the one agent tool that runs headlessly end-to-end.

    [Fact]
    public void RunAgentCommand_Displays_FlowsTheTypedResultIntoTheEnvelope() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = new CommandInvoker { ["displays"] = new DisplaysCommand { Cmd = "displays", Enabled = true } };
        try {
            JsonObject resp = AgentServer.RunAgentCommand("displays", []);

            Assert.False(resp["isError"]!.GetValue<bool>());
            JsonObject envelope = JsonNode.Parse(FirstTextBlock(resp))!.AsObject();
            Assert.True(envelope["ok"]!.GetValue<bool>());
            // The command's Data payload IS the envelope's result; the object pipeline end-to-end.
            Assert.True(envelope["result"]!.AsObject().ContainsKey("displays"));
            Assert.True(envelope["result"]!["count"]!.GetValue<int>() > 0);
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void RunAgentCommand_WindowNotFound_ReportsStructuredCode_NotProse() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.Invoker = new CommandInvoker { ["query"] = new QueryCommand { Cmd = "query", Enabled = true } };
        try {
            JsonObject resp = AgentServer.RunAgentCommand("query",
                new JsonObject { ["window"] = $"no-such-window-{Guid.NewGuid():N}" });

            Assert.True(resp["isError"]!.GetValue<bool>());
            JsonObject error = JsonNode.Parse(FirstTextBlock(resp))!.AsObject()["error"]!.AsObject();
            // Pinned by CODE and CATEGORY; never by the human-readable message (#206).
            Assert.Equal("window-not-found", error["code"]!.GetValue<string>());
            Assert.Equal("no-target", error["category"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void BuildCommand_UnknownName_ReturnsNull_NeverADefaultCommand() {
        // The old default arm meant an unmapped name became an InvokeCommand. Now it maps to null,
        // which RunAgentCommand refuses as unknown-tool.
        Assert.Null(AgentServer.BuildCommand("hover", new JsonObject { ["window"] = "X", ["value"] = "OK" }));
    }

    [Fact]
    public void BuildCommand_Invoke_StillMapsToInvokeCommand() {
        // "invoke" has its own explicit case (#201); the mapping must be unchanged.
        Command? cmd = AgentServer.BuildCommand("invoke", new JsonObject {
            ["window"] = "Calculator",
            ["by"] = "automationId",
            ["value"] = "num7Button",
            ["action"] = "toggle",
            ["text"] = "hi",
        });

        InvokeCommand invoke = Assert.IsType<InvokeCommand>(cmd);
        Assert.Equal("Calculator", invoke.Window);
        Assert.Equal("automationId", invoke.By);
        Assert.Equal("num7Button", invoke.Value);
        Assert.Equal("toggle", invoke.Action);
        Assert.Equal("hi", invoke.Text);
    }

    // -------------------------------------------------------------------------------------------
    // #86 Phase 3: session lifecycle tools (session-start/status/end) + per-call sessionId routing.
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Dispatch_ToolsList_AdvertisesSessionLifecycleAndRoutingArg() {
        JsonObject resp = AgentServer.Dispatch(Request(2, "tools/list"))!;
        JsonArray tools = resp["result"]!.AsObject()["tools"]!.AsArray();

        Dictionary<string, JsonObject> byName = [];
        foreach (JsonNode? tool in tools) {
            if (tool?["name"]?.GetValue<string>() is { } n) {
                byName[n] = tool.AsObject();
            }
        }

        // The three lifecycle tools are advertised (hyphenated, not session/start; '/' is invalid in an
        // MCP/Anthropic tool name).
        Assert.Contains("session-start", byName.Keys);
        Assert.Contains("session-status", byName.Keys);
        Assert.Contains("session-end", byName.Keys);

        // Routable tools (catalog + send_command) advertise an OPTIONAL sessionId arg.
        foreach (string routable in (string[])["capture", "invoke", "drag", "send_command"]) {
            JsonObject schema = byName[routable]["inputSchema"]!.AsObject();
            Assert.True(schema["properties"]!.AsObject().ContainsKey("sessionId"), $"{routable} should advertise sessionId");
            Assert.DoesNotContain("sessionId", Required(schema));
        }

        // session-end targets a session, so its sessionId is REQUIRED; session-status's is optional;
        // session-start takes none (it never routes; it mints a new id).
        Assert.Contains("sessionId", Required(byName["session-end"]["inputSchema"]!.AsObject()));
        Assert.True(byName["session-status"]["inputSchema"]!.AsObject()["properties"]!.AsObject().ContainsKey("sessionId"));
        Assert.DoesNotContain("sessionId", Required(byName["session-status"]["inputSchema"]!.AsObject()));
        Assert.False(byName["session-start"]["inputSchema"]!.AsObject()["properties"]!.AsObject().ContainsKey("sessionId"));
    }

    [Fact]
    public void Dispatch_SessionStart_MintsFreshAddressableSessionDistinctFromDefault() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            string defaultId = AgentRuntime.Session.SessionId;

            JsonObject env = CallEnvelope(40, "session-start");
            Assert.True(env["ok"]!.GetValue<bool>());
            string started = env["result"]!.AsObject()["sessionId"]!.GetValue<string>();

            // The started session names itself on the envelope, is a real new id, and is NOT the default.
            Assert.Equal(started, env["sessionId"]!.GetValue<string>());
            Assert.NotEqual(defaultId, started);

            // Two starts give two different sessions.
            string started2 = CallEnvelope(41, "session-start")["result"]!.AsObject()["sessionId"]!.GetValue<string>();
            Assert.NotEqual(started, started2);
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_SessionId_RoutesTheCall_AndEchoesTheRoutedId() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            string started = CallEnvelope(42, "session-start")["result"]!.AsObject()["sessionId"]!.GetValue<string>();

            // A tool call echoing that sessionId runs in (and names) that session; the command itself is
            // disabled (no loaded table), but the refusal still carries the ROUTED session id.
            JsonObject routed = CallEnvelope(43, "capture", new JsonObject { ["sessionId"] = started });
            Assert.Equal(started, routed["sessionId"]!.GetValue<string>());

            // A call with no sessionId falls back to the default session, a different id.
            JsonObject defaulted = CallEnvelope(44, "capture");
            Assert.Equal(AgentRuntime.Session.SessionId, defaulted["sessionId"]!.GetValue<string>());
            Assert.NotEqual(started, defaulted["sessionId"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_UnknownSessionId_IsRefused_NotSilentlyForked() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            JsonObject env = CallEnvelope(45, "capture", new JsonObject { ["sessionId"] = "deadbeefcafe" });
            Assert.False(env["ok"]!.GetValue<bool>());
            JsonObject error = env["error"]!.AsObject();
            Assert.Equal("unknown-session", error["code"]!.GetValue<string>());
            Assert.Equal("invalid-argument", error["category"]!.GetValue<string>());

            // The refusal echoes the REJECTED id, never the default session; else a client that carries
            // envelope.sessionId forward after an error would silently continue the independent task in the
            // default session, cross-contaminating the state the explicit session was meant to isolate (CR).
            Assert.Equal("deadbeefcafe", env["sessionId"]!.GetValue<string>());
            Assert.NotEqual(AgentRuntime.Session.SessionId, env["sessionId"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_WhitespaceOnlySessionId_IsRefused_NotRoutedToDefault() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            // A whitespace-only id is non-empty content, not an omitted arg: refuse it as unknown rather
            // than silently routing to the default session (which would fork/cross-contaminate state) (CR).
            JsonObject env = CallEnvelope(54, "capture", new JsonObject { ["sessionId"] = "   " });
            Assert.False(env["ok"]!.GetValue<bool>());
            Assert.Equal("unknown-session", env["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_SessionStatus_ReturnsSessionState() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            string started = CallEnvelope(46, "session-start")["result"]!.AsObject()["sessionId"]!.GetValue<string>();

            JsonObject env = CallEnvelope(47, "session-status", new JsonObject { ["sessionId"] = started });
            Assert.True(env["ok"]!.GetValue<bool>());
            JsonObject status = env["result"]!.AsObject();
            Assert.Equal(started, status["sessionId"]!.GetValue<string>());
            Assert.True(status.ContainsKey("startedAt"));
            Assert.True(status.ContainsKey("artifactDir"));

            // Omitting sessionId reports the DEFAULT session.
            JsonObject def = CallEnvelope(48, "session-status");
            Assert.Equal(AgentRuntime.Session.SessionId, def["result"]!.AsObject()["sessionId"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_SessionStatus_IncludesLiveCommandAccess_ReflectingTheTable() {
        // #324: unlike initialize (the provisioning DEFAULTS), session-status reports the LIVE table, so a
        // command the operator has since granted shows as enabled. Here `launch` is enabled in the table (a
        // grant would look the same), so it must appear enabled and NOT gated; `invoke` absent from the table
        // stays gated.
        //
        // #340 CR: `chars:` is granted but `mouse:` is not. A partial raw grant must report ONLY chars: in
        // enabledRawCommands — never imply the whole raw send_command surface (mouse:, winr, …) is open, which
        // a single canary boolean did. `launch` is a catalog tool, so it belongs in enabledTools, NOT the raw
        // list.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        AgentRuntime.Invoker = new CommandInvoker {
            ["launch"] = new LaunchCommand { Cmd = "launch", Enabled = true },
            ["chars:"] = new CharsCommand { Cmd = "chars:", Enabled = true },
            ["mouse:"] = new MouseCommand { Cmd = "mouse:", Enabled = false },
        };
        try {
            JsonObject access = CallEnvelope(55, "session-status")["result"]!.AsObject()["commandAccess"]!.AsObject();
            List<string> enabled = Names(access["enabledTools"]!.AsArray());
            List<string> gated = Names(access["gatedTools"]!.AsArray());
            List<string> enabledRaw = Names(access["enabledRawCommands"]!.AsArray());

            Assert.Contains("launch", enabled);   // enabled in the table (a grant would read the same)
            Assert.DoesNotContain("launch", gated);
            Assert.Contains("invoke", gated);      // absent from the table → gated

            // The partial raw grant is honest: chars: present, mouse: (disabled) absent, and the catalog
            // launch is NOT double-reported as a raw command.
            Assert.Contains("chars:", enabledRaw);
            Assert.DoesNotContain("mouse:", enabledRaw);
            Assert.DoesNotContain("launch", enabledRaw);
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_SessionEnd_RemovesSession_ThenRoutingItIsUnknown_AndReEndIsIdempotent() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.ArtifactRoot = Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());
        AgentRuntime.ResetSession();
        try {
            string started = CallEnvelope(49, "session-start")["result"]!.AsObject()["sessionId"]!.GetValue<string>();

            JsonObject ended = CallEnvelope(50, "session-end", new JsonObject { ["sessionId"] = started });
            Assert.True(ended["ok"]!.GetValue<bool>());
            Assert.True(ended["result"]!.AsObject()["ended"]!.GetValue<bool>());
            // The envelope names the ended session (metadata; consistent with session-status stamping its
            // inspected session), so a client can correlate the result with the session it ended (CR).
            Assert.Equal(started, ended["sessionId"]!.GetValue<string>());

            // The id no longer resolves: a routed call is refused.
            JsonObject afterEnd = CallEnvelope(51, "capture", new JsonObject { ["sessionId"] = started });
            Assert.False(afterEnd["ok"]!.GetValue<bool>());
            Assert.Equal("unknown-session", afterEnd["error"]!.AsObject()["code"]!.GetValue<string>());

            // Re-ending is idempotent (ended:false, not an error).
            JsonObject reEnd = CallEnvelope(52, "session-end", new JsonObject { ["sessionId"] = started });
            Assert.True(reEnd["ok"]!.GetValue<bool>());
            Assert.False(reEnd["result"]!.AsObject()["ended"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    [Fact]
    public void Dispatch_SessionLifecycle_RequiresAgentCommandsEnabled() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent surface not opted in
        AgentRuntime.ResetSession();
        try {
            JsonObject env = CallEnvelope(53, "session-start");
            Assert.False(env["ok"]!.GetValue<bool>());
            Assert.Equal("agent-commands-disabled", env["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.ResetSession();
        }
    }

    /// <summary>Dispatches a tools/call and returns the #101 envelope from its first text content block.</summary>
    private static JsonObject CallEnvelope(int id, string name, JsonObject? arguments = null) {
        JsonObject prms = new() { ["name"] = name, ["arguments"] = arguments ?? [] };
        JsonObject resp = AgentServer.Dispatch(Request(id, "tools/call", prms))!;
        return JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();
    }

    /// <summary>The <c>required</c> array of an inputSchema as a list of strings.</summary>
    private static List<string> Required(JsonObject inputSchema) {
        List<string> required = [];
        if (inputSchema["required"] is JsonArray arr) {
            foreach (JsonNode? r in arr) {
                if (r?.GetValue<string>() is { } s) {
                    required.Add(s);
                }
            }
        }
        return required;
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
