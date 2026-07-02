// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json.Nodes;
using System.Threading;

namespace MCEControl;

/// <summary>
/// Drives a GIF recording for the MCEC 3.0 agent <c>record</c> feature: a background thread grabs
/// frames from a target (window or screen region) at a fixed fps, each frame is immediately encoded
/// to a standalone GIF (keeping memory bounded; we never hold hundreds of raw bitmaps), and
/// <see cref="Stop"/> stitches them into one animation via <see cref="GifEncoder"/>.
///
/// SECURITY/SAFETY: at most one recording runs at a time (a static singleton). The capture loop is
/// hard-bounded by fps, max frames, max duration, and a max width (frames are downscaled) so an agent
/// cannot create an unbounded file. The owning <see cref="RecordCommand"/> applies the security gate
/// and audit; this type is the mechanism only.
///
/// LIFECYCLE (#157): <c>idle → (Start) → recording → (Stop) → idle</c>, or
/// <c>recording → (loop self-terminates on max frames/duration/grab failure) → completed</c>. In the
/// completed state <see cref="IsRecording"/> is false, so a new <see cref="Start"/> is allowed; it
/// discards the unfetched GIF (and reports that it did); while <see cref="Stop"/> still returns the
/// buffered GIF exactly once; fetching releases the frames so they are not pinned for the process
/// lifetime.
/// </summary>
public sealed class GifRecorder {
    private static readonly object Gate = new();
    private static GifRecorder? _active;

    /// <summary>A recording whose capture loop self-terminated and whose GIF has not been fetched yet.</summary>
    private static GifRecorder? _completed;

    private readonly Func<Bitmap> _grab;
    private readonly int _fps;
    private readonly int _maxFrames;
    private readonly int _maxWidth;
    private readonly long _maxDurationMs;
    private readonly JsonNode? _target;
    private readonly List<byte[]> _frames = [];
    private readonly Stopwatch _clock = new();

    private Thread? _thread;
    private volatile bool _stopRequested;
    private int _width;
    private int _height;
    private string? _error;

    private GifRecorder(Func<Bitmap> grab, int fps, int maxFrames, int maxWidth, long maxDurationMs, JsonNode? target) {
        _grab = grab;
        _fps = fps;
        _maxFrames = maxFrames;
        _maxWidth = maxWidth;
        _maxDurationMs = maxDurationMs;
        _target = target;
    }

    /// <summary>True while a recording is in progress. False once the capture loop has
    /// self-terminated (auto-stop), even before the buffered GIF is fetched via <see cref="Stop"/>.</summary>
    public static bool IsRecording {
        get { lock (Gate) { return _active is not null; } }
    }

    /// <summary>True when a recording auto-stopped and its buffered GIF has not been fetched yet.</summary>
    public static bool HasCompletedRecording {
        get { lock (Gate) { return _completed is not null; } }
    }

    /// <summary>The effective fps of the in-progress recording, or 0 when idle.</summary>
    public static int ActiveFps {
        get { lock (Gate) { return _active?._fps ?? 0; } }
    }

    /// <summary>The target descriptor of the in-progress recording, or null when idle.</summary>
    public static JsonNode? ActiveTarget {
        get { lock (Gate) { return _active?._target?.DeepClone(); } }
    }

    /// <summary>
    /// Starts a recording. <paramref name="grab"/> is invoked once per frame and must return a fresh
    /// bitmap the recorder will dispose. The caller has already clamped fps/limits to operator policy.
    /// A completed-but-unfetched recording (auto-stop whose GIF was never fetched) does NOT block a
    /// new start: it is discarded and replaced.
    /// </summary>
    /// <returns>True when a completed-but-unfetched recording was discarded to make room; the caller
    /// should surface that as a warning.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a recording is already in progress.</exception>
    public static bool Start(Func<Bitmap> grab, int fps, int maxFrames, int maxWidth, long maxDurationMs, JsonNode? target) {
        ArgumentNullException.ThrowIfNull(grab);
        lock (Gate) {
            if (_active is not null) {
                throw new InvalidOperationException("A recording is already in progress.");
            }
            bool discardedUnfetched = _completed is not null;
            _completed = null; // an auto-stopped recording nobody fetched; replaced by the new one
            GifRecorder recorder = new(grab, Math.Max(1, fps), Math.Max(1, maxFrames), Math.Max(1, maxWidth), Math.Max(1, maxDurationMs), target);
            recorder._thread = new Thread(recorder.CaptureLoop) {
                IsBackground = true,
                Name = "mcec-gif-recorder",
            };
            _active = recorder;
            recorder._clock.Start();
            recorder._thread.Start();
            return discardedUnfetched;
        }
    }

