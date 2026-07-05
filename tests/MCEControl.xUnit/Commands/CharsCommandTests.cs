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

        var clone = (CharsCommand)original.Clone(null!);

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

    // #269: chars: is LITERAL text entry. It must NOT run Regex.Unescape (which silently mangled Windows
    // paths: `\t`, `\U`, etc. in `C:\Users\tig\...` were eaten or turned into control chars). PrepareText
    // is the pure seam Execute types verbatim; these pin that it never processes escape sequences.

    [Fact]
    public void PrepareText_WindowsPath_TypedVerbatim_NoBackslashMangling()
    {
        // The canonical footgun: `C:\Users\tig\file.txt` — the `\t`/`\f` must stay literal, not become a TAB.
        string path = @"C:\Users\tig\file.txt";

        string result = CharsCommand.PrepareText(path);

        Assert.Equal(path, result);
        Assert.DoesNotContain('\t', result); // \t was NOT turned into a TAB
    }

    [Fact]
    public void PrepareText_BackslashEscapes_StayLiteral()
    {
        // A bare `\t`/`\n` sequence types the two characters backslash+t, not a TAB/newline.
        Assert.Equal(@"a\tb", CharsCommand.PrepareText(@"a\tb"));
        Assert.Equal(@"one\ntwo", CharsCommand.PrepareText(@"one\ntwo"));
        Assert.DoesNotContain('\t', CharsCommand.PrepareText(@"a\tb"));
        Assert.DoesNotContain('\n', CharsCommand.PrepareText(@"one\ntwo"));
    }

    [Fact]
    public void PrepareText_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", CharsCommand.PrepareText(null));
        Assert.Equal("", CharsCommand.PrepareText(""));
    }
}
