// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// State-machine tests for <see cref="GifRecorder"/> (#157): when the capture loop self-terminates
/// (max frames, max duration, or a grab exception) the recorder must leave the "recording" state so
/// a later <c>start</c> is allowed, while a later <c>stop</c> can still fetch the buffered GIF;
/// exactly once. All tests use a synthetic in-memory grabber; the desktop is never touched.
/// </summary>
[Collection("AgentSerial")]
public class GifRecorderTests {
    /// <summary>Polls <paramref name="condition"/> until true or <paramref name="timeoutMs"/> elapses.</summary>
    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000) {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs) {
            if (condition()) {
                return true;
            }
            Thread.Sleep(10);
        }
        return condition();
    }

    /// <summary>Drains any active or completed recording left behind by a prior test.</summary>
    private static void ResetRecorder() {
        while (GifRecorder.Stop() is not null) {
            // keep draining; an active and a completed slot may both need clearing
        }
    }

    [Fact]
    public void AutoStop_OnMaxFrames_ClearsIsRecording() {
        ResetRecorder();
        try {
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 3, maxWidth: 64, maxDurationMs: 60000, target: null);

            // The loop hits maxFrames in ~60 ms; IsRecording must go false without an explicit Stop,
            // and the buffered GIF must move to the completed (fetchable) slot.
            Assert.True(WaitUntil(() => !GifRecorder.IsRecording),
                "IsRecording stayed true after the capture loop self-terminated on maxFrames");
            Assert.True(GifRecorder.HasCompletedRecording);
        }
        finally {
            ResetRecorder();
        }
    }

    [Fact]
    public void AutoStop_OnMaxDuration_ClearsIsRecording() {
        ResetRecorder();
        try {
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 10, maxFrames: 600, maxWidth: 64, maxDurationMs: 150, target: null);

            Assert.True(WaitUntil(() => !GifRecorder.IsRecording),
                "IsRecording stayed true after the capture loop self-terminated on maxDurationMs");
            Assert.True(GifRecorder.HasCompletedRecording);
        }
        finally {
            ResetRecorder();
        }
    }

    [Fact]
    public void AutoStop_OnGrabException_ClearsIsRecording_AndStopReportsError() {
        ResetRecorder();
        try {
            GifRecorder.Start(() => throw new InvalidOperationException("boom-grab"),
                fps: 50, maxFrames: 600, maxWidth: 64, maxDurationMs: 60000, target: null);

            Assert.True(WaitUntil(() => !GifRecorder.IsRecording),
                "IsRecording stayed true after the capture loop self-terminated on a grab exception");
            Assert.True(GifRecorder.HasCompletedRecording);

            // The failure reason must still be fetchable by a later stop; and fetching releases it.
            RecordingResult? result = GifRecorder.Stop();
            Assert.NotNull(result);
            Assert.Contains("boom-grab", result.Error);
            Assert.False(GifRecorder.HasCompletedRecording);
        }
        finally {
            ResetRecorder();
        }
    }

    [Fact]
    public void StartAfterAutoStop_Succeeds() {
        ResetRecorder();
        try {
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 2, maxWidth: 64, maxDurationMs: 60000, target: null);
            Assert.True(WaitUntil(() => !GifRecorder.IsRecording), "first recording never auto-stopped");
            Assert.True(GifRecorder.HasCompletedRecording);

            // #157: this used to throw "A recording is already in progress." forever.
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 5, maxFrames: 600, maxWidth: 64, maxDurationMs: 60000, target: null);
            Assert.True(GifRecorder.IsRecording);
            Assert.False(GifRecorder.HasCompletedRecording); // the unfetched GIF was discarded, not kept
        }
        finally {
            ResetRecorder();
        }
    }

    [Fact]
    public void StopAfterAutoStop_ReturnsBufferedGif_ExactlyOnce() {
        ResetRecorder();
        try {
            Stopwatch sw = Stopwatch.StartNew();
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 3, maxWidth: 64, maxDurationMs: 60000, target: null);
            Assert.True(WaitUntil(() => !GifRecorder.IsRecording), "recording never auto-stopped");
            Assert.True(GifRecorder.HasCompletedRecording);
            long completedAtMs = sw.ElapsedMilliseconds;

            // Wait a while before fetching: the buffered frames must survive, and the reported
            // duration must reflect the recording itself, not how long the GIF sat unfetched.
            Thread.Sleep(500);

            RecordingResult? result = GifRecorder.Stop();
            Assert.NotNull(result);
            Assert.Equal(3, result.Frames);
            Assert.NotEmpty(result.Gif);
            Assert.True(result.DurationMs <= completedAtMs + 250,
                $"DurationMs {result.DurationMs} kept counting after auto-stop (completed at ~{completedAtMs} ms)");

            // Exactly once: the completed recording (and its pinned frames) must be released on fetch.
            Assert.False(GifRecorder.HasCompletedRecording);
            Assert.Null(GifRecorder.Stop());
        }
        finally {
            ResetRecorder();
        }
    }

    [Fact]
    public void StartAfterAutoStop_DiscardsUnfetchedRecording() {
        ResetRecorder();
        try {
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 2, maxWidth: 64, maxDurationMs: 60000,
                target: new JsonObject { ["tag"] = "first" });
            Assert.True(WaitUntil(() => !GifRecorder.IsRecording), "first recording never auto-stopped");

            // Starting while a completed-but-unfetched GIF exists replaces it (documented behavior).
            GifRecorder.Start(() => new Bitmap(8, 8), fps: 50, maxFrames: 2, maxWidth: 64, maxDurationMs: 60000,
                target: new JsonObject { ["tag"] = "second" });
            Assert.True(WaitUntil(() => !GifRecorder.IsRecording), "second recording never auto-stopped");

            // Stop returns the NEW recording; the discarded one is gone (only one fetch total).
            RecordingResult? result = GifRecorder.Stop();
            Assert.NotNull(result);
            Assert.Equal("second", result.Target?["tag"]?.GetValue<string>());
            Assert.Null(GifRecorder.Stop());
        }
        finally {
            ResetRecorder();
        }
    }
}
