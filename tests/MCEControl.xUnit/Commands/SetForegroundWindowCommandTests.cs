using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class SetForegroundWindowCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new SetForegroundWindowCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsSetForegroundWindowCommands()
    {
        var builtIns = SetForegroundWindowCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
        // Actual built-in commands use specific app names, not generic "setforegroundwindow:"
        Assert.Contains(builtIns, c => c.Cmd == "activatecode");
        Assert.Contains(builtIns, c => c.Cmd == "activatenotepad");
        Assert.Contains(builtIns, c => c.Cmd == "activatemcec");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new SetForegroundWindowCommand
        {
            Cmd = "setforegroundwindow:",
            Args = "notepad",
            Enabled = true
        };

        var clone = (SetForegroundWindowCommand)original.Clone(null!);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new SetForegroundWindowCommand
        {
            Cmd = "setforegroundwindow:",
            Args = "notepad",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new SetForegroundWindowCommand
        {
            Cmd = "activate",
            AppName = "Calculator"
        };

        string result = cmd.ToString();
        Assert.Contains("activate", result);
        Assert.Contains("Calculator", result);  // ToString uses AppName not Args
    }
}
