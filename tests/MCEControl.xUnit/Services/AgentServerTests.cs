// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
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
    public void Dispatch_ToolsList_IncludesCaptureAndQuery() {
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
    public void Dispatch_ToolsCall_Capture_WhenCommandDisabledInTable_IsBlocked() {
        // CR P1: even with AgentCommandsEnabled, the MCP path must honor the per-command Enabled flag
        // (the documented second gate). A disabled `capture` in the command table must not run.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        CommandInvoker invoker = new();
        invoker["capture"] = new CaptureCommand { Cmd = "capture", Enabled = false };
        AgentRuntime.Invoker = invoker;
        try {
            JsonObject prms = new() { ["name"] = "capture", ["arguments"] = new JsonObject() };
            JsonObject resp = AgentServer.Dispatch(Request(60, "tools/call", prms))!;

            Assert.True(resp["result"]!["isError"]!.GetValue<bool>());
            Assert.Contains("disabled", resp.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }

    [Fact]
    public void Dispatch_ToolsCall_Capture_WhenCommandEnabledInTable_PassesPerCommandGate() {
        // With the per-command flag enabled, the MCP path runs the command. With no target args it
        // fails for "No matching window" (proving it executed) — NOT the "disabled" gate message.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        CommandInvoker invoker = new();
        invoker["capture"] = new CaptureCommand { Cmd = "capture", Enabled = true };
        AgentRuntime.Invoker = invoker;
        try {
            JsonObject prms = new() { ["name"] = "capture", ["arguments"] = new JsonObject() };
            JsonObject resp = AgentServer.Dispatch(Request(61, "tools/call", prms))!;
            string raw = resp.ToJsonString();

            Assert.DoesNotContain("is disabled", raw, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No matching window", raw, StringComparison.Ordinal);
        }
        finally {
            AgentRuntime.Settings = null;
            AgentRuntime.Invoker = null;
        }
    }
}
