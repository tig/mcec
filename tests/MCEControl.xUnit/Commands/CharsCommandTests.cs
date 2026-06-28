using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class CharsCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new CharsCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsCharsCommand()
    {
        var builtIns = CharsCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "chars:");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new CharsCommand
        {
            Cmd = "chars:",
            Args = "Hello World",
            Enabled = true
        };

        var clone = (CharsCommand)original.Clone(null);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new CharsCommand
        {
            Cmd = "chars:",
            Args = "test",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new CharsCommand
        {
            Cmd = "chars:",
            Args = "test text"
        };

        string result = cmd.ToString();
        Assert.Contains("chars:", result);
        Assert.Contains("test text", result);
    }

    [Fact]
    public void Args_CanBeSet()
    {
        var cmd = new CharsCommand
        {
            Args = "Test String"
        };

        Assert.Equal("Test String", cmd.Args);
    }
}
