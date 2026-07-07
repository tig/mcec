// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class GetTextCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        GetTextCommand cmd = new();
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsGetText() {
        List<Command> builtIns = GetTextCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "get-text");
    }

    [Fact]
    public void Execute_OversizedRegion_FailsWithRegionTooLarge_BeforeCapturing() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            GetTextCommand cmd = new() {
                Cmd = "get-text", Enabled = true, Reply = reply, X = 0, Y = 0, Width = 40000, Height = 40000,
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Equal("region-too-large", json["errorCode"]!.GetValue<string>());
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
            Assert.Null(json["data"]);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WindowRegionOutOfBounds_FailsBeforeOcr() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            StubGetTextCommand cmd = new() {
                Cmd = "get-text",
                Enabled = true,
                Reply = reply,
                Window = "Stub",
                X = 90,
                Y = 90,
                Width = 20,
                Height = 20,
                StubWindowBitmap = new Bitmap(100, 100),
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.Equal("region-out-of-bounds", json["errorCode"]!.GetValue<string>());
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
            Assert.False(cmd.OcrCalled);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WindowRegion_ReturnsOcrText() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            StubGetTextCommand cmd = new() {
                Cmd = "get-text",
                Enabled = true,
                Reply = reply,
                Window = "Stub",
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40,
                StubWindowBitmap = NonBlankBitmap(100, 100),
                StubOcrResult = new RegionOcrResult("hello world", 1, 2, "en-US"),
            };

            bool result = cmd.Execute();

            Assert.True(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>());
            JsonObject data = json["data"]!.AsObject();
            Assert.Equal("hello world", data["text"]!.GetValue<string>());
            Assert.Equal(2, data["wordCount"]!.GetValue<int>());
            Assert.Equal("window", data["region"]!["relativeTo"]!.GetValue<string>());
            Assert.True(cmd.OcrCalled);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_OcrEmptyText_FailsWithOcrNoText() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            StubGetTextCommand cmd = new() {
                Cmd = "get-text",
                Enabled = true,
                Reply = reply,
                Window = "Stub",
                StubWindowBitmap = NonBlankBitmap(100, 100),
                StubOcrResult = new RegionOcrResult("", 0, 0, "en-US"),
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.Equal("ocr-no-text", json["errorCode"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    private static Bitmap NonBlankBitmap(int w, int h) {
        Bitmap bmp = new(w, h);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.DrawLine(Pens.Black, 0, 0, w - 1, h - 1);
        g.DrawString("sample", SystemFonts.DefaultFont, Brushes.Black, 4, 4);
        return bmp;
    }

}