    /// <summary>
    /// Stops the in-progress recording (or claims a completed one whose loop already self-terminated),
    /// waits for the capture thread to drain, assembles the GIF, and returns the result. Fetching a
    /// completed recording releases it; a second call returns null, so the buffered frames are never
    /// pinned past the fetch. Returns null when there is nothing to stop or fetch.
    /// </summary>
    public static RecordingResult? Stop(bool loop = true) {
        GifRecorder? recorder;
        lock (Gate) {
            recorder = _active ?? _completed;
            _active = null;
            _completed = null;
        }
        if (recorder is null) {
            return null;
        }

        recorder._stopRequested = true;
        recorder._thread?.Join();
        recorder._clock.Stop();

        long durationMs = recorder._clock.ElapsedMilliseconds;
        if (recorder._frames.Count == 0) {
            return new RecordingResult {
                Frames = 0,
                Fps = recorder._fps,
                DurationMs = durationMs,
                Target = recorder._target,
                Error = recorder._error ?? "No frames were captured.",
            };
        }

        byte[] gif;
        try {
            gif = GifEncoder.Assemble(recorder._frames, 1000 / recorder._fps, loop);
        }
        catch (Exception e) {
            return new RecordingResult {
                Frames = recorder._frames.Count,
                Fps = recorder._fps,
                DurationMs = durationMs,
                Target = recorder._target,
                Error = $"GIF assembly failed: {e.Message}",
            };
        }

        return new RecordingResult {
            Gif = gif,
            Frames = recorder._frames.Count,
            Width = recorder._width,
            Height = recorder._height,
            Fps = recorder._fps,
            DurationMs = durationMs,
            Target = recorder._target,
            Error = recorder._error, // a soft error (e.g. a dropped frame) is reported but the GIF still returns
        };
    }

    /// <summary>Background capture loop: grab → downscale → encode, paced to fps, until stop or a limit.
    /// On exit it always runs <see cref="TransitionToCompleted"/> so a self-terminating loop (max
    /// frames/duration/grab failure) cannot leave the recorder stuck in the recording state (#157).</summary>
    private void CaptureLoop() {
        try {
            CaptureFrames();
        }
        finally {
            TransitionToCompleted();
        }
    }

    /// <summary>
    /// Moves this recorder from active to completed when its loop exited on its own (a limit was hit
    /// or the grab failed). Under <see cref="Gate"/> so it cannot race <see cref="Start"/>/<see cref="Stop"/>:
    /// when <see cref="Stop"/> already claimed this recorder (it clears <see cref="_active"/> before
    /// requesting the stop), there is nothing to do; Stop owns the frames and will assemble them.
    /// </summary>
    private void TransitionToCompleted() {
        lock (Gate) {
            if (ReferenceEquals(_active, this)) {
                _clock.Stop(); // freeze the duration at auto-stop, not at whenever the GIF is fetched
                _active = null;
                _completed = this;
            }
        }
    }

    /// <summary>The body of <see cref="CaptureLoop"/>: one iteration per frame, paced to fps.</summary>
    private void CaptureFrames() {
        double frameIntervalMs = 1000.0 / _fps;
        while (!_stopRequested) {
            long frameStart = _clock.ElapsedMilliseconds;

            try {
                using Bitmap raw = _grab();
                Bitmap? scaled = null;
                try {
                    Bitmap toEncode = raw;
                    if (_maxWidth > 0 && raw.Width > _maxWidth) {
                        scaled = Downscale(raw, _maxWidth);
                        toEncode = scaled;
                    }
                    _width = Math.Max(_width, toEncode.Width);
                    _height = Math.Max(_height, toEncode.Height);
                    _frames.Add(GifEncoder.EncodeFrame(toEncode));
                }
                finally {
                    scaled?.Dispose();
                }
            }
            catch (Exception e) {
                // The target may have closed mid-record. Record the reason and stop; any frames so far
                // are still assembled into a (shorter) GIF.
                _error = e.Message;
                break;
            }

            if (_frames.Count >= _maxFrames) {
                break;
            }
            if (_clock.ElapsedMilliseconds >= _maxDurationMs) {
                break;
            }

            // Pace to fps, but wake often enough to honor a stop request promptly.
            long spent = _clock.ElapsedMilliseconds - frameStart;
            int remaining = (int)(frameIntervalMs - spent);
            while (remaining > 0 && !_stopRequested && _clock.ElapsedMilliseconds < _maxDurationMs) {
                int slice = Math.Min(remaining, 50);
                Thread.Sleep(slice);
                remaining -= slice;
            }
        }
    }

    /// <summary>Scales a bitmap down so its width is <paramref name="maxWidth"/>, preserving aspect.</summary>
    private static Bitmap Downscale(Bitmap src, int maxWidth) {
        int newWidth = maxWidth;
        int newHeight = Math.Max(1, (int)Math.Round(src.Height * (maxWidth / (double)src.Width)));
        Bitmap dst = new(newWidth, newHeight);
        try {
            using Graphics g = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, newWidth, newHeight);
        }
        catch {
            dst.Dispose();
            throw;
        }
        return dst;
    }
}
