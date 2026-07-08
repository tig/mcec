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
    public void Execute_OversizedRegion_FailsWithRegionTooLarge_BeforeCapturing() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            // The #158 attack: an agent-supplied 40000x40000 region would ask GDI+ for ~6.4 GB.
            // The command must reject it with a diagnosable envelope (code region-too-large, detail
            // stating the limits) WITHOUT allocating a bitmap or touching the screen.
            CapturingReply reply = new();
            CaptureCommand cmd = new() {
                Cmd = "capture", Enabled = true, Reply = reply, X = 0, Y = 0, Width = 40000, Height = 40000,
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Equal("region-too-large", json["errorCode"]!.GetValue<string>());
            // #191: an oversized region is a malformed request; the recovery is to shrink it,
            // so the category is invalid-argument, not no-target.
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
            // The detail must tell the agent what the limits are so the failure is recoverable.
            Assert.Contains(ScreenCapture.MaxRegionDimension.ToString(), json["error"]!.GetValue<string>());
            // Rejected before capture: no image payload may be present.
            Assert.Null(json["data"]);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_RegionOverPixelBudget_FailsWithRegionTooLarge() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            // Each side is under the per-side cap; the product (81 MP) exceeds the pixel budget.
            CapturingReply reply = new();
            CaptureCommand cmd = new() {
                Cmd = "capture", Enabled = true, Reply = reply, X = 0, Y = 0, Width = 9000, Height = 9000,
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Equal("region-too-large", json["errorCode"]!.GetValue<string>());
            Assert.Contains(ScreenCapture.MaxRegionPixels.ToString(), json["error"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
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

    [Fact]
    public void BuildCommand_MapsCaptureDownscaleArgs() {
        CaptureCommand cmd = Assert.IsType<CaptureCommand>(AgentServer.BuildCommand("capture", new JsonObject {
            ["foreground"] = true,
            ["maxWidth"] = 640,
            ["scale"] = 0.5,
        }));

        Assert.True(cmd.Foreground);
        Assert.Equal(640, cmd.MaxWidth);
        Assert.Equal(0.5, cmd.Scale, 3);
    }
}
