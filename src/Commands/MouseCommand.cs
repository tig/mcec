//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;

namespace MCEControl; 
/// <summary>
/// Simulates mouse movements.
/// </summary>
public class MouseCommand : Command {
    public const string CmdPrefix = "mouse:";

    public static new List<Command> BuiltInCommands {
        get => [
            new MouseCommand{ Cmd = $"{CmdPrefix }" },  // Commands that use form of "cmd:" must define a blank version
            new MouseCommand{ Cmd = $"{CmdPrefix }lbc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }lbdc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }lbd" },
            new MouseCommand{ Cmd = $"{CmdPrefix }lbu" },
            new MouseCommand{ Cmd = $"{CmdPrefix }rbc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }rbdc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }rbd" },
            new MouseCommand{ Cmd = $"{CmdPrefix }rbu" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mbc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mbdc" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mbd" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mbu" },
            new MouseCommand{ Cmd = $"{CmdPrefix }xbc,n" },
            new MouseCommand{ Cmd = $"{CmdPrefix }xbcd,n" },
            new MouseCommand{ Cmd = $"{CmdPrefix }xbd,n" },
            new MouseCommand{ Cmd = $"{CmdPrefix }xbu,n" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mm,x,y" },
            new MouseCommand{ Cmd = $"{CmdPrefix }mt,x,y" },
            new MouseCommand{ Cmd = $"{CmdPrefix }hs,x" },
            new MouseCommand{ Cmd = $"{CmdPrefix }vs,y" },
            new MouseCommand{ Cmd = $"{CmdPrefix }drag,x1,y1,x2,y2" },
        ];
    }

    // Drag gesture tuning. A drag is a press-move-release dispatched atomically inside a single
    // Execute() so no other command interleaves (issue #123 / #113). Successive synthesized move
    // points are kept within DragStepPx of each other so slow drop targets (title bars, sizing
    // borders, sliders) track the held button; the dwells give the target loop time to react.
    internal const int DragStepPx = 12;      // max pixel gap between synthesized move points
    internal const int DragMaxPoints = 400;  // cap synthesized points so a huge path can't spin forever
    private const int DragPressDwellMs = 90;    // dwell after moving to the start and after button-down
    private const int DragMoveDwellMs = 12;     // dwell between successive move points
    private const int DragReleaseDwellMs = 90;  // dwell before button-up so the drop registers

    public MouseCommand() { }

    public override string ToString() {
        return $"Cmd=\"{Cmd}\"";
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new MouseCommand());

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        InputSimulator sim = new InputSimulator();
        // Format is "mouse:<action>[,<parameters>]
        string[] param = Args.Split([','], StringSplitOptions.RemoveEmptyEntries);
        if (param.Length == 0) {
            return true;
        }

        int mb = 0;

        switch (param[0]) {
            case "lbc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Click");
                sim.Mouse.LeftButtonClick();
                break;

            case "lbdc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Double Click");
                sim.Mouse.LeftButtonDoubleClick();
                break;

            case "lbd":
                Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Down");
                sim.Mouse.LeftButtonDown();
                break;

            case "lbu":
                Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Up");
                sim.Mouse.LeftButtonUp();
                break;

            case "rbc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Click");
                sim.Mouse.RightButtonClick();
                break;

