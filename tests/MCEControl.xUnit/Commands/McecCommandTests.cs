using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class McecCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new McecCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsMcecCommands()
    {
        var builtIns = McecCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);

        // Check for expected built-in MCE Controller commands
        Assert.Contains(builtIns, c => c.Cmd == "mcec:");
        Assert.Contains(builtIns, c => c.Cmd == "mcec:ver");
        Assert.Contains(builtIns, c => c.Cmd == "mcec:exit");
        Assert.Contains(builtIns, c => c.Cmd == "mcec:cmds");
        Assert.Contains(builtIns, c => c.Cmd == "mcec:time");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new McecCommand
        {
            Cmd = "mcec:reload",
            Enabled = true
        };

        var clone = (McecCommand)original.Clone(null);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new McecCommand
        {
            Cmd = "mcec:reload",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new McecCommand
        {
            Cmd = "mcec:exit"
        };

        string result = cmd.ToString();
        Assert.Contains("mcec:exit", result);
    }
}
