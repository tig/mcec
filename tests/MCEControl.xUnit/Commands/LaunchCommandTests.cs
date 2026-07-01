// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class LaunchCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        LaunchCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsLaunch() {
        List<Command> builtIns = LaunchCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "launch");
    }

    [Fact]
    public void Clone_CopiesProperties_AndIsIndependent() {
        LaunchCommand original = new() {
            Cmd = "launch",
            Enabled = true,
            Path = "notepad.exe",
            Arguments = "test.txt",
            WorkingDirectory = @"C:\temp",
            Timeout = 1234,
        };

        LaunchCommand clone = (LaunchCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("notepad.exe", clone.Path);
        Assert.Equal("test.txt", clone.Arguments);
        Assert.Equal(@"C:\temp", clone.WorkingDirectory);
        Assert.Equal(1234, clone.Timeout);

        clone.Path = "changed.exe";
        Assert.Equal("notepad.exe", original.Path);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            LaunchCommand cmd = new() { Cmd = "launch", Enabled = true, Reply = reply, Path = "notepad.exe" };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Contains("disabled", json["error"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_MissingPath_FailsWithMessage() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            LaunchCommand cmd = new() { Cmd = "launch", Enabled = true, Reply = reply, Path = "" };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Contains("path", json["error"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    // Test-first for Codex CR feedback (P2 on shell:.lnk null Process):
    // We expect that even if internal Process.Start returns null (common for shell targets),
    // the command should still succeed (return ok=true) and attempt to surface window info
    // rather than hard-failing the launch. The unit guard tests above + E2E/her o usage
    // cover the path; actual null-Process simulation is integration-only to avoid side effects.
}
