// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.IO;
using System.Text.Json.Nodes;
using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Commands;

public class WindowCommandTests {
    [Fact]
    public void ValidateArguments_RequiresCoordinatesForMove() {
        Assert.Null(WindowCommand.ValidateArguments("move", hasPosition: true, hasSize: false));
        Assert.Equal("move requires x and y.", WindowCommand.ValidateArguments("move", hasPosition: false, hasSize: false));
    }

    [Fact]
    public void ValidateArguments_RequiresSizeForResize() {
        Assert.Null(WindowCommand.ValidateArguments("resize", hasPosition: false, hasSize: true));
        Assert.Equal("resize requires width and height.", WindowCommand.ValidateArguments("resize", hasPosition: false, hasSize: false));
    }

    [Fact]
    public void ValidateArguments_AllowsOtherActionsWithoutExtraArgs() {
        Assert.Null(WindowCommand.ValidateArguments("minimize", hasPosition: false, hasSize: false));
        Assert.Null(WindowCommand.ValidateArguments("maximize", hasPosition: false, hasSize: false));
        Assert.Null(WindowCommand.ValidateArguments("restore", hasPosition: false, hasSize: false));
        Assert.Null(WindowCommand.ValidateArguments("foreground", hasPosition: false, hasSize: false));
    }

    // ---- MCP argument mapping (#314 review): nullable JSON args collapse to non-nullable coordinates
    // plus PositionSpecified/SizeSpecified flags; a pair counts only when BOTH members are present. ----

    [Fact]
    public void BuildCommand_MapsBothCoordinates_SetsPositionSpecified() {
        WindowCommand cmd = Assert.IsType<WindowCommand>(
            AgentServer.BuildCommand("window", new JsonObject { ["action"] = "move", ["x"] = 100, ["y"] = 200 }));
        Assert.Equal(100, cmd.X);
        Assert.Equal(200, cmd.Y);
        Assert.True(cmd.PositionSpecified);
        Assert.False(cmd.SizeSpecified);
    }

    [Fact]
    public void BuildCommand_PartialCoordinates_LeavesPositionUnspecified() {
        // Only x given: not a valid move target, so PositionSpecified stays false and ValidateArguments rejects it.
        WindowCommand cmd = Assert.IsType<WindowCommand>(
            AgentServer.BuildCommand("window", new JsonObject { ["action"] = "move", ["x"] = 100 }));
        Assert.False(cmd.PositionSpecified);
        Assert.Equal("move requires x and y.",
            WindowCommand.ValidateArguments("move", cmd.PositionSpecified, cmd.SizeSpecified));
    }

    [Fact]
    public void BuildCommand_MapsBothDimensions_SetsSizeSpecified() {
        WindowCommand cmd = Assert.IsType<WindowCommand>(
            AgentServer.BuildCommand("window", new JsonObject { ["action"] = "resize", ["width"] = 640, ["height"] = 480, ["animate"] = true }));
        Assert.Equal(640, cmd.Width);
        Assert.Equal(480, cmd.Height);
        Assert.True(cmd.SizeSpecified);
        Assert.False(cmd.PositionSpecified);
        Assert.True(cmd.Animate);
    }

    // ---- #314 review P1: the window tool must be XML-serializable. Nullable<int> XmlAttributes threw a
    // TypeInitializationException in SerializedCommands' static serializer, breaking ALL mcec.commands
    // load/save. Round-trip a fully-populated WindowCommand to lock that shut. ----

    [Fact]
    public void SaveLoad_RoundTrip_WindowCommand_PreservesFields() {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        SerializedCommands original = new() {
            commandArray = [
                new WindowCommand {
                    Cmd = "window", Action = "move", X = 12, Y = 34, Width = 800, Height = 600,
                    Animate = true, PositionSpecified = true, SizeSpecified = true, Enabled = true,
                },
            ],
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        SerializedCommands loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded.commandArray);
        WindowCommand? win = Assert.Single(loaded.commandArray) as WindowCommand;
        Assert.NotNull(win);
        Assert.Equal("window", win.Cmd);
        Assert.Equal("move", win.Action);
        Assert.Equal(12, win.X);
        Assert.Equal(34, win.Y);
        Assert.Equal(800, win.Width);
        Assert.Equal(600, win.Height);
        Assert.True(win.Animate);
        Assert.True(win.PositionSpecified);
        Assert.True(win.SizeSpecified);
        Assert.True(win.Enabled);

        File.Delete(tempFile);
    }

    // ---- #314 review P2: animate:true synthesizes a held-button mouse drag, so the tool must serialize
    // on the shared input gate (#113) like drag/focus. ----

    [Fact]
    public void WindowTool_SerializesOnInput() {
        Assert.True(ToolCatalog.TryGet("window", out ToolDescriptor descriptor));
        Assert.True(descriptor.SerializesOnInput);
    }
}
