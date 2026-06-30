using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class StartProcessCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        var cmd = new StartProcessCommand();
        Assert.NotNull(cmd);
        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_ContainsExpectedCommands()
    {
        var builtIns = StartProcessCommand.BuiltInCommands;
        Assert.NotEmpty(builtIns);

        // Check for some expected built-in commands
        Assert.Contains(builtIns, c => c.Cmd == "code");
        Assert.Contains(builtIns, c => c.Cmd == "tada");
        Assert.Contains(builtIns, c => c.Cmd == "type_into_notepad");
        Assert.Contains(builtIns, c => c.Cmd == "netflix");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new StartProcessCommand
        {
            Cmd = "notepad",
            File = "notepad.exe",
            Arguments = "test.txt",
            Verb = "open",
            Enabled = true
        };

        var clone = (StartProcessCommand)original.Clone(null!);

        Assert.Equal(original.Cmd, clone.Cmd);
        Assert.Equal(original.File, clone.File);
        Assert.Equal(original.Arguments, clone.Arguments);
        Assert.Equal(original.Verb, clone.Verb);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse()
    {
        var cmd = new StartProcessCommand
        {
            Cmd = "test",
            File = "notepad.exe",
            Enabled = false
        };

        bool result = cmd.Execute();
        Assert.False(result);
    }

    [Fact]
    public void Execute_WithNullReply_Throws()
    {
        // Reaching the null-Reply guard requires getting past base.Execute(), whose telemetry metric
        // needs an initialized telemetry client. Initialize it so this test is deterministic regardless
        // of whether other tests in the run have already done so.
        AgentTestSupport.EnsureTelemetry();
        var cmd = new StartProcessCommand
        {
            Cmd = "test",
            File = "notepad.exe",
            Enabled = true,
            Reply = null!
        };

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var cmd = new StartProcessCommand
        {
            Cmd = "notepad",
            File = "notepad.exe",
            Arguments = "test.txt",
            Verb = "open"
        };

        string result = cmd.ToString();
        Assert.Contains("notepad", result);
        Assert.Contains("notepad.exe", result);
        Assert.Contains("test.txt", result);
        Assert.Contains("open", result);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var cmd = new StartProcessCommand
        {
            File = "cmd.exe",
            Arguments = "/c dir",
            Verb = "runas"
        };

        Assert.Equal("cmd.exe", cmd.File);
        Assert.Equal("/c dir", cmd.Arguments);
        Assert.Equal("runas", cmd.Verb);
    }

    [Fact]
    public void Clone_WithEmbeddedCommands_ClonesNested()
    {
        var original = new StartProcessCommand
        {
            Cmd = "notepad",
            File = "notepad.exe",
            Enabled = true,
            EmbeddedCommands =
            [
                new PauseCommand { Cmd = "pause", Args = "100", Enabled = true },
                new CharsCommand { Cmd = "chars:", Args = "Hello", Enabled = true }
            ]
        };

        var clone = (StartProcessCommand)original.Clone(null!);

        Assert.NotNull(clone.EmbeddedCommands);
        Assert.Equal(2, clone.EmbeddedCommands.Count);
        Assert.IsType<PauseCommand>(clone.EmbeddedCommands[0]);
        Assert.IsType<CharsCommand>(clone.EmbeddedCommands[1]);
    }
}
