using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class MouseCommandTests
{
    [Fact]
    public void BuiltInCommands_ContainsDrag()
    {
        Assert.Contains(MouseCommand.BuiltInCommands, c => c.Cmd == "mouse:drag,x1,y1,x2,y2");
    }

    [Fact]
    public void BuiltInCommands_ContainsPixelMove()
    {
        // mtp is the absolute-screen-pixel move added for the agent's observe->act loop (#122).
        Assert.Contains(MouseCommand.BuiltInCommands, c => c.Cmd == "mouse:mtp,x,y");
    }

    [Theory]
    [InlineData("left", "left")]
    [InlineData("LEFT", "left")]
    [InlineData("l", "left")]
    [InlineData("right", "right")]
    [InlineData("R", "right")]
    [InlineData("middle", "middle")]
    [InlineData("m", "middle")]
    [InlineData("", "left")]      // empty falls back to left
    [InlineData("bogus", "left")] // unknown falls back to left
    public void NormalizeButton_MapsAliasesAndDefaults(string input, string expected)
    {
        Assert.Equal(expected, MouseCommand.NormalizeButton(input));
    }

    [Theory]
    [InlineData(new[] { "drag", "0", "0", "100", "50" }, 2)]
    [InlineData(new[] { "drag", "-5", "-10", "5", "10", "20", "30" }, 3)]
    public void ParseCoordinatePairs_ValidRuns_ReturnPoints(string[] param, int expectedCount)
    {
        List<(int X, int Y)>? points = MouseCommand.ParseCoordinatePairs(param, 1);

        Assert.NotNull(points);
        Assert.Equal(expectedCount, points!.Count);
    }

    [Fact]
    public void ParseCoordinatePairs_KeepsNegativeCoordinates()
    {
        List<(int X, int Y)>? points = MouseCommand.ParseCoordinatePairs(["drag", "-5", "-10", "5", "10"], 1);

        Assert.Equal((-5, -10), points![0]);
        Assert.Equal((5, 10), points[1]);
    }

    [Fact]
    public void ParseCoordinatePairs_Invalid_ReturnsNull()
    {
        Assert.Null(MouseCommand.ParseCoordinatePairs(["drag", "0", "0"], 1));           // only one point
        Assert.Null(MouseCommand.ParseCoordinatePairs(["drag", "0", "0", "1"], 1));      // odd number of values
        Assert.Null(MouseCommand.ParseCoordinatePairs(["drag", "a", "0", "1", "2"], 1)); // non-integer
    }

    [Fact]
    public void InterpolatePath_PreservesEndpoints_AndKeepsStepsSmall()
    {
        List<(int X, int Y)> path = MouseCommand.InterpolatePath([(0, 0), (100, 0)], stepPx: 12);

        Assert.Equal((0, 0), path[0]);
        Assert.Equal((100, 0), path[^1]);
        Assert.True(path.Count > 2, "a 100px segment should be densified");
        for (int i = 1; i < path.Count; i++)
        {
            int dx = Math.Abs(path[i].X - path[i - 1].X);
            int dy = Math.Abs(path[i].Y - path[i - 1].Y);
            Assert.True(dx <= 12 && dy <= 12, $"gap {dx},{dy} exceeds step");
        }
    }

    [Fact]
    public void InterpolatePath_WalksAllWaypointsInOrder()
    {
        (int X, int Y)[] waypoints = [(0, 0), (50, 0), (50, 50)];
        List<(int X, int Y)> path = MouseCommand.InterpolatePath(waypoints, stepPx: 10);

        Assert.Equal((0, 0), path[0]);
        Assert.Contains((50, 0), path);   // the corner waypoint is on the path
        Assert.Equal((50, 50), path[^1]);
    }

    [Fact]
    public void InterpolatePath_RespectsMaxPointsCap()
    {
        List<(int X, int Y)> path = MouseCommand.InterpolatePath([(0, 0), (100000, 0)], stepPx: 1, maxPoints: 50);

        // Without the cap a 100000px segment at 1px steps would be ~100000 points; the cap holds it near 50.
        Assert.True(path.Count <= 50, $"expected <= 50 points, got {path.Count}");
        Assert.Equal((100000, 0), path[^1]);
    }

    [Fact]
    public void InterpolatePath_CapsPointsEvenWithManyTinyWaypoints()
    {
        // Regression: a caller passing more waypoints than maxPoints (each 1px apart) must still be
        // capped; otherwise the drag holds ExecLock and keeps the button down for path.Count*dwell.
        List<(int X, int Y)> waypoints = [];
        for (int i = 0; i <= 500; i++)
        {
            waypoints.Add((i, 0));
        }

        List<(int X, int Y)> path = MouseCommand.InterpolatePath(waypoints, stepPx: 1, maxPoints: 400);

        Assert.True(path.Count <= 400, $"expected <= 400 points, got {path.Count}");
        // The destination is still honored so the drag releases at the intended endpoint.
        Assert.Equal((500, 0), path[^1]);
    }

    [Fact]
    public void PixelToVirtualDesktopNormalized_MapsCornersAndClamps()
    {
        // A screen whose width-1/height-1 is exactly 65535 makes pixels map 1:1 to normalized units.
        Rectangle screen = new(0, 0, 65536, 65536);

        Assert.Equal((100, 200), MouseCommand.PixelToVirtualDesktopNormalized(100, 200, screen));
        Assert.Equal((0, 0), MouseCommand.PixelToVirtualDesktopNormalized(0, 0, screen));
        Assert.Equal((65535, 65535), MouseCommand.PixelToVirtualDesktopNormalized(65535, 65535, screen));
        // Out-of-bounds pixels clamp into range rather than overflow.
        Assert.Equal((0, 0), MouseCommand.PixelToVirtualDesktopNormalized(-500, -500, screen));
        Assert.Equal((65535, 65535), MouseCommand.PixelToVirtualDesktopNormalized(999999, 999999, screen));
    }

    [Fact]
    public void PixelToVirtualDesktopNormalized_HandlesNegativeOriginMonitor()
    {
        // Secondary monitor to the left of primary: virtual-screen origin is negative. Its left edge
        // must normalize to 0 (not underflow), proving drag coords work across a multi-monitor desktop.
        Rectangle screen = new(-1920, 0, 1920 + 65536, 65536);

        Assert.Equal((0, 0), MouseCommand.PixelToVirtualDesktopNormalized(-1920, 0, screen));
    }

    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new MouseCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsMouseCommands()
    {
        var builtIns = MouseCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "mouse:");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new MouseCommand
        {
            Cmd = "mouse:",
            Args = "left",
            Enabled = true
        };

        var clone = (MouseCommand)original.Clone(null!);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new MouseCommand
        {
            Cmd = "mouse:",
            Args = "left",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new MouseCommand
        {
            Cmd = "mouse:"
        };

        string result = cmd.ToString();
        Assert.Contains("mouse:", result);
        // ToString uses base Command.ToString() which includes Cmd and Args
        // Args is empty by default, so just verify Cmd is present
    }
}
