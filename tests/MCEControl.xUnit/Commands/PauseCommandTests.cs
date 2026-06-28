using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class PauseCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new PauseCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled); // Commands disabled by default
    }

    [Fact]
    public void BuiltInCommands_ContainsPauseCommand()
    {
        var builtIns = PauseCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new PauseCommand
        {
            Cmd = "pause",
            Args = "1000",
            Enabled = true
        };

        var clone = (PauseCommand)original.Clone(null);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_DoesNotThrow()
    {
        var cmd = new PauseCommand
        {
            Cmd = "pause",
            Args = "100",
            Enabled = false
        };

        // Disabled commands return false without executing
        // This doesn't throw, just returns false
        var exception = Record.Exception(() => cmd.Execute());
        Assert.Null(exception);
    }

    [Fact]
    public void Args_CanBeSet()
    {
        var cmd = new PauseCommand
        {
            Args = "500"
        };

        Assert.Equal("500", cmd.Args);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new PauseCommand
        {
            Cmd = "pause",
            Args = "100"
        };

        string result = cmd.ToString();
        Assert.Contains("pause", result);
        Assert.Contains("100", result);
    }

    private class TestReply : Reply
    {
        public override void Write(string text)
        {
            // No-op for testing
        }
    }
}
