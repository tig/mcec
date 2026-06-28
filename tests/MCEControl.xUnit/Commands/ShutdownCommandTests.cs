using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class ShutdownCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new ShutdownCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsShutdownCommands()
    {
        var builtIns = ShutdownCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);

        // Check for expected built-in shutdown commands
        Assert.Contains(builtIns, c => c.Cmd == "shutdown");
        Assert.Contains(builtIns, c => c.Cmd == "shutdown-hybrid");
        Assert.Contains(builtIns, c => c.Cmd == "restart");
        Assert.Contains(builtIns, c => c.Cmd == "restart-g");
        Assert.Contains(builtIns, c => c.Cmd == "standby");
        Assert.Contains(builtIns, c => c.Cmd == "hibernate");
        // logoff is also a built-in command
        Assert.Contains(builtIns, c => c.Cmd == "logoff");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new ShutdownCommand
        {
            Cmd = "shutdown",
            Args = "force",
            Enabled = true
        };

        var clone = (ShutdownCommand)original.Clone(null);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new ShutdownCommand
        {
            Cmd = "shutdown",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new ShutdownCommand
        {
            Cmd = "shutdown",
            Type = "shutdown",
            TimeOut = 30
        };

        string result = cmd.ToString();
        Assert.Contains("shutdown", result);
        Assert.Contains("30", result); // TimeOut should be in the string
    }
}
