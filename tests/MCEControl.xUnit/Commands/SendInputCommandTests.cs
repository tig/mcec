using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class SendInputCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new SendInputCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
        Assert.False(cmd.Shift);
        Assert.False(cmd.Ctrl);
        Assert.False(cmd.Alt);
        Assert.False(cmd.Win);
    }

    [Fact]
    public void BuiltInCommands_ContainsSendInputCommands()
    {
        var builtIns = SendInputCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);

        // Check for some expected built-in commands
        // Note: These have colons in the Cmd but no "down" or "up" suffix
        Assert.Contains(builtIns, c => c.Cmd == "shiftdown:");
        Assert.Contains(builtIns, c => c.Cmd == "shiftup:");
        Assert.Contains(builtIns, c => c.Cmd == "atlesc");
        Assert.Contains(builtIns, c => c.Cmd == "wintab");
        // Common editing chord builtins
        Assert.Contains(builtIns, c => c.Cmd == "ctrl-a");
        Assert.Contains(builtIns, c => c.Cmd == "ctrl-c");
        Assert.Contains(builtIns, c => c.Cmd == "ctrl-v");
        Assert.Contains(builtIns, c => c.Cmd == "ctrl-z");
        Assert.Contains(builtIns, c => c.Cmd == "ctrl-s");
        // Also includes all VK_ codes
        Assert.Contains(builtIns, c => c.Cmd == "VK_RETURN");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new SendInputCommand
        {
            Cmd = "enter",
            Vk = "VK_RETURN",
            Shift = true,
            Ctrl = false,
            Alt = true,
            Win = false,
            Enabled = true
        };

        var clone = (SendInputCommand)original.Clone(null!);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Vk, clone.Vk);
        Assert.Equal(original.Shift, clone.Shift);
        Assert.Equal(original.Ctrl, clone.Ctrl);
        Assert.Equal(original.Alt, clone.Alt);
        Assert.Equal(original.Win, clone.Win);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new SendInputCommand
        {
            Vk = "VK_RETURN",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new SendInputCommand
        {
            Cmd = "ctrl_c",
            Vk = "c",
            Ctrl = true
        };

        string result = cmd.ToString();
        Assert.Contains("ctrl_c", result);
        Assert.Contains("c", result);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var cmd = new SendInputCommand
        {
            Vk = "VK_ESCAPE",
            Shift = true,
            Ctrl = true,
            Alt = false,
            Win = true
        };

        Assert.Equal("VK_ESCAPE", cmd.Vk);
        Assert.True(cmd.Shift);
        Assert.True(cmd.Ctrl);
        Assert.False(cmd.Alt);
        Assert.True(cmd.Win);
    }

    [Fact]
    public void Clone_WithModifierKeys_PreservesFlags()
    {
        var original = new SendInputCommand
        {
            Vk = "a",
            Shift = true,
            Ctrl = true,
            Alt = true,
            Win = true,
            Enabled = true
        };

        var clone = (SendInputCommand)original.Clone(null!);

        Assert.True(clone.Shift);
        Assert.True(clone.Ctrl);
        Assert.True(clone.Alt);
        Assert.True(clone.Win);
    }
}
