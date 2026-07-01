// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests the two agent safety features at the MCP dispatch boundary: the emergency-stop (#135) latch
/// refuses every tool call, and isolated session provisioning (#138) is gated on the operator opt-in.
/// </summary>
[Collection("AgentSerial")]
public class AgentServerSafetyTests {
    private static JsonObject Request(int id, string method, JsonObject? prms = null) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["method"] = method,
        ["params"] = prms ?? [],
    };

    private static JsonObject Call(int id, string tool, JsonObject args) =>
        AgentServer.Dispatch(Request(id, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = args }))!;

    private static JsonObject Envelope(JsonObject resp) =>
        JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

    private static string FirstTextBlock(JsonObject toolResult) {
        foreach (JsonNode? block in toolResult["content"]!.AsArray()) {
            if (block?["type"]?.GetValue<string>() == "text") {
                return block["text"]!.GetValue<string>();
            }
        }
        Assert.Fail("no text content block in tool result");
        return "";
    }

    [Fact]
    public void EmergencyStopped_RefusesEveryToolCall() {
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        AgentRuntime.SetEmergencyStopped(true);
        try {
            JsonObject resp = Call(1, "capture", new JsonObject { ["foreground"] = true });
            JsonObject env = Envelope(resp);

            Assert.False(env["ok"]!.GetValue<bool>());
            Assert.Equal("emergency-stopped", env["error"]!.AsObject()["code"]!.GetValue<string>());
            Assert.True(resp["result"]!.AsObject()["isError"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.SetEmergencyStopped(false);
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void EmergencyStopped_RefusesSendCommandToo() {
        AgentRuntime.SetEmergencyStopped(true);
        try {
            JsonObject env = Envelope(Call(2, "send_command", new JsonObject { ["command"] = "winr" }));
            Assert.Equal("emergency-stopped", env["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.SetEmergencyStopped(false);
        }
    }

    [Fact]
    public void ToolsList_IncludesProvisioningTools() {
        JsonArray tools = AgentServer.Dispatch(Request(3, "tools/list"))!["result"]!.AsObject()["tools"]!.AsArray();
        bool hasProvision = false, hasEnd = false;
        foreach (JsonNode? tool in tools) {
            string? name = tool?["name"]?.GetValue<string>();
            hasProvision |= name == "provision-session";
            hasEnd |= name == "end-session";
        }
        Assert.True(hasProvision, "provision-session tool missing");
        Assert.True(hasEnd, "end-session tool missing");
    }

    [Fact]
    public void ProvisionSession_NotAuthorized_IsRefused() {
        AgentRuntime.Settings = new AppSettings { AllowSessionProvisioning = false };
        try {
            JsonObject env = Envelope(Call(4, "provision-session", []));
            Assert.False(env["ok"]!.GetValue<bool>());
            Assert.Equal("provisioning-not-authorized", env["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void ProvisionSession_Authorized_ReturnsIsolatedDirectory_AndEndSessionTearsItDown() {
        string origRoot = SessionProvisioner.SessionsRoot;
        string origBin = SessionProvisioner.BinariesDir;
        string baseTemp = Path.Combine(Path.GetTempPath(), "mcec-provision-gate-test", Path.GetRandomFileName());
        string fakeBin = Path.Combine(baseTemp, "install");
        Directory.CreateDirectory(fakeBin);
        File.WriteAllText(Path.Combine(fakeBin, "mcec.exe"), "stub");

        AgentRuntime.Settings = new AppSettings { AllowSessionProvisioning = true };
        SessionProvisioner.SessionsRoot = Path.Combine(baseTemp, "sessions");
        SessionProvisioner.BinariesDir = fakeBin;
        try {
            JsonObject env = Envelope(Call(5, "provision-session", new JsonObject { ["mcpServer"] = false }));
            Assert.True(env["ok"]!.GetValue<bool>());

            JsonObject result = env["result"]!.AsObject();
            string dir = result["directory"]!.GetValue<string>();
            string sessionId = result["sessionId"]!.GetValue<string>();
            Assert.True(Directory.Exists(dir));
            Assert.True(File.Exists(Path.Combine(dir, "mcec.exe")));

            // end-session removes it.
            JsonObject endEnv = Envelope(Call(6, "end-session", new JsonObject { ["sessionId"] = sessionId }));
            Assert.True(endEnv["ok"]!.GetValue<bool>());
            Assert.True(endEnv["result"]!.AsObject()["removed"]!.GetValue<bool>());
            Assert.False(Directory.Exists(dir));
        }
        finally {
            SessionProvisioner.SessionsRoot = origRoot;
            SessionProvisioner.BinariesDir = origBin;
            AgentRuntime.Settings = null;
            try { if (Directory.Exists(baseTemp)) { Directory.Delete(baseTemp, recursive: true); } } catch { }
        }
    }
}