            case "rbdc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Double Click");
                sim.Mouse.RightButtonDoubleClick();
                break;

            case "rbd":
                Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Down");
                sim.Mouse.RightButtonDown();
                break;

            case "rbu":
                Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Up");
                sim.Mouse.RightButtonUp();
                break;

            case "mbc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Click");
                sim.Mouse.MiddleButtonClick();
                break;

            case "mbdc":
                Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Double Click");
                sim.Mouse.MiddleButtonDoubleClick();
                break;

            case "mbd":
                Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Down");
                sim.Mouse.MiddleButtonDown();
                break;

            case "mbu":
                Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Up");
                sim.Mouse.MiddleButtonUp();
                break;

            // "mouse:xbc,3" - Mouse X button 3 click
            case "xbc":
                mb = GetIntOrZero(param, 1);
                Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} click");
                sim.Mouse.XButtonClick(mb);
                break;

            case "xbdc":
                mb = GetIntOrZero(param, 1);
                Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} doubleclick");
                sim.Mouse.XButtonDoubleClick(mb);
                break;

            case "xbd":
                mb = GetIntOrZero(param, 1);
                Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} down");
                sim.Mouse.XButtonDown(mb);
                break;

            case "xbu":
                mb = GetIntOrZero(param, 1);
                Logger.Instance.Log4.Info($"{GetType().Name}: Xbutton {mb} up");
                sim.Mouse.XButtonUp(mb);
                break;

            // "mouse:mb,15,20" - Move mouse 15 in X direction, and 20 in Y direction
            case "mm": sim.Mouse.MoveMouseBy(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

            // "mouse:mt,812,562" - Move mouse to (812,562) on the screen
            case "mt": sim.Mouse.MoveMouseTo(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

            // "mouse:mtv,812,562" - Move mouse to (812,562) on the virtual desktop screen
            case "mtv": sim.Mouse.MoveMouseToPositionOnVirtualDesktop(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

            case "hs": sim.Mouse.HorizontalScroll(GetIntOrZero(param, 1)); break;
            case "vs": sim.Mouse.VerticalScroll(GetIntOrZero(param, 1)); break;

            // "mouse:drag,x1,y1,x2,y2[,x3,y3,...]" - Press the left button at the first point, move
            // through every subsequent point with the button held, release at the last. The whole
            // gesture runs here, synchronously, so it can never interleave with another command's
            // mouse input (issue #123; the hazard #113 warns about with hand-rolled lbd/mt/lbu).
            // Coordinates are ABSOLUTE SCREEN PIXELS (unlike mt's 0-65535 units); they are normalized
            // across the virtual desktop internally, so negative/secondary-monitor points work.
            case "drag": ExecuteDrag(param); break;
        }
        return true;
    }

    /// <summary>Handles <c>mouse:drag,x1,y1,x2,y2[,...]</c> — parse the pixel points, then drag through them.</summary>
    private void ExecuteDrag(string[] param) {
        List<(int X, int Y)>? points = ParseCoordinatePairs(param, 1);
        if (points is null) {
            Logger.Instance.Log4.Error($"{GetType().Name}: drag needs at least two x,y points: mouse:drag,x1,y1,x2,y2[,...]");
            return;
        }
        Logger.Instance.Log4.Info($"{GetType().Name}: Drag through {points.Count} point(s) from ({points[0].X},{points[0].Y}) to ({points[^1].X},{points[^1].Y})");
        PerformDrag(points);
    }

    /// <summary>
    /// Parses a flat run of <c>x,y</c> integer pairs from <paramref name="param"/> starting at
    /// <paramref name="startIndex"/>. Returns the points, or <c>null</c> if there are fewer than two
    /// complete pairs, an odd number of values, or a non-integer value. Pure — unit-testable.
    /// </summary>
    public static List<(int X, int Y)>? ParseCoordinatePairs(string[] param, int startIndex) {
        int count = param.Length - startIndex;
        if (count < 4 || (count % 2) != 0) {
            return null;
        }
        List<(int X, int Y)> points = [];
        for (int i = startIndex; i + 1 < param.Length; i += 2) {
            if (!int.TryParse(param[i], out int x) || !int.TryParse(param[i + 1], out int y)) {
                return null;
            }
            points.Add((x, y));
        }
        return points;
    }

    /// <summary>
    /// Densifies <paramref name="waypoints"/> into a move path whose successive points are at most
    /// <see cref="DragStepPx"/> apart (so a drop target tracks the held button), capped at
    /// <see cref="DragMaxPoints"/> points total. Endpoints are preserved exactly; consecutive
    /// duplicate points are dropped. Pure — unit-testable, no input is generated here.
    /// </summary>
    public static List<(int X, int Y)> InterpolatePath(IReadOnlyList<(int X, int Y)> waypoints, int stepPx = DragStepPx, int maxPoints = DragMaxPoints) {
        List<(int X, int Y)> path = [];
        if (waypoints.Count == 0) {
            return path;
        }
        // Choose a step big enough that the whole path stays within maxPoints, but never finer than
        // the caller asked for. This bounds the work for a very long (or multi-waypoint) drag. The
        // budget is maxPoints minus the starting point we always add up front.
        double total = 0;
        for (int i = 1; i < waypoints.Count; i++) {
            total += Distance(waypoints[i - 1], waypoints[i]);
        }
        int budget = Math.Max(1, maxPoints - 1);
        int effStep = Math.Max(Math.Max(1, stepPx), (int)Math.Ceiling(total / budget));

        path.Add(waypoints[0]);
        for (int i = 1; i < waypoints.Count; i++) {
            (int X, int Y) a = waypoints[i - 1];
            (int X, int Y) b = waypoints[i];
            int segSteps = Math.Max(1, (int)Math.Ceiling(Distance(a, b) / effStep));
            for (int s = 1; s <= segSteps; s++) {
                double t = (double)s / segSteps;
                (int X, int Y) p = (
                    (int)Math.Round(a.X + (b.X - a.X) * t),
                    (int)Math.Round(a.Y + (b.Y - a.Y) * t));
                if (path[^1] != p) {
                    path.Add(p);
                }
            }
        }
        return path;
    }

    private static double Distance((int X, int Y) a, (int X, int Y) b) {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Maps an absolute screen pixel to the 0-65535 coordinate space
    /// <see cref="IMouseSimulator.MoveMouseToPositionOnVirtualDesktop"/> expects, relative to
    /// <paramref name="virtualScreen"/> (<see cref="SystemInformation.VirtualScreen"/> at runtime).
    /// Clamped to the valid range. Pure — unit-testable with a synthetic screen rectangle.
    /// </summary>
    public static (int X, int Y) PixelToVirtualDesktopNormalized(int px, int py, Rectangle virtualScreen) {
        int w = Math.Max(1, virtualScreen.Width - 1);
        int h = Math.Max(1, virtualScreen.Height - 1);
        double nx = (px - virtualScreen.X) * 65535.0 / w;
        double ny = (py - virtualScreen.Y) * 65535.0 / h;
        return (
            (int)Math.Round(Math.Clamp(nx, 0, 65535)),
            (int)Math.Round(Math.Clamp(ny, 0, 65535)));
    }

    /// <summary>
    /// Performs a left-button drag through <paramref name="pixelWaypoints"/> (absolute screen pixels):
    /// move to the first point, button-down, move through the interpolated path, button-up — all on
    /// this thread so the gesture is atomic. Shared by <c>mouse:drag</c> and the agent
    /// <see cref="DragCommand"/> so both dispatch an identical, un-interleavable gesture.
    /// </summary>
    public static void PerformDrag(IReadOnlyList<(int X, int Y)> pixelWaypoints) {
        if (pixelWaypoints is null || pixelWaypoints.Count < 2) {
            return;
        }
        Rectangle vs = SystemInformation.VirtualScreen;
        List<(int X, int Y)> path = InterpolatePath(pixelWaypoints);
        InputSimulator sim = new InputSimulator();

        MoveToPixel(sim, path[0], vs);
        Thread.Sleep(DragPressDwellMs);
        sim.Mouse.LeftButtonDown();
        Thread.Sleep(DragPressDwellMs);
        for (int i = 1; i < path.Count; i++) {
            MoveToPixel(sim, path[i], vs);
            Thread.Sleep(DragMoveDwellMs);
        }
        Thread.Sleep(DragReleaseDwellMs);
        sim.Mouse.LeftButtonUp();
    }

    private static void MoveToPixel(InputSimulator sim, (int X, int Y) pixel, Rectangle virtualScreen) {
        (int nx, int ny) = PixelToVirtualDesktopNormalized(pixel.X, pixel.Y, virtualScreen);
        sim.Mouse.MoveMouseToPositionOnVirtualDesktop(nx, ny);
    }

    private static int GetIntOrZero(String[] s, int index) {
        int val = 0;
        if (index < s.Length) {
            if (!int.TryParse(s[index], out val)) {
                return 0;
            }
        }
        return val;
    }
}
