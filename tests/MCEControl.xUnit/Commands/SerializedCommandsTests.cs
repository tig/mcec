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
            commandArray = new Command[]
            {
                new PauseCommand() { Cmd = "pause100", Args = "100", Enabled = true }
            }
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
            commandArray = new Command[]
            {
                new PauseCommand() { Cmd = "pause100", Args = "100", Enabled = true },
                new CharsCommand() { Cmd = "hello", Args = "Hello World", Enabled = false }
            }
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
            commandArray = new Command[]
            {
                new StartProcessCommand() { Cmd = "notepad", File = "notepad.exe", Enabled = true },
                new SendInputCommand() { Cmd = "enter", Vk = "VK_RETURN", Enabled = true },
                new MouseCommand() { Cmd = "click", Args = "left", Enabled = false }
            }
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
