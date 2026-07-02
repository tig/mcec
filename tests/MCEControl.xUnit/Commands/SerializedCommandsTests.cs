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

        // Regression (#200): durationMs/maxWidth/classname were serialized camelCase, so the
        // lower-casing XSLT in LoadCommands silently dropped them on reload.
        var original = new SerializedCommands
        {
            commandArray =
            [
                new RecordCommand() {
                    Cmd = "record", Action = "oneshot", Fps = 7, DurationMs = 3000,
                    MaxWidth = 560, ClassName = "Notepad", Enabled = true,
                }
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
        Assert.Equal(3000, rec.DurationMs);
        Assert.Equal(560, rec.MaxWidth);
        Assert.Equal("Notepad", rec.ClassName);
        Assert.True(rec.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_QueryCommand_TopLevel()
    {
        // Regression (#200): className/maxDepth/maxNodes were serialized camelCase, so the
        // lower-casing XSLT in LoadCommands silently dropped them on reload; a saved query lost its
        // targeting and limits.
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new QueryCommand() {
                    Cmd = "query", Window = "Calc", Process = "calc", ClassName = "ApplicationFrameWindow",
                    MaxDepth = 9, MaxNodes = 250, Enabled = true,
                }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var query = Assert.Single(loaded.commandArray) as QueryCommand;
        Assert.NotNull(query);
        Assert.Equal("query", query.Cmd);
        Assert.Equal("Calc", query.Window);
        Assert.Equal("calc", query.Process);
        Assert.Equal("ApplicationFrameWindow", query.ClassName);
        Assert.Equal(9, query.MaxDepth);
        Assert.Equal(250, query.MaxNodes);
        Assert.True(query.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void SaveLoad_RoundTrip_LaunchCommand_TopLevel()
    {
        // Regression (#200): workingDirectory was serialized camelCase and silently dropped on reload.
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var original = new SerializedCommands
        {
            commandArray =
            [
                new LaunchCommand() {
                    Cmd = "launch", Path = @"C:\Windows\notepad.exe", Arguments = "readme.txt",
                    WorkingDirectory = @"C:\Temp", Timeout = 5000, Enabled = true,
                }
            ]
        };

        SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        var launch = Assert.Single(loaded.commandArray) as LaunchCommand;
        Assert.NotNull(launch);
        Assert.Equal("launch", launch.Cmd);
        Assert.Equal(@"C:\Windows\notepad.exe", launch.Path);
        Assert.Equal("readme.txt", launch.Arguments);
        Assert.Equal(@"C:\Temp", launch.WorkingDirectory);
        Assert.Equal(5000, launch.Timeout);
        Assert.True(launch.Enabled);

        File.Delete(tempFile);
    }

    [Fact]
    public void LoadCommands_LegacyCamelCaseAttributes_StillBind()
    {
        // Old files written before #200 contain camelCase attribute names (className=, maxDepth=,
        // maxNodes=, durationMs=, maxWidth=, workingDirectory=). The lower-casing XSLT normalizes them
        // on load, so they must bind to the now-lowercase serialization names.
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile,
            "<?xml version=\"1.0\"?>\r\n" +
            "<MCEController Version=\"1.0.0.0\">\r\n" +
            "  <Commands xmlns=\"http://www.kindel.com/products/mcecontroller\">\r\n" +
            "    <Query Cmd=\"query\" className=\"Shell_TrayWnd\" maxDepth=\"4\" maxNodes=\"99\" Enabled=\"true\" />\r\n" +
            "    <Record Cmd=\"record\" durationMs=\"2500\" maxWidth=\"320\" Enabled=\"true\" />\r\n" +
            "    <Launch Cmd=\"launch\" path=\"x.exe\" workingDirectory=\"C:\\Temp\" Enabled=\"true\" />\r\n" +
            "  </Commands>\r\n" +
            "</MCEController>\r\n");

        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.commandArray.Length);

        var query = loaded.commandArray[0] as QueryCommand;
        Assert.NotNull(query);
        Assert.Equal("Shell_TrayWnd", query.ClassName);
        Assert.Equal(4, query.MaxDepth);
        Assert.Equal(99, query.MaxNodes);

        var rec = loaded.commandArray[1] as RecordCommand;
        Assert.NotNull(rec);
        Assert.Equal(2500, rec.DurationMs);
        Assert.Equal(320, rec.MaxWidth);

        var launch = loaded.commandArray[2] as LaunchCommand;
        Assert.NotNull(launch);
        Assert.Equal(@"C:\Temp", launch.WorkingDirectory);

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
    public void LoadCommands_NonExistentFile_CreatesDefaultCatalog()
    {
        // A missing file is first-run, not an error: LoadCommands creates the default file (the
        // built-in catalog, all Enabled=false) and loads it. See DefaultCommandsFileTests.
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

        Assert.NotNull(loaded);
        Assert.True(loaded.Count > 0);
        Assert.All(loaded.commandArray, c => Assert.False(c.Enabled));
        Assert.True(File.Exists(tempFile));
        File.Delete(tempFile);
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
