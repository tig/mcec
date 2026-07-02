// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Agent observation command: reports the geometry of every connected display — each monitor's pixel
/// bounds, working area, primary flag, and effective DPI/scale — plus the union virtual-desktop bounds
/// (issue #122). This is the keystone that lets an agent translate the absolute-pixel bounds
/// <c>query</c>/<c>find</c> return into pointer actions: it learns screen size and per-monitor scaling
/// from MCEC rather than doing the host-side <c>SetProcessDPIAware()</c>/<c>GetSystemMetrics()</c> dance
/// the hero script used to. Pure observation — no input is generated. Gated by
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class DisplaysCommand : AgentCommand {
    public static List<Command> BuiltInCommands {
        get => [new DisplaysCommand { Cmd = "displays" }];
    }

    protected override string? AuditDetails() => "displays";

    protected override bool ExecuteCore() {
        JsonArray displays = [];
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++) {
            Screen s = screens[i];
            uint dpi = EffectiveDpi(s.Bounds);
            displays.Add(new JsonObject {
                ["index"] = i,
                ["primary"] = s.Primary,
                ["deviceName"] = s.DeviceName,
                ["bounds"] = RectJson(s.Bounds),
                ["workingArea"] = RectJson(s.WorkingArea),
                ["dpi"] = dpi,
                ["scale"] = Math.Round(dpi / 96.0, 4),
            });
        }

        JsonObject data = new() {
            ["count"] = screens.Length,
            ["virtualBounds"] = RectJson(SystemInformation.VirtualScreen),
            ["displays"] = displays,
        };
        Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
        return true;
    }

    /// <summary>
    /// Effective DPI (X axis) of the monitor containing the centre of <paramref name="bounds"/> via
    /// <c>GetDpiForMonitor</c>. Falls back to 96 (100%) when the shcore API is unavailable (pre-8.1) or
    /// the call fails, so geometry is still returned rather than throwing. X and Y DPI are equal on every
    /// shipping monitor, so only X is reported.
    /// </summary>
    private static uint EffectiveDpi(Rectangle bounds) {
        try {
            Point centre = new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            IntPtr monitor = AgentNativeMethods.MonitorFromPoint(centre, AgentNativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero &&
                AgentNativeMethods.GetDpiForMonitor(monitor, AgentNativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint _) == 0) {
                return dpiX;
            }
        }
        catch (EntryPointNotFoundException) {
            // GetDpiForMonitor is Windows 8.1+; fall through to the 96-DPI default on older hosts.
        }
        catch (DllNotFoundException) {
            // shcore.dll missing; fall through to the 96-DPI default.
        }
        return 96;
    }

    private static JsonObject RectJson(Rectangle r) => new() {
        ["x"] = r.X,
        ["y"] = r.Y,
        ["width"] = r.Width,
        ["height"] = r.Height,
    };
}
