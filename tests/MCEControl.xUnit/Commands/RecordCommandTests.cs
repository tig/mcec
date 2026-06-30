// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class RecordCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        RecordCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsRecord() {
        List<Command> builtIns = RecordCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "record");
    }

    [Fact]
    public void Clone_CopiesProperties_AndIsIndependent() {
        RecordCommand original = new() {
            Cmd = "record",
            Enabled = true,
            Action = "oneshot",
            Window = "Notepad",
            Handle = 42,
            Process = "notepad",
            ClassName = "Notepad",
            Foreground = true,
            X = 1, Y = 2, Width = 3, Height = 4,
            Fps = 10,
            DurationMs = 5000,
            MaxWidth = 800,
            File = "out.gif",
        };

        RecordCommand clone = (RecordCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("record", clone.Cmd);
        Assert.True(clone.Enabled);
        Assert.Equal("oneshot", clone.Action);
        Assert.Equal("Notepad", clone.Window);
        Assert.Equal(42, clone.Handle);
        Assert.Equal("notepad", clone.Process);
        Assert.Equal("Notepad", clone.ClassName);
        Assert.True(clone.Foreground);
        Assert.Equal(1, clone.X);
        Assert.Equal(2, clone.Y);
        Assert.Equal(3, clone.Width);
        Assert.Equal(4, clone.Height);
        Assert.Equal(10, clone.Fps);
        Assert.Equal(5000, clone.DurationMs);
        Assert.Equal(800, clone.MaxWidth);
        Assert.Equal("out.gif", clone.File);

        // Independence: mutating the clone must not bleed back into the original.
        clone.Window = "Other";
        clone.Fps = 99;
        Assert.Equal("Notepad", original.Window);
        Assert.Equal(10, original.Fps);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            RecordCommand cmd = new() { Cmd = "record", Enabled = true, Reply = reply, DurationMs = 200 };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_UnknownAction_FailsWithoutTouchingDesktop() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            RecordCommand cmd = new() { Cmd = "record", Enabled = true, Reply = reply, Action = "bogus" };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Contains("action", json["error"]!.GetValue<string>(), System.StringComparison.OrdinalIgnoreCase);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_StopWhenNotRecording_Fails() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            // Ensure no recording is in progress from a prior test.
            GifRecorder.Stop();

            CapturingReply reply = new();
            RecordCommand cmd = new() { Cmd = "record", Enabled = true, Reply = reply, Action = "stop" };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }
}
