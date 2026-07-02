using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class CommandTests
{
    [Fact]
    public void Constructor_DisabledByDefault()
    {
        // We override Enabled in TestCommand, so test with a real command
        var pauseCmd = new PauseCommand();
        Assert.False(pauseCmd.Enabled);
    }

    [Fact]
    public void Constructor_UserDefinedIsFalse()
    {
        var cmd = new TestCommand();
        Assert.False(cmd.UserDefined);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new TestCommand { Enabled = false };
        bool result = cmd.Execute();
        Assert.False(result);
        Assert.False(cmd.ExecuteCalled);
    }

    [Fact]
    public void Execute_WhenEnabled_CallsExecuteLogic()
    {
        var cmd = new TestCommand { Enabled = true };
        bool result = cmd.Execute();
        Assert.True(result);
        Assert.True(cmd.ExecuteCalled);
    }

    [Fact]
    public void Clone_CopiesProperties()
    {
        var original = new TestCommand
        {
            Cmd = "test",
            Args = "testargs",
            Enabled = true,
            UserDefined = true
        };

        var clone = (TestCommand)original.Clone(null!);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.Args, clone.Args);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.Equal(original.UserDefined, clone.UserDefined);
    }

    [Fact]
    public void Clone_WithReply_SetsReplyContext()
    {
        var original = new TestCommand();
        var mockReply = new TestReply();

        var clone = (TestCommand)original.Clone(mockReply);

        Assert.Same(mockReply, clone.Reply);
    }

    [Fact]
    public void Clone_WithEmbeddedCommands_ClonesEmbedded()
    {
        var original = new TestCommand
        {
            Cmd = "parent",
            EmbeddedCommands =
            [
                new PauseCommand { Cmd = "pause", Args = "100", Enabled = true },
                new TestCommand { Cmd = "child", Enabled = true }
            ]
        };

        var clone = (TestCommand)original.Clone(null!);

        Assert.NotNull(clone.EmbeddedCommands);
        Assert.Equal(2, clone.EmbeddedCommands.Count);
        Assert.Equal("pause", clone.EmbeddedCommands[0].Cmd);
        Assert.Equal("child", clone.EmbeddedCommands[1].Cmd);
    }

    [Fact]
    public void Clone_EmbeddedChildren_KeepTheirOwnEnabled()
    {
        // #183: the old base Clone assigned clone.Enabled (the parent) inside the embedded-clone
        // loop where it meant the child's. Pin that each embedded clone carries its OWN source
        // child's Enabled; with children on BOTH sides of the parent's value; and that the
        // parent's Enabled survives the loop.
        var original = new TestCommand
        {
            Cmd = "parent",
            Enabled = false,
            EmbeddedCommands =
            [
                new PauseCommand { Cmd = "on", Enabled = true },
                new PauseCommand { Cmd = "off", Enabled = false }
            ]
        };

        var clone = (TestCommand)original.Clone(null!);

        Assert.False(clone.Enabled);
        Assert.NotNull(clone.EmbeddedCommands);
        Assert.True(clone.EmbeddedCommands[0].Enabled);
        Assert.False(clone.EmbeddedCommands[1].Enabled);
    }

    [Fact]
    public void Clone_EmbeddedCommands_AreDeepCopies()
    {
        // MemberwiseClone alone would share the EmbeddedCommands list/children; the base Clone
        // must deep-clone them so mutating a clone's child never leaks into the prototype.
        var original = new TestCommand
        {
            Cmd = "parent",
            EmbeddedCommands = [new PauseCommand { Cmd = "child", Args = "100", Enabled = true }]
        };

        var clone = (TestCommand)original.Clone(null!);

        Assert.NotNull(clone.EmbeddedCommands);
        Assert.NotSame(original.EmbeddedCommands, clone.EmbeddedCommands);
        Assert.NotSame(original.EmbeddedCommands[0], clone.EmbeddedCommands[0]);
        clone.EmbeddedCommands[0].Args = "changed";
        Assert.Equal("100", original.EmbeddedCommands[0].Args);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new TestCommand
        {
            Cmd = "testcmd",
            Args = "testargs"
        };

        string result = cmd.ToString();

        Assert.Contains("testcmd", result);
        Assert.Contains("testargs", result);
    }

    // NOTE (#204): GetDerivedClassesCollection_ReturnsAllCommandTypes used to live here. The
    // reflection sweep it tested is gone; command types are declared in CommandRegistry.Entries,
    // and CommandRegistryTests asserts the registry covers every concrete Command subclass.
}
