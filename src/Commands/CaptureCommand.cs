// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// MCEC 3.0 agent "see the screen" command. Captures a target window (by handle / title substring /
/// process / class / foreground) or an explicit screen region, encodes it as PNG, and returns a
/// <see cref="CommandResult"/> carrying image metadata + inline bytes (unless the caller later requests
/// path-only shaping in the MCP layer); optionally writes the PNG to a file.
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> (a separate opt-in from the
/// actuation enable; enforced structurally by <see cref="AgentCommand"/>) and every capture is
/// audited via <see cref="AgentRuntime.Audit"/>.
/// </summary>
public class CaptureCommand : WindowTargetingAgentCommand {
    [XmlAttribute("x")]
    public int X { get; set; }

    [XmlAttribute("y")]
    public int Y { get; set; }

    [XmlAttribute("width")]
    public int Width { get; set; }

    [XmlAttribute("height")]
    public int Height { get; set; }

    [XmlAttribute("file")]
    public string File { get; set; } = null!;

    [XmlIgnore]
    public int MaxWidth { get; set; }

    [XmlIgnore]
    public double Scale { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new CaptureCommand { Cmd = "capture" }];
    }

    /// <summary>True when this is an explicit-region capture (region given, no window selector).</summary>
    private bool IsRegionCapture => Width > 0 && Height > 0 && !HasWindowTarget;

    // A region capture never resolves a window; everything else (including no selectors at all,
    // which fails as window-not-found) does.
    protected override bool RequiresWindowTarget => !IsRegionCapture;

    protected override CommandResult OnWindowNotFound() {
        AgentRuntime.Audit(Cmd, "no matching window");
        return base.OnWindowNotFound();
    }

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        try {
            return target is null ? CaptureRegion() : CaptureWindow(target);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Capture failed: {e.Message}");
            return CommandResult.Fail(Cmd, $"Capture failed: {e.Message}", "capture-exception", "internal");
        }
    }

    /// <summary>The explicit-region path (no window was required or resolved).</summary>
    private CommandResult CaptureRegion() {
        // SECURITY (#158): region dimensions are agent-controlled; reject oversized
        // requests BEFORE any bitmap/PNG/base64 allocation, with a diagnosable envelope.
        // Category invalid-argument (#191): the recovery is to SHRINK the request, not to broaden
        // a selector; no-target's documented recovery would send the agent the wrong way.
        string? sizeError = ScreenCapture.ValidateRegionSize(Width, Height);
        if (sizeError is not null) {
            AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height} REJECTED; {sizeError}");
            return CommandResult.Fail(Cmd, sizeError, "region-too-large", "invalid-argument");
        }

        AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height}");

        CaptureResult regionCap = ScreenCapture.CaptureRegion(X, Y, Width, Height);
        regionCap = ApplyDownscaleIfRequested(regionCap);
        JsonObject data = new JsonObject {
            ["encoding"] = "png",
            ["width"] = regionCap.Width,
            ["height"] = regionCap.Height,
            ["bytes"] = regionCap.Png.Length,
            ["base64"] = Convert.ToBase64String(regionCap.Png),
            ["blankCheck"] = BlankCheckJson(regionCap.Stats),
        };
        WriteFileIfRequested(regionCap.Png, data);

        // A user-specified region can legitimately be empty, so a blank region is a non-fatal
        // warning (not a capture-blank error); the agent still gets the image and the signal.
        CommandResult regionRes = CommandResult.Ok(Cmd, data);
        if (regionCap.Stats.IsBlank) {
            regionRes.Warn("capture-blank", "Captured region is blank (a flat fill); it may be off-screen or genuinely empty.");
        }
        return regionRes;
    }

    /// <summary>The resolved-window path.</summary>
    private CommandResult CaptureWindow(WindowInfo win) {
        AgentRuntime.Audit(Cmd, $"window 0x{win.Handle:X} \"{win.Title}\" ({win.ProcessName})");

        CaptureResult cap = ScreenCapture.CaptureWindow(new IntPtr(win.Handle));
        cap = ApplyDownscaleIfRequested(cap);
        JsonObject data = new JsonObject {
            ["handle"] = win.Handle,
            ["width"] = cap.Width,
            ["height"] = cap.Height,
            ["encoding"] = "png",
            ["bytes"] = cap.Png.Length,
            ["base64"] = Convert.ToBase64String(cap.Png),
            ["window"] = win.ToJsonObject(),
            ["blankCheck"] = BlankCheckJson(cap.Stats),
        };
        WriteFileIfRequested(cap.Png, data);

        // A blank window frame is a hard observation failure: don't return a silent bad image.
        // The PNG stays in `data` (carried into the envelope's error.partialResult, #206; the agent
        // still sees what was grabbed), and the result is flagged capture-blank so the agent branches.
        CommandResult res;
        if (cap.Stats.IsBlank) {
            string code = cap.Stats.DominantIsDark ? "frame-all-black" : "frame-uniform";
            string detail = cap.UsedFallback
                ? "Captured frame is blank; PrintWindow was refused and the on-screen-blit fallback returned a flat image (composited/occluded/minimized window or a locked session)."
                : "Captured frame is blank (a flat fill); the window may be minimized, cloaked, or rendering off-screen.";
            AgentRuntime.Audit(Cmd, $"blank frame ({code}, dominant {cap.Stats.DominantFraction:P0}, fallback={cap.UsedFallback})");
            res = CommandResult.Fail(Cmd, detail, code, "capture-blank", data);
        }
        else {
            res = CommandResult.Ok(Cmd, data);
        }
        if (cap.UsedFallback) {
            res.Warn("capture-fallback", "PrintWindow was refused; used an on-screen blit, which returns black for composited/occluded surfaces and cannot see windows behind others.");
        }
        return res;
    }

    private CaptureResult ApplyDownscaleIfRequested(CaptureResult cap) {
        int targetWidth = cap.Width;
        if (MaxWidth > 0) {
            targetWidth = Math.Min(targetWidth, MaxWidth);
        }
        if (Scale > 0) {
            targetWidth = Math.Min(targetWidth, Math.Max(1, (int)Math.Round(cap.Width * Scale)));
        }
        if (targetWidth >= cap.Width) {
            return cap;
        }
        using MemoryStream input = new(cap.Png);
        using Bitmap src = new(input);
        int targetHeight = Math.Max(1, (int)Math.Round(src.Height * (targetWidth / (double)src.Width)));
        using Bitmap scaled = new(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(scaled)) {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, targetWidth, targetHeight);
        }
        using MemoryStream output = new();
        scaled.Save(output, ImageFormat.Png);
        return new CaptureResult(output.ToArray(), targetWidth, targetHeight, cap.UsedFallback, ScreenCapture.AnalyzeBlank(scaled));
    }

    /// <summary>Serializes the blank-frame analysis so an agent can see why a capture was flagged.</summary>
    private static JsonObject BlankCheckJson(ImageStats stats) => new() {
        ["blank"] = stats.IsBlank,
        ["dominantFraction"] = Math.Round(stats.DominantFraction, 4),
        ["dominantIsDark"] = stats.DominantIsDark,
    };

    /// <summary>
    /// Writes the captured PNG to <see cref="File"/> if set, recording the path in <paramref name="data"/>.
    /// File IO failures are non-fatal: the capture result still returns metadata (and inline bytes unless
    /// path-only shaping is requested at the MCP layer), with the write error noted in data.
    /// </summary>
    private void WriteFileIfRequested(byte[] png, JsonObject data) {
        if (string.IsNullOrEmpty(File)) {
            return;
        }

        try {
            System.IO.File.WriteAllBytes(File, png);
            data["file"] = File;
        }
        catch (IOException e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Could not write capture to '{File}': {e.Message}");
            data["fileError"] = e.Message;
        }
        catch (UnauthorizedAccessException e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Could not write capture to '{File}': {e.Message}");
            data["fileError"] = e.Message;
        }
    }
}
