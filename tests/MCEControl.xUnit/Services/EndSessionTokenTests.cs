// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the #215 session-token wiring: <c>provision-session</c>'s token is the teardown
/// credential for real; <c>end-session</c> refuses a correct sessionId with a missing or wrong
/// token (with a structured error) and tears down only for the token holder. Before this,
/// <c>ProvisionedSession.Token</c> was generated and documented but checked nowhere.
/// </summary>
[Collection("AgentSerial")]
public class EndSessionTokenTests : IDisposable {
    private readonly string _baseTemp;
    private readonly string _origRoot;
    private readonly string _origBinaries;

    public EndSessionTokenTests() {
        _origRoot = SessionProvisioner.SessionsRoot;
        _origBinaries = SessionProvisioner.BinariesDir;

        _baseTemp = Path.Combine(Path.GetTempPath(), "mcec-endsession-token-test", Path.GetRandomFileName());
        string fakeBinaries = Path.Combine(_baseTemp, "install");
        Directory.CreateDirectory(fakeBinaries);
        File.WriteAllText(Path.Combine(fakeBinaries, "mcec.exe"), "stub");

        SessionProvisioner.SessionsRoot = Path.Combine(_baseTemp, "sessions");
        SessionProvisioner.BinariesDir = fakeBinaries;
        AgentRuntime.Settings = new AppSettings { AllowSessionProvisioning = true };
    }

    public void Dispose() {
        SessionProvisioner.SessionsRoot = _origRoot;
        SessionProvisioner.BinariesDir = _origBinaries;
        AgentRuntime.Settings = null;
        try {
            if (Directory.Exists(_baseTemp)) {
                Directory.Delete(_baseTemp, recursive: true);
            }
        }
        catch { /* best-effort cleanup */ }
    }

    private static JsonObject Call(int id, string tool, JsonObject args) =>
        AgentServer.Dispatch(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args },
        })!;

    private static JsonObject Envelope(JsonObject resp) {
        foreach (JsonNode? block in resp["result"]!.AsObject()["content"]!.AsArray()) {
            if (block?["type"]?.GetValue<string>() == "text") {
                return JsonNode.Parse(block["text"]!.GetValue<string>())!.AsObject();
            }
        }
        Assert.Fail("no text content block in tool result");
        return [];
    }

    private static string ErrorCode(JsonObject env) => env["error"]!.AsObject()["code"]!.GetValue<string>();

    [Fact]
    public void EndSession_MissingToken_IsRefused_AndNothingIsDeleted() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);

        JsonObject env = Envelope(Call(1, "end-session", new JsonObject { ["sessionId"] = session.SessionId }));

        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("bad-arguments", ErrorCode(env));
        Assert.Equal("invalid-argument", env["error"]!.AsObject()["category"]!.GetValue<string>());
        Assert.True(Directory.Exists(session.Directory), "a token-less end-session must not delete anything");
    }

    [Fact]
    public void EndSession_WrongToken_IsRefused_WithStructuredError_AndNothingIsDeleted() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);

        JsonObject env = Envelope(Call(2, "end-session",
            new JsonObject { ["sessionId"] = session.SessionId, ["token"] = "not-the-token" }));

        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("session-token-invalid", ErrorCode(env));
        Assert.True(Directory.Exists(session.Directory), "a wrong-token end-session must not delete anything");
    }

    [Fact]
    public void EndSession_CorrectToken_TearsDown() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);

        JsonObject env = Envelope(Call(3, "end-session",
            new JsonObject { ["sessionId"] = session.SessionId, ["token"] = session.Token }));

        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.True(env["result"]!.AsObject()["removed"]!.GetValue<bool>());
        Assert.False(Directory.Exists(session.Directory));
    }

    [Fact]
    public void EndSession_SessionAlreadyGone_StaysIdempotentSuccess_WithAnyToken() {
        // A well-formed id whose directory no longer exists: nothing left for the credential to
        // protect, so teardown keeps its historical idempotent-success shape.
        JsonObject env = Envelope(Call(4, "end-session",
            new JsonObject { ["sessionId"] = "abc123def456", ["token"] = "whatever" }));

        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.True(env["result"]!.AsObject()["removed"]!.GetValue<bool>());
    }

    [Fact]
    public void EndSession_ToolSchema_RequiresTheToken() {
        JsonObject resp = AgentServer.Dispatch(new JsonObject {
            ["jsonrpc"] = "2.0", ["id"] = 5, ["method"] = "tools/list", ["params"] = new JsonObject(),
        })!;
        JsonObject? endSession = null;
        foreach (JsonNode? tool in resp["result"]!.AsObject()["tools"]!.AsArray()) {
            if (tool?["name"]?.GetValue<string>() == "end-session") {
                endSession = tool.AsObject();
                break;
            }
        }
        Assert.NotNull(endSession);
        JsonObject schema = endSession["inputSchema"]!.AsObject();
        Assert.True(schema["properties"]!.AsObject().ContainsKey("token"));
        bool tokenRequired = false;
        foreach (JsonNode? r in schema["required"]!.AsArray()) {
            tokenRequired |= r!.GetValue<string>() == "token";
        }
        Assert.True(tokenRequired, "end-session must declare 'token' as required");
    }
}
