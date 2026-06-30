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
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
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

                byte[] png = ScreenCapture.CaptureRegion(X, Y, Width, Height);
                data = new JsonObject {
                    ["encoding"] = "png",
                    ["width"] = Width,
                    ["height"] = Height,
                    ["bytes"] = png.Length,
                    ["base64"] = Convert.ToBase64String(png),
                };
                WriteFileIfRequested(png, data);
                Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
                return true;
            }

            WindowInfo? win = WindowResolver.Resolve(
                Handle > 0 ? Handle : (long?)null, Window, Process, ClassName, Foreground);
            if (win is null) {
                AgentRuntime.Audit(Cmd, "no matching window");
                Reply?.WriteLine(CommandResult.Fail(Cmd, "No matching window").ToJson());
                return false;
            }

            AgentRuntime.Audit(Cmd, $"window 0x{win.Handle:X} \"{win.Title}\" ({win.ProcessName})");

            byte[] winPng = ScreenCapture.CaptureWindow(new IntPtr(win.Handle));
            data = new JsonObject {
                ["handle"] = win.Handle,
                ["width"] = win.Width,
                ["height"] = win.Height,
                ["encoding"] = "png",
                ["bytes"] = winPng.Length,
                ["base64"] = Convert.ToBase64String(winPng),
                ["window"] = win.ToJsonObject(),
            };
            WriteFileIfRequested(winPng, data);
            Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
            return true;
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: Capture failed: {e.Message}");
            Reply?.WriteLine(CommandResult.Fail(Cmd, $"Capture failed: {e.Message}").ToJson());
            return false;
        }
    }

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
