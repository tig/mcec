// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Drawing.Imaging;
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

    [Fact]
    public void Execute_BlankFrame_ReturnsOcrBlankCategory() {
        // Addresses CR feedback P2 (ocr-blank mapping) on PR 334. Blank detection happens before OCR.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // solid-color small bitmap will be detected blank by AnalyzeBlank
            StubGetTextCommand cmd = new() {
                Cmd = "get-text",
                Enabled = true,
                Reply = reply,
                Window = "Stub",
                StubWindowBitmap = new Bitmap(8, 8),  // default pixels -> blank dominant
            };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.Equal("ocr-blank", json["errorCategory"]!.GetValue<string>());
            Assert.Contains("blank", json["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.False(cmd.OcrCalled); // never reached OCR
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void RegionOcr_TooLargeImage_ThrowsEarly_WithClearMessage() {
        // Addresses CR P2 on PR 334: before calling RecognizeAsync (which would fail inside OCR for dims > Max),
        // RegionOcr must check against OcrEngine.MaxImageDimension and fail fast with invalid-argument path.
        // Uses early check so test runs even when no OCR language pack is installed on the test agent.
        uint max = Windows.Media.Ocr.OcrEngine.MaxImageDimension;
        int tooBig = (int)max + 1;
        using Bitmap huge = new(tooBig, 4);  // width exceeds

        var ex = Assert.Throws<ArgumentException>(() => RegionOcr.Recognize(huge));
        Assert.Contains("exceed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OCR", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BitmapFromPngBytes_RemainsUsableAfterStreamClosed() {
        // Addresses CR P1 on PR 334 (stream lifetime): documents the Bitmap(Stream) contract.
        // The GetTextCommand window path must not hand out a Bitmap whose backing stream was disposed
        // by the time FinishOcr / AnalyzeBlank / RegionOcr.ToSoftwareBitmap run.
        using Bitmap original = NonBlankBitmap(16, 16);
        using var save = new MemoryStream();
        original.Save(save, ImageFormat.Png);
        byte[] png = save.ToArray();

        Bitmap? bmp;
        using (var ms = new MemoryStream(png)) {
            bmp = new Bitmap(ms);
            // stream goes out of scope / disposed here -- simulates end of CaptureWindowBitmap
        }

        // Use like the real path does (GetPixels in AnalyzeBlank; Save in RegionOcr)
        ImageStats stats = ScreenCapture.AnalyzeBlank(bmp!);
        Assert.False(stats.IsBlank);

        using var rt = new MemoryStream();
        bmp!.Save(rt, ImageFormat.Png);
        Assert.True(rt.Length > 100);

        bmp!.Dispose();
    }

}