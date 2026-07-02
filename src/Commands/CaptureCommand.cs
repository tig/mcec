// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// MCEC 3.0 agent "see the screen" command. Captures a target window (by handle / title substring /
/// process / class / foreground) or an explicit screen region, encodes it as PNG, and returns a
/// <see cref="CommandResult"/> carrying base64 image bytes (and optionally writes the PNG to a file).
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> (a separate opt-in from the
/// actuation enable) and every capture is audited via <see cref="AgentRuntime.Audit"/>.
/// </summary>
public class CaptureCommand : Command {
    [XmlAttribute("window")]
    public string Window { get; set; } = null!;

    [XmlAttribute("handle")]
    public long Handle { get; set; }

    [XmlAttribute("process")]
    public string Process { get; set; } = null!;

    [XmlAttribute("classname")]
    public string ClassName { get; set; } = null!;

    [XmlAttribute("foreground")]
    public bool Foreground { get; set; }

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

    public static new List<Command> BuiltInCommands {
        get => [new CaptureCommand { Cmd = "capture" }];
    }

    public CaptureCommand() { }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new CaptureCommand {
        Window = Window,
        Handle = Handle,
        Process = Process,
        ClassName = ClassName,
        Foreground = Foreground,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        File = File,
    });

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED; agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }

        bool hasWindowTarget = !string.IsNullOrEmpty(Window)
            || Handle > 0
            || !string.IsNullOrEmpty(Process)
            || !string.IsNullOrEmpty(ClassName)
            || Foreground;

        try {
            JsonObject data;

            if (Width > 0 && Height > 0 && !hasWindowTarget) {
                AgentRuntime.Audit(Cmd, $"region ({X},{Y}) {Width}x{Height}");

                CaptureResult regionCap = ScreenCapture.CaptureRegion(X, Y, Width, Height);
                data = new JsonObject {
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
                Reply?.WriteLine(regionRes.ToJson());
                return true;
            }

            WindowInfo? win = WindowResolver.Resolve(
                Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
            if (win is null) {
                AgentRuntime.Audit(Cmd, "no matching window");
                Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window", "window-not-found", "no-target").ToJson());
                return false;
            }

            AgentRuntime.Audit(Cmd, $"window 0x{win.Handle:X} \"{win.Title}\" ({win.ProcessName})");

            CaptureResult cap = ScreenCapture.CaptureWindow(new IntPtr(win.Handle));
            data = new JsonObject {
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
            // The PNG stays in `data` (so the agent still sees what was grabbed and it can serve as the
            // contract's lastObservation), but the result is flagged capture-blank so the agent branches.
            CommandResult res;
            bool ok;
            if (cap.Stats.IsBlank) {
                string code = cap.Stats.DominantIsDark ? "frame-all-black" : "frame-uniform";
                string detail = cap.UsedFallback
                    ? "Captured frame is blank; PrintWindow was refused and the on-screen-blit fallback returned a flat image (composited/occluded/minimized window or a locked session)."
                    : "Captured frame is blank (a flat fill); the window may be minimized, cloaked, or rendering off-screen.";
                AgentRuntime.Audit(Cmd, $"blank frame ({code}, dominant {cap.Stats.DominantFraction:P0}, fallback={cap.UsedFallback})");
                res = CommandResult.Fail(Cmd, detail, code, "capture-blank", data);
                ok = false;
            }
            else {
                res = CommandResult.Ok(Cmd, data);
                ok = true;
            }
            if (cap.UsedFallback) {
                res.Warn("capture-fallback", "PrintWindow was refused; used an on-screen blit, which returns black for composited/occluded surfaces and cannot see windows behind others.");
            }
            Reply?.WriteLine(res.ToJson());
            return ok;
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Capture failed: {e.Message}");
            Reply?.WriteLine(CommandResult.Fail(Cmd, $"Capture failed: {e.Message}").ToJson());
            return false;
        }
    }

    /// <summary>Serializes the blank-frame analysis so an agent can see why a capture was flagged.</summary>
    private static JsonObject BlankCheckJson(ImageStats stats) => new() {
        ["blank"] = stats.IsBlank,
        ["dominantFraction"] = System.Math.Round(stats.DominantFraction, 4),
        ["dominantIsDark"] = stats.DominantIsDark,
    };

    /// <summary>
    /// Writes the captured PNG to <see cref="File"/> if set, recording the path in <paramref name="data"/>.
    /// File IO failures are non-fatal: the base64 image is still returned and the error noted in data.
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
