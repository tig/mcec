// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class CaptureCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        CaptureCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsCapture() {
        List<Command> builtIns = CaptureCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "capture");
    }

    [Fact]
    public void Clone_CopiesProperties_AndIsIndependent() {
        CaptureCommand original = new() {
            Cmd = "capture",
            Enabled = true,
            Window = "Notepad",
            Handle = 42,
            Process = "notepad",
            ClassName = "Notepad",
            Foreground = true,
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4,
            File = "out.png",
        };

        CaptureCommand clone = (CaptureCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("capture", clone.Cmd);
        Assert.True(clone.Enabled);
        Assert.Equal("Notepad", clone.Window);
        Assert.Equal(42, clone.Handle);
        Assert.Equal("notepad", clone.Process);
        Assert.Equal("Notepad", clone.ClassName);
        Assert.True(clone.Foreground);
        Assert.Equal(1, clone.X);
        Assert.Equal(2, clone.Y);
        Assert.Equal(3, clone.Width);
        Assert.Equal(4, clone.Height);
        Assert.Equal("out.png", clone.File);

        // Independence: mutating the clone must not bleed back into the original.
        clone.Window = "Other";
        clone.Handle = 99;
        Assert.Equal("Notepad", original.Window);
        Assert.Equal(42, original.Handle);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            CaptureCommand cmd = new() { Cmd = "capture", Enabled = true, Reply = reply };

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
