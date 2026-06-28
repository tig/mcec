using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.IO;

namespace MCEControl.xUnit;

public class AppSettingsTests
{
    [Fact]
    public void GetSettingsPath_inProgramFiles_Test()
    {
        // If we're running within Program Files, use %AppData% 
        string startupPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Kindel Systems\MCE Controller";
        // should be "%appdata%\Kindel Systems\MCE Controller"
        string settingsPath = $@"{Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1))}";
        Assert.True(AppSettings.GetSettingsPath(startupPath).CompareTo(settingsPath) == 0);
    }

    [Fact]
    public void GetSettingsPath_standalone_Test()
    {
        // If we're running elsewhere (not in Program Files), use current dir
        string startupPath = $@".";
        // should be ame as Application.Startpath
        string settingsPath = $@".";
        Assert.True(AppSettings.GetSettingsPath(startupPath).CompareTo(settingsPath) == 0);
    }

    /// <summary>
    /// Tests that SafeForTelemetryAttribute is working
    /// </summary>
    [Fact]
    public void GetTelemetryDictionary_Test()
    {
        AppSettings appSettings = new AppSettings();
        IDictionary<string, string>? dict = appSettings.GetTelemetryDictionary();

        Assert.True(dict.ContainsKey("AutoStart"));

        Assert.False(dict.ContainsKey("WakeupCommand"));
    }

    [Fact]
    public void DeserializeExists_Test()
    {
        string tempPath = Path.GetTempPath();
        string? settingsPath = AppSettings.GetSettingsPath(tempPath);

        AppSettings settings = new AppSettings();
        settings.AutoStart = true; // default is false, so this is a good test

        string settingsFullPath = $@"{tempPath}test.settings.xml";
        settings.Serialize(settingsFullPath);

        string text = System.IO.File.ReadAllText(settingsFullPath);

        Assert.False(string.IsNullOrEmpty(text));

        AppSettings? readSettings = AppSettings.Deserialize(settingsFullPath);

        Assert.True(readSettings.AutoStart == true);
    }

    [Fact]
    public void DeserializeNotExists_Test()
    {
        // create a temp file and then delete it
        string settingsFullPath = Path.GetTempFileName();
        File.Delete(settingsFullPath);

        // Verify it was deleted
        Assert.False(File.Exists(settingsFullPath));

        AppSettings? readSettings = AppSettings.Deserialize(settingsFullPath);
        Assert.True(File.Exists(settingsFullPath));

        // This proves default settings were saved
        Assert.Equal("localhost", readSettings.ClientHost);

        File.Delete(settingsFullPath);
        Assert.False(File.Exists(settingsFullPath));
    }
}
