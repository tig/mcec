// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Serialization;
using WindowsInput;

namespace MCEControl;

/// <summary>
/// Agent actuation command: manage a top-level window by handle/title/process/class and optionally drag it
/// visually for move/resize. Supports <c>move</c>, <c>resize</c>, <c>minimize</c>, <c>maximize</c>,
/// <c>restore</c>, and <c>foreground</c>. For <c>move</c>/<c>resize</c>, <c>animate:true</c> renders the
/// screen change as a short drag gesture instead of an instant jump.
/// </summary>
public class WindowCommand : WindowTargetingAgentCommand {
    [XmlAttribute("action")] public string Action { get; set; } = "foreground";
    [XmlAttribute("x")] public int X { get; set; }
    [XmlAttribute("y")] public int Y { get; set; }
    [XmlAttribute("width")] public int Width { get; set; }
    [XmlAttribute("height")] public int Height { get; set; }
    [XmlAttribute("animate")] public bool Animate { get; set; }

    // #314 review: the move/resize dimensions are optional, but XmlSerializer cannot encode a
    // Nullable<int> as an [XmlAttribute] ("XmlAttribute/XmlText cannot be used to encode complex types"),
    // and SerializedCommands builds a single cached serializer over EVERY registered command type at type
    // load. int? attributes here therefore threw a TypeInitializationException that broke all
    // mcec.commands load/save. Keep the XML-facing coordinates non-nullable and carry "was it supplied?"
    // in these bool flags (the same idiom as FocusCommand.PointSpecified), so a literal 0 is not read as
    // "omitted"; the nullable semantics live only in the MCP argument mapping (ToolCatalog.BuildWindowCommand).
    [XmlAttribute("pos")] public bool PositionSpecified { get; set; }
    [XmlAttribute("size")] public bool SizeSpecified { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new WindowCommand { Cmd = "window" }];
    }

    protected override string AuditDetails() =>
        $"window action='{NormalizedAction}' window handle={Handle} title='{Window}' process='{Process}'";

    protected override CommandResult ExecuteCore(WindowInfo? target) {
        WindowInfo window = target!;
        string action = NormalizedAction;
        string? argsError = ValidateArguments(action, PositionSpecified, SizeSpecified);
        if (argsError is not null) {
            return CommandResult.Fail(Cmd, argsError, "invalid-argument", "invalid-argument");
        }

        IntPtr hwnd = new(window.Handle);
        switch (action) {
            case "move":
                if (Animate) {
                    AnimateMove(hwnd, window, X, Y);
                }
                else {
                    ApplyMove(hwnd, window, X, Y);
                }
                break;
            case "resize":
                if (Animate) {
                    AnimateResize(hwnd, window, Width, Height);
                }
                else {
                    ApplyResize(hwnd, window, Width, Height);
                }
                break;
            case "minimize":
                ApplyShowWindow(hwnd, AgentNativeMethods.SW_MINIMIZE);
                break;
            case "maximize":
                ApplyShowWindow(hwnd, AgentNativeMethods.SW_MAXIMIZE);
                break;
            case "restore":
                ApplyShowWindow(hwnd, AgentNativeMethods.SW_RESTORE);
                break;
            case "foreground":
                if (!FocusService.BringToForeground(hwnd)) {
                    return ForegroundFailure();
                }
                break;
            default:
                return CommandResult.Fail(Cmd, $"Unsupported window action '{Action}'.", "invalid-argument", "invalid-argument");
        }

        WindowInfo updated = WindowResolver.Describe(hwnd) ?? window;
        JsonObject data = new() {
            ["action"] = action,
            ["animate"] = Animate && (action is "move" or "resize"),
            ["window"] = updated.ToJsonObject(),
        };
        return CommandResult.Ok(Cmd, data);
    }

    /// <summary>
    /// Validates the action's required dimensions: <c>move</c> needs an x/y target
    /// (<paramref name="hasPosition"/>), <c>resize</c> needs a width/height target
    /// (<paramref name="hasSize"/>); the state-change actions need neither. Returns an error message or
    /// null when valid.
    /// </summary>
    internal static string? ValidateArguments(string action, bool hasPosition, bool hasSize) =>
        NormalizeAction(action) switch {
            "move" => !hasPosition ? "move requires x and y." : null,
            "resize" => !hasSize ? "resize requires width and height." : null,
            "minimize" or "maximize" or "restore" or "foreground" => null,
            _ => $"Unsupported window action '{action}'.",
        };

    private string NormalizedAction => NormalizeAction(Action);

    private static string NormalizeAction(string? action) => (action ?? "foreground").Trim().ToLowerInvariant() switch {
        "min" or "minimize" or "minimise" => "minimize",
        "max" or "maximize" or "maximise" => "maximize",
        "restore" => "restore",
        "foreground" or "focus" => "foreground",
        "move" => "move",
        "resize" => "resize",
        _ => string.Empty,
    };

    private static void ApplyMove(IntPtr hwnd, WindowInfo window, int x, int y) {
        AgentNativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, window.Width, window.Height, AgentNativeMethods.SWP_NOACTIVATE);
    }

    private static void ApplyResize(IntPtr hwnd, WindowInfo window, int width, int height) {
        AgentNativeMethods.SetWindowPos(hwnd, IntPtr.Zero, window.X, window.Y, width, height, AgentNativeMethods.SWP_NOACTIVATE);
    }

    private static void AnimateMove(IntPtr hwnd, WindowInfo window, int x, int y) {
        List<(int X, int Y)> path = MouseCommand.InterpolatePath([(window.X, window.Y), (x, y)]);
        InputSimulator sim = new();
        MoveToPixel(sim, path[0]);
        Thread.Sleep(90);
        sim.Mouse.LeftButtonDown();
        Thread.Sleep(90);
        for (int i = 1; i < path.Count; i++) {
            if (AgentRuntime.EmergencyStopped) {
                break;
            }
            MoveToPixel(sim, path[i]);
            ApplyMove(hwnd, window, path[i].X, path[i].Y);
            Thread.Sleep(12);
        }
        Thread.Sleep(90);
        sim.Mouse.LeftButtonUp();
        // #314 review: honor the emergency-stop contract. If the operator engaged the panic hotkey
        // mid-drag, the loop broke early; do NOT then teleport the window to the requested destination.
        // Release the button (above) as cleanup, but leave the window where the stop caught it.
        if (!AgentRuntime.EmergencyStopped) {
            ApplyMove(hwnd, window, x, y);
        }
    }

    private static void AnimateResize(IntPtr hwnd, WindowInfo window, int width, int height) {
        (int startX, int startY) = (window.X + window.Width, window.Y + window.Height);
        (int endX, int endY) = (window.X + width, window.Y + height);
        List<(int X, int Y)> path = MouseCommand.InterpolatePath([(startX, startY), (endX, endY)]);
        InputSimulator sim = new();
        MoveToPixel(sim, path[0]);
        Thread.Sleep(90);
        sim.Mouse.LeftButtonDown();
        Thread.Sleep(90);
        for (int i = 1; i < path.Count; i++) {
            if (AgentRuntime.EmergencyStopped) {
                break;
            }
            MoveToPixel(sim, path[i]);
            int newWidth = Math.Max(1, path[i].X - window.X);
            int newHeight = Math.Max(1, path[i].Y - window.Y);
            AgentNativeMethods.SetWindowPos(hwnd, IntPtr.Zero, window.X, window.Y, newWidth, newHeight, AgentNativeMethods.SWP_NOACTIVATE);
            Thread.Sleep(12);
        }
        Thread.Sleep(90);
        sim.Mouse.LeftButtonUp();
        // #314 review: as in AnimateMove, do not snap the window to the requested size after an
        // emergency-stop abort; leave it at the size the stop caught it.
        if (!AgentRuntime.EmergencyStopped) {
            ApplyResize(hwnd, window, width, height);
        }
    }

    private static void MoveToPixel(InputSimulator sim, (int X, int Y) pixel) {
        (int nx, int ny) = MouseCommand.PixelToVirtualDesktopNormalized(pixel.X, pixel.Y, System.Windows.Forms.SystemInformation.VirtualScreen);
        sim.Mouse.MoveMouseToPositionOnVirtualDesktop(nx, ny);
    }

    private static void ApplyShowWindow(IntPtr hwnd, int nCmdShow) {
        AgentNativeMethods.ShowWindow(hwnd, nCmdShow);
    }

    internal CommandResult ForegroundFailure() => CommandResult.Fail(Cmd,
        "Could not bring the target window to the foreground; Windows refused the activation (foreground lock, " +
        "a modal on another app, or a full-screen exclusive window is holding it).",
        "foreground-not-set", "foreground");
}
