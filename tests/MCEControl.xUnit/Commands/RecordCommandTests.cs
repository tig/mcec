// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
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
            Assert.Contains("action", json["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_StartOversizedRegion_FailsWithRegionTooLarge_WithoutRecording() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            GifRecorder.Stop(); // clear any prior recording
            // A record region flows into the same CaptureRegionBitmap as capture (#158): an
            // oversized region must be rejected up front; no recording may start.
            CapturingReply reply = new();
            RecordCommand cmd = new() {
                Cmd = "record", Enabled = true, Reply = reply, Action = "start",
                X = 0, Y = 0, Width = 40000, Height = 40000,
            };

            bool result = cmd.Execute();

            Assert.False(result);
            Assert.False(GifRecorder.IsRecording);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Equal("region-too-large", json["errorCode"]!.GetValue<string>());
            // #191: an oversized region is a malformed request; invalid-argument, not no-target.
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
            Assert.Contains(ScreenCapture.MaxRegionDimension.ToString(), json["error"]!.GetValue<string>());
        }
        finally {
            GifRecorder.Stop();
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_StopWriteFailure_ReturnsError() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        // A path whose parent is a FILE (not a directory) makes WriteAllBytes throw deterministically.
        string fileAsDir = Path.GetTempFileName();
        string badPath = Path.Combine(fileAsDir, "out.gif");
        try {
            GifRecorder.Stop(); // clear any prior recording
            // Record a couple of synthetic frames (no desktop capture needed), then stop to a bad path.
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 10, maxFrames: 600, maxWidth: 64, maxDurationMs: 60000, target: null);
            Thread.Sleep(350);

            CapturingReply reply = new();
            RecordCommand cmd = new() { Cmd = "record", Enabled = true, Reply = reply, Action = "stop", File = badPath };

            bool result = cmd.Execute();

            // No GIF bytes are returned inline, so a failed write means there is no usable output:
            // the command must report failure rather than success-with-fileError.
            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
        }
        finally {
            GifRecorder.Stop();
            AgentRuntime.Settings = null;
            File.Delete(fileAsDir);
        }
    }

    [Fact]
    public void Execute_StopAfterAutoStop_WritesBufferedGif() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        string outFile = Path.Combine(Path.GetTempPath(), $"mcec-rec-test-{Guid.NewGuid():N}.gif");
        try {
            GifRecorder.Stop(); // clear any prior recording
            // A tiny maxFrames makes the capture loop self-terminate almost immediately (#157).
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 3, maxWidth: 64, maxDurationMs: 60000, target: null);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (GifRecorder.IsRecording && sw.ElapsedMilliseconds < 5000) {
                Thread.Sleep(10);
            }
            Assert.False(GifRecorder.IsRecording); // auto-stop must clear the recording state

            // `record action=stop` after the auto-stop must still return the buffered GIF.
            CapturingReply reply = new();
            RecordCommand cmd = new() { Cmd = "record", Enabled = true, Reply = reply, Action = "stop", File = outFile };

            bool result = cmd.Execute();

            Assert.True(result, reply.Captured);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>(), reply.Captured);
            Assert.Equal(3, json["data"]!["frames"]!.GetValue<int>());
            Assert.True(File.Exists(outFile));
        }
        finally {
            GifRecorder.Stop();
            AgentRuntime.Settings = null;
            try { File.Delete(outFile); } catch (IOException) { /* best effort cleanup */ }
        }
    }

    [Fact]
    public void Execute_Oneshot_AfterAutoStop_WarnsUnfetchedRecordingDiscarded() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        string outFile = Path.Combine(Path.GetTempPath(), $"mcec-rec-test-{Guid.NewGuid():N}.gif");
        try {
            GifRecorder.Stop(); // clear any prior recording
            // Leave a completed-but-unfetched recording behind (tiny maxFrames → immediate auto-stop).
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 2, maxWidth: 64, maxDurationMs: 60000, target: null);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (GifRecorder.IsRecording && sw.ElapsedMilliseconds < 5000) {
                Thread.Sleep(10);
            }
            Assert.False(GifRecorder.IsRecording);
            Assert.True(GifRecorder.HasCompletedRecording);

            // A oneshot issued now discards that unfetched GIF; its reply must say so (M1, #157):
            // the discard warning may not be silently dropped just because the reply comes from stop.
            CapturingReply reply = new();
            SyntheticGrabRecordCommand cmd = new() {
                Cmd = "record", Enabled = true, Reply = reply, Action = "oneshot", DurationMs = 150, Fps = 20, File = outFile,
            };

            bool result = cmd.Execute();

            Assert.True(result, reply.Captured);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>(), reply.Captured);
            Assert.True(File.Exists(outFile));

            JsonArray? warnings = json["warnings"]?.AsArray();
            Assert.NotNull(warnings);
            Assert.Contains(warnings, w => w?["code"]?.GetValue<string>() == "unfetched-recording-discarded");

            // The discarded recording is gone: nothing else is left to fetch.
            Assert.False(GifRecorder.HasCompletedRecording);
        }
        finally {
            GifRecorder.Stop();
            AgentRuntime.Settings = null;
            try { File.Delete(outFile); } catch (IOException) { /* best effort cleanup */ }
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
