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
        string startupPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Kindel\MCE Controller";
        // should be "%appdata%\Kindel\MCE Controller" (rebrand 3.0; test uses UserAppDataPath directly)
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

    /// <summary>
    /// Issue #155: a settings file corrupted by a mid-write crash, disk error, or hand-edit must not
    /// put the app into a fail-to-start state (GUI or headless --mcp). Deserialize must fall back to
    /// defaults AND must not silently overwrite the corrupt file (so the user can recover it).
    /// </summary>
    [Theory]
    [InlineData("<?xml version=\"1.0\"?><AppSettings><AutoStart>tru")] // truncated mid-write
    [InlineData("this is not xml at all")] // hand-edit / wrong file
    [InlineData("<?xml version=\"1.0\"?><NotAppSettings/>")] // wrong root element
    public void Deserialize_CorruptSettingsFile_ReturnsDefaults_AndPreservesFile(string content)
    {
        bool priorHeadless = AgentRuntime.Headless;
        AgentRuntime.Headless = true; // never pop a modal dialog in tests
        string settingsFullPath = Path.Combine(Path.GetTempPath(), $"mcec-corrupt-{Guid.NewGuid():N}.settings");
        try
        {
            File.WriteAllText(settingsFullPath, content);

            AppSettings settings = AppSettings.Deserialize(settingsFullPath);

            Assert.NotNull(settings);
            // Defaults were used
            Assert.Equal("localhost", settings.ClientHost);
            Assert.True(settings.ActAsServer);
            // Recovery: the corrupt file must NOT have been overwritten
            Assert.Equal(content, File.ReadAllText(settingsFullPath));
        }
        finally
        {
            AgentRuntime.Headless = priorHeadless;
            File.Delete(settingsFullPath);
        }
    }

    /// <summary>
    /// Issue #155 (second bug): the UnauthorizedAccessException path left settings null and then
    /// dereferenced it (settings!.GetTelemetryDictionary()); the "handled" branch itself crashed
    /// with an NRE. Opening a directory as a file throws UnauthorizedAccessException; the same
    /// exception type as an ACL-denied settings file; without touching real ACLs.
    /// </summary>
    [Fact]
    public void Deserialize_UnauthorizedAccess_ReturnsDefaults_NoNullReference()
    {
        bool priorHeadless = AgentRuntime.Headless;
        AgentRuntime.Headless = true; // never pop a modal dialog in tests
        string dirPath = Path.Combine(Path.GetTempPath(), $"mcec-uae-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirPath);
        try
        {
            AppSettings settings = AppSettings.Deserialize(dirPath);

            Assert.NotNull(settings);
            Assert.Equal("localhost", settings.ClientHost);
        }
        finally
        {
            AgentRuntime.Headless = priorHeadless;
            Directory.Delete(dirPath);
        }
    }

    /// <summary>
    /// Issue #155 (third bug): Convert.ToBoolean on a junk DisableInternalCommands registry value
    /// (e.g. REG_SZ "banana") threw FormatException and crashed startup. The conversion seam must
    /// tolerate junk and fall back to the default; no real registry involved here.
    /// </summary>
    [Theory]
    [InlineData(null, false, false)] // absent value -> default
    [InlineData(null, true, true)]
    [InlineData("banana", false, false)] // junk REG_SZ -> default (FormatException path)
    [InlineData("banana", true, true)]
    [InlineData("true", false, true)] // valid values still parse
    [InlineData("False", true, false)]
    [InlineData(1, false, true)] // REG_DWORD
    [InlineData(0, true, false)]
    public void RegistryValueToBoolean_ToleratesJunk(object? value, bool defaultValue, bool expected)
    {
        Assert.Equal(expected, AppSettings.RegistryValueToBoolean(value, defaultValue));
    }

    [Fact]
    public void RegistryValueToBoolean_BinaryJunk_ReturnsDefault()
    {
        // REG_BINARY comes back as byte[] -> InvalidCastException path
        Assert.False(AppSettings.RegistryValueToBoolean(new byte[] { 1, 2, 3 }, false));
        Assert.True(AppSettings.RegistryValueToBoolean(new byte[] { 1, 2, 3 }, true));
    }

    /// <summary>
    /// Issue #155 review follow-up (M1): Registry.GetValue itself can throw SecurityException
    /// (deny-read ACE) or IOException (key marked for deletion). GetRegistryValue must swallow
    /// these and return the supplied default; a registry problem must never crash startup
    /// (this also covers the TelemetryService opt-in read). Exercised through the injectable
    /// seam; the real registry is never touched.
    /// </summary>
    [Fact]
    public void GetRegistryValue_SecurityException_ReturnsDefault()
    {
        object? result = AppSettings.GetRegistryValue("Whatever", "fallback",
            (key, name, def) => throw new System.Security.SecurityException("deny-read ACE"));
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void GetRegistryValue_IOException_ReturnsDefault()
    {
        object? result = AppSettings.GetRegistryValue("Whatever", 42,
            (key, name, def) => throw new IOException("key has been marked for deletion"));
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetRegistryValue_UnauthorizedAccessException_ReturnsDefault()
    {
        object? result = AppSettings.GetRegistryValue("Whatever", null,
            (key, name, def) => throw new UnauthorizedAccessException("no read access"));
        Assert.Null(result);
    }

    [Fact]
    public void GetRegistryValue_LegacyKeyThrows_ReturnsDefault()
    {
        // Current key reads fine (absent -> null); the legacy fallback key is the one that throws.
        object? result = AppSettings.GetRegistryValue("Whatever", false,
            (key, name, def) => key == AppSettings.RegistryKeyPath
                ? null
                : throw new System.Security.SecurityException("deny-read ACE on legacy key"));
        Assert.Equal(false, result);
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
