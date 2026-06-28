using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class SendMessageCommandTests
{
    [Fact]
    public void Constructor_WithParameters_SetsProperties()
    {
        var cmd = new SendMessageCommand("WindowClass", "WindowTitle", 123, 456, 789);

        Assert.Equal("WindowClass", cmd.ClassName);
        Assert.Equal("WindowTitle", cmd.WindowName);
        Assert.Equal(123, cmd.Msg);
        Assert.Equal(456, cmd.WParam);
        Assert.Equal(789, cmd.LParam);
    }

    [Fact]
    public void Constructor_Default_InitializesProperties()
    {
        var cmd = new SendMessageCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsSendMessageCommands()
    {
        var builtIns = SendMessageCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new SendMessageCommand("Class1", "Window1", 100, 200, 300)
        {
            Cmd = "sendmsg",
            Enabled = true
        };

        var clone = (SendMessageCommand)original.Clone(null);

        Assert.Equal(original.ClassName, clone.ClassName);
        Assert.Equal(original.WindowName, clone.WindowName);
        Assert.Equal(original.Msg, clone.Msg);
        Assert.Equal(original.WParam, clone.WParam);
        Assert.Equal(original.LParam, clone.LParam);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new SendMessageCommand("Class", "Window", 0, 0, 0)
        {
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new SendMessageCommand("TestClass", "TestWindow", 100, 200, 300)
        {
            Cmd = "test"
        };

        string result = cmd.ToString();
        Assert.Contains("test", result);
        Assert.Contains("TestClass", result);
        Assert.Contains("TestWindow", result);
        Assert.Contains("100", result);
        Assert.Contains("200", result);
        Assert.Contains("300", result);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var cmd = new SendMessageCommand
        {
            ClassName = "NewClass",
            WindowName = "NewWindow",
            Msg = 555,
            WParam = 666,
            LParam = 777
        };

        Assert.Equal("NewClass", cmd.ClassName);
        Assert.Equal("NewWindow", cmd.WindowName);
        Assert.Equal(555, cmd.Msg);
        Assert.Equal(666, cmd.WParam);
        Assert.Equal(777, cmd.LParam);
    }
}
