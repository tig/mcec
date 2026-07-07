// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// MCEC 3.0 agent <c>record</c> command: captures agent-driven desktop activity to an animated GIF;
/// a whole short segment via explicit <c>start</c>/<c>stop</c>, or a bounded one-shot via
/// <c>durationMs</c>. Reuses the same target model as <see cref="CaptureCommand"/> (window by
/// handle/title/process/class/foreground, or an explicit region) and the same security gate.
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> (a separate opt-in from
/// actuation; enforced structurally by <see cref="AgentCommand"/>) and the per-command <c>Enabled</c>
/// flag; every start/stop/write is audited via <see cref="AgentRuntime.Audit"/>. The capture loop is
/// hard-bounded (fps/duration/frames/width) by the operator's <see cref="AppSettings"/> limits so an
/// agent cannot create an unbounded file.
///
/// PRIVACY: a recording captures whatever is on screen for its whole duration; louder than a single
/// still <c>capture</c>. See <c>docs/agent_control.md</c>.
/// </summary>
public class RecordCommand : WindowTargetingAgentCommand {
    [XmlAttribute("action")] public string Action { get; set; } = null!;
    [XmlAttribute("x")] public int X { get; set; }
    [XmlAttribute("y")] public int Y { get; set; }
    [XmlAttribute("width")] public int Width { get; set; }
    [XmlAttribute("height")] public int Height { get; set; }
    [XmlAttribute("fps")] public int Fps { get; set; }
    [XmlAttribute("durationms")] public int DurationMs { get; set; }
    [XmlAttribute("maxwidth")] public int MaxWidth { get; set; }
    [XmlAttribute("file")] public string File { get; set; } = null!;

    private const int DefaultFps = 5;

    /// <summary>Warning code emitted when starting a new recording discards a completed-but-unfetched GIF.</summary>
    private const string DiscardedWarningCode = "unfetched-recording-discarded";
    private const string DiscardedWarningDetail =
        "A previous recording auto-stopped and its GIF was never fetched; it has been discarded and replaced by this recording.";

    public static List<Command> BuiltInCommands {
        get => [new RecordCommand { Cmd = "record" }];
    }

    // Window resolution happens per-target inside BuildGrabber (a virtual test seam that also owns
    // the region-vs-window branch and its distinct error/audit shapes), not in the base template.
    protected override bool RequiresWindowTarget => false;

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        // Default the action: `oneshot` when a duration is given, else `start`.
        string action = string.IsNullOrWhiteSpace(Action)
            ? (DurationMs > 0 ? "oneshot" : "start")
            : Action.Trim().ToLowerInvariant();

        try {
            return action switch {
                "stop" => DoStop(),
                "start" => DoStart(oneshot: false),
                "oneshot" => DoStart(oneshot: true),
                _ => FailWith($"Unknown record action '{action}'. Use start, stop, or oneshot.",
                    code: "record-action-unknown", category: "invalid-argument"),
            };
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: record failed: {e.Message}");
            return CommandResult.Fail(Cmd, $"Record failed: {e.Message}", "record-exception", "internal");
        }
    }

    /// <summary>True when the command targets an explicit screen region (no window selector given).</summary>
    private bool IsRegionTarget => Width > 0 && Height > 0 && !HasWindowTarget;

    /// <summary>Starts a recording; for a one-shot, also waits the duration and stops/encodes/writes.</summary>
    private CommandResult DoStart(bool oneshot) {
        if (GifRecorder.IsRecording) {
            // invalid-argument: the request cannot apply in the current state; stop the active
            // recording first; retrying the same start unchanged will keep failing.
            return FailWith("A recording is already in progress. Stop it first (action=stop).",
                code: "recording-in-progress", category: "invalid-argument");
        }

        // SECURITY (#158): an explicit record region feeds the same CaptureRegionBitmap as
        // `capture`, so its agent-controlled dimensions get the same fail-fast cap; reject
        // before any recording starts (window targets are naturally bounded by window size).
        if (IsRegionTarget) {
            string? sizeError = ScreenCapture.ValidateRegionSize(Width, Height);
            if (sizeError is not null) {
                AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height} REJECTED; {sizeError}");
                // invalid-argument (#191): the recovery is to shrink the request, not broaden a selector.
                return FailWith(sizeError, code: "region-too-large", category: "invalid-argument");
            }
        }

        RecordLimits limits = ResolveLimits(oneshot);

        Func<Bitmap>? grab = BuildGrabber(out JsonNode? target, out string? error);
        if (grab is null) {
            AgentRuntime.Audit(Cmd, error ?? "no matching window");
            return FailWith(error ?? "No matching window", code: "window-not-found", category: "no-target");
        }

        AgentRuntime.Audit(Cmd, $"start {DescribeTarget(target)} fps={limits.Fps} oneshot={oneshot} durationMs={(oneshot ? limits.LoopDurationMs : 0)}");
        bool discardedUnfetched = GifRecorder.Start(grab, limits.Fps, limits.MaxFrames, limits.MaxWidth, limits.LoopDurationMs, target);
        if (discardedUnfetched) {
            // A prior recording auto-stopped (max duration/frames) and its GIF was never fetched with
            // action=stop. The new recording (start OR oneshot) replaces it; warn so the loss is
            // visible, not silent.
            Logger.Instance.Log4.Warn($"{GetType().Name}: a previous recording auto-stopped and was never fetched; its buffered GIF was discarded by this new recording.");
            AgentRuntime.Audit(Cmd, $"{(oneshot ? "oneshot" : "start")}; discarded an unfetched auto-stopped recording");
        }

        if (!oneshot) {
            JsonObject data = new() {
                ["recording"] = true,
                ["fps"] = limits.Fps,
                ["maxDurationMs"] = limits.LoopDurationMs,
                ["target"] = target?.DeepClone(),
            };
            CommandResult result = CommandResult.Ok(Cmd, data);
            if (discardedUnfetched) {
                result.Warn(DiscardedWarningCode, DiscardedWarningDetail);
            }
            return result;
        }

        // One-shot: wait for the bounded loop to finish (it auto-stops at loopDurationMs), with a small
        // grace so the final frame is captured, then stop + encode + write. The oneshot's single reply
        // comes from DoStop, so the discard warning must ride along or it would be silently dropped.
        Thread.Sleep((int)Math.Min(limits.LoopDurationMs, int.MaxValue) + 200);
        return DoStop(discardedUnfetched);
    }

    /// <summary>Resolves and clamps fps/duration/frames/width against the operator's policy, auditing clamps.</summary>
    private RecordLimits ResolveLimits(bool oneshot) {
        AppSettings? settings = AgentRuntime.Settings;
        int maxFps = settings?.AgentRecordMaxFps > 0 ? settings.AgentRecordMaxFps : 30;
        int maxDurationMs = settings?.AgentRecordMaxDurationMs > 0 ? settings.AgentRecordMaxDurationMs : 60000;
        int maxFrames = settings?.AgentRecordMaxFrames > 0 ? settings.AgentRecordMaxFrames : 600;
        int settingsMaxWidth = settings?.AgentRecordMaxWidth > 0 ? settings.AgentRecordMaxWidth : 1280;

        if (Fps > maxFps) {
            AgentRuntime.Audit(Cmd, $"fps {Fps} clamped to limit {maxFps}");
        }
        if (oneshot && DurationMs > maxDurationMs) {
            AgentRuntime.Audit(Cmd, $"durationMs {DurationMs} clamped to limit {maxDurationMs}");
        }

        // For a one-shot the requested duration bounds the loop; for an open start the operator's max
        // duration is the safety auto-stop.
        long loopDurationMs = oneshot
            ? Clamp(DurationMs > 0 ? DurationMs : maxDurationMs, 1, maxDurationMs)
            : maxDurationMs;

        return new RecordLimits {
            Fps = Clamp(Fps > 0 ? Fps : DefaultFps, 1, maxFps),
            MaxFrames = maxFrames,
            MaxWidth = Clamp(MaxWidth > 0 ? MaxWidth : settingsMaxWidth, 1, settingsMaxWidth),
            LoopDurationMs = loopDurationMs,
        };
    }

    /// <summary>
    /// Builds the per-frame grabber for the requested target (explicit region, else resolved window).
    /// Returns null with <paramref name="errorMessage"/> set when no window matches. Virtual so tests can
    /// substitute a synthetic in-memory grabber and exercise start/oneshot without touching the desktop.
    /// </summary>
    protected virtual Func<Bitmap>? BuildGrabber(out JsonNode? target, out string? errorMessage) {
        errorMessage = null;
        if (IsRegionTarget) {
            int rx = X, ry = Y, rw = Width, rh = Height;
            target = new JsonObject {
                ["type"] = "region",
                ["x"] = rx,
                ["y"] = ry,
                ["width"] = rw,
                ["height"] = rh,
            };
            return () => ScreenCapture.CaptureRegionBitmap(rx, ry, rw, rh);
        }

        WindowInfo? win = ResolveTargetWindow();
        if (win is null) {
            target = null;
            errorMessage = "No matching window";
            return null;
        }
        IntPtr hwnd = new(win.Handle);
        target = win.ToJsonObject();
        return () => ScreenCapture.CaptureWindowBitmap(hwnd);
    }

    private static string DescribeTarget(JsonNode? target) {
        if (target is not JsonObject obj) {
            return "target";
        }
        if (obj["type"]?.GetValue<string>() == "region") {
            return $"region ({obj["x"]},{obj["y"]}) {obj["width"]}x{obj["height"]}";
        }
        return $"window 0x{obj["handle"]?.GetValue<long>():X} \"{obj["title"]?.GetValue<string>()}\" ({obj["processName"]?.GetValue<string>()})";
    }

    /// <summary>Stops the active recording (or fetches one that already auto-stopped at its limits),
    /// encodes the GIF, writes it to disk, and replies metadata. <paramref name="warnDiscardedUnfetched"/>
    /// is set by the oneshot path when its start discarded an unfetched auto-stopped GIF, so the
    /// warning surfaces on the oneshot's single (stop-produced) reply.</summary>
    private CommandResult DoStop(bool warnDiscardedUnfetched = false) {
        RecordingResult? result = GifRecorder.Stop();
        if (result is null) {
            // invalid-argument: there is nothing to stop/fetch; start a recording first.
            return FailWith("No recording is in progress or awaiting fetch.", warnDiscardedUnfetched,
                code: "no-recording", category: "invalid-argument");
        }

        if (result.Frames == 0 || result.Gif.Length == 0) {
            AgentRuntime.Audit(Cmd, $"stop; no output ({result.Error})");
            return FailWith(result.Error ?? "Recording produced no frames.", warnDiscardedUnfetched,
                code: "record-no-frames", category: "internal");
        }

        string path = string.IsNullOrEmpty(File)
            ? Path.Combine(Path.GetTempPath(), $"mcec-rec-{DateTime.Now:yyyyMMdd-HHmmss}.gif")
            : File;

        JsonObject data = new() {
            ["frames"] = result.Frames,
            ["durationMs"] = result.DurationMs,
            ["fps"] = result.Fps,
            ["width"] = result.Width,
            ["height"] = result.Height,
            ["bytes"] = result.Gif.Length,
            ["target"] = result.Target?.DeepClone(),
        };
        if (result.Error is not null) {
            data["warning"] = result.Error;
        }

        try {
            System.IO.File.WriteAllBytes(path, result.Gif);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Unlike `capture`, `record` does not return the bytes inline, so a failed write means there
            // is no usable output; report failure rather than success-with-fileError.
            Logger.Instance.Log4.Error($"{GetType().Name}: could not write GIF to '{path}': {e.Message}");
            AgentRuntime.Audit(Cmd, $"stop; encode ok ({result.Frames} frames) but write failed: {e.Message}");
            return FailWith($"Recorded {result.Frames} frames but could not write GIF to '{path}': {e.Message}",
                warnDiscardedUnfetched, code: "record-write-failed", category: "internal");
        }

        data["file"] = path;
        AgentRuntime.Audit(Cmd, $"stop; wrote {result.Frames} frames, {result.Gif.Length} bytes, {result.Width}x{result.Height} to {path}");
        CommandResult ok = CommandResult.Ok(Cmd, data);
        if (warnDiscardedUnfetched) {
            ok.Warn(DiscardedWarningCode, DiscardedWarningDetail);
        }
        return ok;
    }

    /// <summary>Builds a failure result carrying the structured code/category taxonomy (#206;
    /// mandatory for every record failure); the discard warning still rides along when set;
    /// warnings are valid on failure too, and the discard already happened regardless of this
    /// call's outcome.</summary>
    private CommandResult FailWith(string error, bool warnDiscardedUnfetched = false, string? code = null, string? category = null) {
        CommandResult result = CommandResult.Fail(Cmd, error, code ?? "unhandled", category ?? "internal");
        if (warnDiscardedUnfetched) {
            result.Warn(DiscardedWarningCode, DiscardedWarningDetail);
        }
        return result;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
