using System;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

public class SerializedCommandsTests
{
    [Fact]
    public void SaveCommands_CreatesFile()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var commands = new SerializedCommands
        {
            commandArray =
            [
                new PauseCommand() { Cmd = "pause100", Args = "100", Enabled = true }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, commands, "1.0.0.0");

        Assert.True(File.Exists(tempFile));
        File.Delete(tempFile);
    }

    [Fact]
    public void LoadCommands_ReadsFile()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var commands = new SerializedCommands
        {
            commandArray =
            [
                new PauseCommand() { Cmd = "pause100", Args = "100", Enabled = true },
                new CharsCommand() { Cmd = "hello", Args = "Hello World", Enabled = false }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, commands, "1.0.0.0");

        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.commandArray.Length);
        Assert.Equal("pause100", loaded.commandArray[0].Cmd);
        Assert.Equal("hello", loaded.commandArray[1].Cmd);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_RecordCommand_TopLevel()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new RecordCommand() { Cmd = "record", Action = "oneshot", Fps = 7, DurationMs = 3000, Enabled = true }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var rec = Assert.Single(loaded.commandArray) as RecordCommand;
        Assert.NotNull(rec);
        Assert.Equal("record", rec.Cmd);
        Assert.Equal("oneshot", rec.Action);
        Assert.Equal(7, rec.Fps);
        Assert.True(rec.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_RecordCommand_Embedded()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new StartProcessCommand() {
                    Cmd = "proc", File = "x.exe", Enabled = true,
                    EmbeddedCommands = [ new RecordCommand() { Cmd = "record", Action = "start", Enabled = true } ],
                }
            ]
        };

        // Must serialize AND deserialize the nested <record> as a RecordCommand (the EmbeddedCommands
        // polymorphic map must know the type, same as the top-level commandArray map).
        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var proc = Assert.Single(loaded.commandArray) as StartProcessCommand;
        Assert.NotNull(proc);
        Assert.NotNull(proc.EmbeddedCommands);
        var embedded = Assert.Single(proc.EmbeddedCommands) as RecordCommand;
        Assert.NotNull(embedded);
        Assert.Equal("start", embedded.Action);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_DragCommand_TopLevel()
    {
        // Regression: DragCommand is a built-in, so a real command table contains one and Save() must be
        // able to serialize it. That only works if the type is in the XmlArrayItem/XmlElement known-type
        // lists (like every other agent command). Without the registration this throws at Save.
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new DragCommand() { Cmd = "drag", FromValue = "Volume", ToX = 300, ToY = 120, PathSpec = "10,10;20,20", Enabled = true }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var drag = Assert.Single(loaded.commandArray) as DragCommand;
        Assert.NotNull(drag);
        Assert.Equal("drag", drag.Cmd);
        Assert.Equal("Volume", drag.FromValue);
        Assert.Equal(300, drag.ToX);
        Assert.Equal("10,10;20,20", drag.PathSpec);
        Assert.True(drag.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_ClickCommand_TopLevel()
    {
        // ClickCommand is a built-in, so a real command table contains one and Save() must serialize it;
        // which only works if the type is in the XmlArrayItem/XmlElement known-type lists (#122).
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new ClickCommand() { Cmd = "click", Value = "OK", X = 42, Y = 84, Button = "right", Count = 2, Enabled = true }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var click = Assert.Single(loaded.commandArray) as ClickCommand;
        Assert.NotNull(click);
        Assert.Equal("click", click.Cmd);
        Assert.Equal("OK", click.Value);
        Assert.Equal(42, click.X);
        Assert.Equal(84, click.Y);
        Assert.Equal("right", click.Button);
        Assert.Equal(2, click.Count);
        Assert.True(click.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_DisplaysCommand_TopLevel()
    {
        // DisplaysCommand is a built-in; Save() must serialize it via the known-type lists (#122).
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new DisplaysCommand() { Cmd = "displays", Enabled = true }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var displays = Assert.Single(loaded.commandArray) as DisplaysCommand;
        Assert.NotNull(displays);
        Assert.Equal("displays", displays.Cmd);
        Assert.True(displays.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void LoadCommands_NonExistentFile_ReturnsNull()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.Null(loaded);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesData()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new StartProcessCommand() { Cmd = "notepad", File = "notepad.exe", Enabled = true },
                new SendInputCommand() { Cmd = "enter", Vk = "VK_RETURN", Enabled = true },
                new MouseCommand() { Cmd = "click", Args = "left", Enabled = false }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.commandArray.Length);

        var notepadCmd = loaded.commandArray[0] as StartProcessCommand;
        Assert.NotNull(notepadCmd);
        Assert.Equal("notepad", notepadCmd.Cmd);
        Assert.Equal("notepad.exe", notepadCmd.File);
        Assert.True(notepadCmd.Enabled);

        var enterCmd = loaded.commandArray[1] as SendInputCommand;
        Assert.NotNull(enterCmd);
        Assert.Equal("VK_RETURN", enterCmd.Vk);

        var mouseCmd = loaded.commandArray[2] as MouseCommand;
        Assert.NotNull(mouseCmd);
        Assert.False(mouseCmd.Enabled);

        File.Delete(tempFile);
    }
}
