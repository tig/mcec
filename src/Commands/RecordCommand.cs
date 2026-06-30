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
/// MCEC 3.0 agent <c>record</c> command: captures agent-driven desktop activity to an animated GIF —
/// a whole short segment via explicit <c>start</c>/<c>stop</c>, or a bounded one-shot via
/// <c>durationMs</c>. Reuses the same target model as <see cref="CaptureCommand"/> (window by
/// handle/title/process/class/foreground, or an explicit region) and the same security gate.
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> (a separate opt-in from
/// actuation) and the per-command <c>Enabled</c> flag; every start/stop/write is audited via
/// <see cref="AgentRuntime.Audit"/>. The capture loop is hard-bounded (fps/duration/frames/width) by
/// the operator's <see cref="AppSettings"/> limits so an agent cannot create an unbounded file.
///
/// PRIVACY: a recording captures whatever is on screen for its whole duration — louder than a single
/// still <c>capture</c>. See <c>docs/agent-server.md</c>.
/// </summary>
public class RecordCommand : Command {
    [XmlAttribute("action")] public string Action { get; set; } = null!;
    [XmlAttribute("window")] public string Window { get; set; } = null!;
    [XmlAttribute("handle")] public long Handle { get; set; }
    [XmlAttribute("process")] public string Process { get; set; } = null!;
    [XmlAttribute("className")] public string ClassName { get; set; } = null!;
    [XmlAttribute("foreground")] public bool Foreground { get; set; }
    [XmlAttribute("x")] public int X { get; set; }
    [XmlAttribute("y")] public int Y { get; set; }
    [XmlAttribute("width")] public int Width { get; set; }
    [XmlAttribute("height")] public int Height { get; set; }
    [XmlAttribute("fps")] public int Fps { get; set; }
    [XmlAttribute("durationMs")] public int DurationMs { get; set; }
    [XmlAttribute("maxWidth")] public int MaxWidth { get; set; }
    [XmlAttribute("file")] public string File { get; set; } = null!;

    private const int DefaultFps = 5;

    public static new List<Command> BuiltInCommands {
        get => [new RecordCommand { Cmd = "record" }];
    }

    public RecordCommand() { }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new RecordCommand {
        Action = Action,
        Window = Window,
        Handle = Handle,
        Process = Process,
        ClassName = ClassName,
        Foreground = Foreground,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        Fps = Fps,
        DurationMs = DurationMs,
        MaxWidth = MaxWidth,
        File = File,
    });

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }

        // Default the action: `oneshot` when a duration is given, else `start`.
        string action = string.IsNullOrWhiteSpace(Action)
            ? (DurationMs > 0 ? "oneshot" : "start")
            : Action.Trim().ToLowerInvariant();

        try {
            return action switch {
                "stop" => DoStop(),
                "start" => DoStart(oneshot: false),
                "oneshot" => DoStart(oneshot: true),
                _ => FailWith($"Unknown record action '{action}'. Use start, stop, or oneshot."),
            };
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: record failed: {e.Message}");
            Reply?.WriteLine(CommandResult.Fail(Cmd, $"Record failed: {e.Message}").ToJson());
            return false;
        }
    }

    /// <summary>Starts a recording; for a one-shot, also waits the duration and stops/encodes/writes.</summary>
    private bool DoStart(bool oneshot) {
        if (GifRecorder.IsRecording) {
            return FailWith("A recording is already in progress. Stop it first (action=stop).");
        }

        RecordLimits limits = ResolveLimits(oneshot);

        Func<Bitmap>? grab = BuildGrabber(out JsonNode? target, out string? error);
        if (grab is null) {
            AgentRuntime.Audit(Cmd, error ?? "no matching window");
            return FailWith(error ?? "No matching window");
        }

        AgentRuntime.Audit(Cmd, $"start {DescribeTarget(target)} fps={limits.Fps} oneshot={oneshot} durationMs={(oneshot ? limits.LoopDurationMs : 0)}");
        GifRecorder.Start(grab, limits.Fps, limits.MaxFrames, limits.MaxWidth, limits.LoopDurationMs, target);

        if (!oneshot) {
            JsonObject data = new() {
                ["recording"] = true,
                ["fps"] = limits.Fps,
                ["maxDurationMs"] = limits.LoopDurationMs,
                ["target"] = target?.DeepClone(),
            };
            Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
            return true;
        }

        // One-shot: wait for the bounded loop to finish (it auto-stops at loopDurationMs), with a small
        // grace so the final frame is captured, then stop + encode + write.
        Thread.Sleep((int)Math.Min(limits.LoopDurationMs, int.MaxValue) + 200);
        return DoStop();
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
    /// Returns null with <paramref name="error"/> set when no window matches.
    /// </summary>
    private Func<Bitmap>? BuildGrabber(out JsonNode? target, out string? error) {
        error = null;
        bool hasWindowTarget = !string.IsNullOrEmpty(Window)
            || Handle > 0
            || !string.IsNullOrEmpty(Process)
            || !string.IsNullOrEmpty(ClassName)
            || Foreground;

        if (Width > 0 && Height > 0 && !hasWindowTarget) {
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

        WindowInfo? win = WindowResolver.Resolve(
            Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
        if (win is null) {
            target = null;
            error = "No matching window";
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

    /// <summary>Stops the active recording, encodes the GIF, writes it to disk, and replies metadata.</summary>
    private bool DoStop() {
        RecordingResult? result = GifRecorder.Stop();
        if (result is null) {
            return FailWith("No recording is in progress.");
        }

        if (result.Frames == 0 || result.Gif.Length == 0) {
            AgentRuntime.Audit(Cmd, $"stop — no output ({result.Error})");
            return FailWith(result.Error ?? "Recording produced no frames.");
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
            data["file"] = path;
            AgentRuntime.Audit(Cmd, $"stop — wrote {result.Frames} frames, {result.Gif.Length} bytes, {result.Width}x{result.Height} to {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            Logger.Instance.Log4.Error($"{GetType().Name}: could not write GIF to '{path}': {e.Message}");
            data["fileError"] = e.Message;
            AgentRuntime.Audit(Cmd, $"stop — encode ok but write failed: {e.Message}");
        }

        Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
        return true;
    }

    private bool FailWith(string error) {
        Reply?.WriteLine(CommandResult.Fail(Cmd, error).ToJson());
        return false;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private static long Clamp(long value, long min, long max) => Math.Max(min, Math.Min(max, value));
}
