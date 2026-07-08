// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// MCEC 3.0 agent "read text from screen" command (#331). Captures a target window (by handle / title
/// substring / process / class / foreground), an explicit screen region, or a window-relative sub-region,
/// runs Windows OCR on the pixels, and returns the extracted string plus metadata.
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited via
/// <see cref="AgentRuntime.Audit"/>.
/// </summary>
public class GetTextCommand : WindowTargetingAgentCommand {
    [XmlAttribute("x")]
    public int X { get; set; }

    [XmlAttribute("y")]
    public int Y { get; set; }

    [XmlAttribute("width")]
    public int Width { get; set; }

    [XmlAttribute("height")]
    public int Height { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new GetTextCommand { Cmd = "get-text" }];
    }

    /// <summary>True when width/height specify a region.</summary>
    private bool HasRegion => Width > 0 && Height > 0;

    /// <summary>True when this is a screen-absolute region capture (region given, no window selector).</summary>
    private bool IsScreenRegion => HasRegion && !HasWindowTarget;

    // Window resolution happens inside ExecuteCore (like record's grabber-owned resolution), so a
    // screen-absolute region needs no window and window-relative regions still resolve here.
    protected override bool RequiresWindowTarget => false;

    /// <summary>Virtual seam so tests can supply a resolved window without enumerating the desktop.</summary>
    protected virtual WindowInfo? TryResolveWindow() => ResolveTargetWindow();

    protected override CommandResult OnWindowNotFound() {
        AgentRuntime.Audit(Cmd, "no matching window");
        return base.OnWindowNotFound();
    }

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        try {
            if (IsScreenRegion) {
                return ReadScreenRegion();
            }

            WindowInfo? win = TryResolveWindow();
            if (win is null) {
                return OnWindowNotFound();
            }

            return ReadWindow(win);
        }
        catch (InvalidOperationException e) when (e.Message.Contains("OCR", StringComparison.OrdinalIgnoreCase)) {
            AgentRuntime.Audit(Cmd, "ocr unavailable");
            return CommandResult.Fail(Cmd, e.Message, "ocr-unavailable", "internal");
        }
        catch (ArgumentException e) when (e.Message.Contains("OCR", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("dimension", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("exceed", StringComparison.OrdinalIgnoreCase)) {
            // CR P2 (PR 334): oversized for OCR -> invalid-argument so agent can shrink region (distinct from ocr-unavailable).
            AgentRuntime.Audit(Cmd, "ocr region exceeds engine limit");
            return CommandResult.Fail(Cmd, e.Message, "ocr-image-too-large", "invalid-argument");
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: get-text failed: {e.Message}");
            return CommandResult.Fail(Cmd, $"get-text failed: {e.Message}", "get-text-exception", "internal");
        }
    }

    private CommandResult ReadScreenRegion() {
        string? sizeError = ScreenCapture.ValidateRegionSize(Width, Height);
        if (sizeError is not null) {
            AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height} REJECTED; {sizeError}");
            return CommandResult.Fail(Cmd, sizeError, "region-too-large", "invalid-argument");
        }

        AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height}");
        using Bitmap bmp = ScreenCapture.CaptureRegionBitmap(X, Y, Width, Height);
        return FinishOcr(bmp, target: null, regionRelativeTo: "screen", regionX: X, regionY: Y);
    }

    private CommandResult ReadWindow(WindowInfo win) {
        AgentRuntime.Audit(Cmd, DescribeWindowTarget(win));

        using Bitmap windowBmp = CaptureWindowBitmap(win, out bool usedFallback);
        Bitmap regionBmp;
        bool disposeRegion;
        if (HasRegion) {
            string? cropError = TryCropWindowRegion(windowBmp, out regionBmp);
            if (cropError is not null) {
                AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height} REJECTED; {cropError}");
                return CommandResult.Fail(Cmd, cropError, "region-out-of-bounds", "invalid-argument");
            }
            disposeRegion = true;
        }
        else {
            regionBmp = windowBmp;
            disposeRegion = false;
        }
        try {
            CommandResult result = FinishOcr(
                regionBmp,
                win,
                regionRelativeTo: HasRegion ? "window" : null,
                regionX: HasRegion ? X : 0,
                regionY: HasRegion ? Y : 0);
            if (usedFallback) {
                result.Warn("ocr-fallback",
                    "PrintWindow was refused; OCR ran on an on-screen blit, which may be wrong for composited/occluded surfaces.");
            }
            return result;
        }
        finally {
            if (disposeRegion) {
                regionBmp.Dispose();
            }
        }
    }

    /// <summary>Virtual seam so tests can stub window capture without touching the desktop.</summary>
    protected virtual Bitmap CaptureWindowBitmap(WindowInfo win, out bool usedFallback) {
        CaptureResult cap = ScreenCapture.CaptureWindow(new IntPtr(win.Handle));
        usedFallback = cap.UsedFallback;
        // CR P1 (PR 334): Bitmap(Stream) requires the stream to stay open for the Bitmap lifetime.
        // Decode while open, then clone the pixels so the returned Bitmap is independent and the
        // temp stream can be disposed. Downstream AnalyzeBlank/RegionOcr (which Save again) stay safe.
        using MemoryStream ms = new(cap.Png);
        using Bitmap tmp = new Bitmap(ms);
        return new Bitmap(tmp);
    }

    /// <summary>Virtual seam so tests can stub OCR without requiring a language pack.</summary>
    protected virtual RegionOcrResult Recognize(Bitmap bitmap) => RegionOcr.Recognize(bitmap);

    private string? TryCropWindowRegion(Bitmap windowBmp, out Bitmap cropped) {
        string? sizeError = ScreenCapture.ValidateRegionSize(Width, Height);
        if (sizeError is not null) {
            cropped = windowBmp;
            return sizeError;
        }

        if (X < 0 || Y < 0 || X + Width > windowBmp.Width || Y + Height > windowBmp.Height) {
            cropped = windowBmp;
            return $"Region ({X},{Y}) {Width}x{Height} is outside the {windowBmp.Width}x{windowBmp.Height} window capture. " +
                   "Re-query for fresh bounds or shrink the region.";
        }

        cropped = ScreenCapture.CropBitmap(windowBmp, X, Y, Width, Height);
        return null;
    }

    private CommandResult FinishOcr(
        Bitmap bmp,
        WindowInfo? target,
        string? regionRelativeTo,
        int regionX,
        int regionY) {
        ImageStats blank = ScreenCapture.AnalyzeBlank(bmp);
        JsonObject data = BuildData(bmp, target, regionRelativeTo, regionX, regionY, blank);

        if (blank.IsBlank) {
            string code = blank.DominantIsDark ? "frame-all-black" : "frame-uniform";
            string detail = "Captured region is blank (a flat fill); the window may be minimized, cloaked, or off-screen.";
            AgentRuntime.Audit(Cmd, $"blank frame ({code}, dominant {blank.DominantFraction:P0})");
            return CommandResult.Fail(Cmd, detail, code, "ocr-blank", data);
        }

        RegionOcrResult ocr = Recognize(bmp);
        data["text"] = ocr.Text;
        data["lineCount"] = ocr.LineCount;
        data["wordCount"] = ocr.WordCount;
        if (!string.IsNullOrEmpty(ocr.Language)) {
            data["language"] = ocr.Language;
        }

        if (string.IsNullOrWhiteSpace(ocr.Text)) {
            AgentRuntime.Audit(Cmd, "ocr returned no text");
            return CommandResult.Fail(Cmd,
                "OCR could not read any text in the region (low contrast, unsupported script, or too small). " +
                "Try a larger region, foreground the window, or fall back to capture.",
                "ocr-no-text", "ocr-no-text", data);
        }

        AgentRuntime.Audit(Cmd, $"ocr {ocr.WordCount} word(s), {ocr.LineCount} line(s)");
        return CommandResult.Ok(Cmd, data);
    }

    private JsonObject BuildData(
        Bitmap bmp,
        WindowInfo? target,
        string? regionRelativeTo,
        int regionX,
        int regionY,
        ImageStats blank) {
        JsonObject data = new() {
            ["width"] = bmp.Width,
            ["height"] = bmp.Height,
            ["blankCheck"] = BlankCheckJson(blank),
        };

        if (regionRelativeTo is not null) {
            data["region"] = new JsonObject {
                ["x"] = regionX,
                ["y"] = regionY,
                ["width"] = HasRegion ? Width : bmp.Width,
                ["height"] = HasRegion ? Height : bmp.Height,
                ["relativeTo"] = regionRelativeTo,
            };
        }

        if (target is not null) {
            data["window"] = target.ToJsonObject();
        }

        return data;
    }

    private static JsonObject BlankCheckJson(ImageStats stats) => new() {
        ["blank"] = stats.IsBlank,
        ["dominantFraction"] = Math.Round(stats.DominantFraction, 4),
        ["dominantIsDark"] = stats.DominantIsDark,
    };

    private static string DescribeWindowTarget(WindowInfo win) =>
        $"window 0x{win.Handle:X} \"{win.Title}\" ({win.ProcessName})";
}