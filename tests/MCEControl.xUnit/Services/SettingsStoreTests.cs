using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace MCEControl.xUnit;

// #216: persistence (load/save/path resolution) moved from AppSettings to SettingsStore.
// SettingsStore never shows dialogs and never emits telemetry — it returns a result object and
// the host decides. Nothing here touches AgentRuntime statics, the registry (beyond the
// read-tolerant machine-policy read Load performs), or the installed MCEC config (temp dirs only).
public class SettingsStoreTests
{
    private static string NewTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"mcec-store-{Guid.NewGuid():N}.settings");

    // --- Path resolution -------------------------------------------------------------------

    [Fact]
    public void GetSettingsPath_inProgramFiles_Test()
    {
        // If we're running within Program Files, use %AppData%
        string startupPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Kindel\MCE Controller";
        // should be "%appdata%\Kindel\MCE Controller" (rebrand 3.0; test uses UserAppDataPath directly)
        string settingsPath = $@"{Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1))}";
        Assert.Equal(settingsPath, SettingsStore.GetSettingsPath(startupPath));
    }

    [Fact]
    public void GetSettingsPath_standalone_Test()
    {
        // If we're running elsewhere (not in Program Files), use current dir
        Assert.Equal(".", SettingsStore.GetSettingsPath("."));
    }

    /// <summary>
    /// #216: the old check was startupPath.Contains(ProgramFiles) — a raw substring test that
    /// false-positived on sibling directories like "C:\Program Files Custom". The replacement is a
    /// separator-aware, case-insensitive path-prefix comparison.
    /// </summary>
    [Fact]
    public void GetSettingsPath_ProgramFilesSibling_IsNotRedirected()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string sibling = $"{programFiles} Custom\\MCE Controller";
        Assert.Equal(sibling, SettingsStore.GetSettingsPath(sibling));
    }

    [Theory]
    [InlineData(@"C:\Program Files\App", @"C:\Program Files", true)] // descendant
    [InlineData(@"C:\Program Files", @"C:\Program Files", true)] // the dir itself
    [InlineData(@"C:\program files\app", @"C:\Program Files", true)] // case-insensitive
    [InlineData(@"C:\Program Files\", @"C:\Program Files", true)] // trailing separator
    [InlineData(@"C:\Program Files Custom\App", @"C:\Program Files", false)] // sibling prefix (the old bug)
    [InlineData(@"C:\Other\Program Files\App", @"C:\Program Files", false)] // substring elsewhere
    public void IsPathUnder_SeparatorAware(string path, string directory, bool expected)
    {
        Assert.Equal(expected, SettingsStore.IsPathUnder(path, directory));
    }

    // --- Load outcomes ---------------------------------------------------------------------

    [Fact]
    public void Load_MissingFile_CreatesDefaults()
    {
        string settingsFile = NewTempSettingsPath();
        try
        {
            Assert.False(File.Exists(settingsFile));

            SettingsLoadResult result = SettingsStore.Load(settingsFile);

            Assert.Equal(SettingsLoadOutcome.CreatedDefault, result.Outcome);
            Assert.Null(result.Error);
            Assert.NotNull(result.Settings);
            // Defaults were used...
            Assert.Equal("localhost", result.Settings.ClientHost);
            Assert.True(result.Settings.ActAsServer);
            // ...and persisted for next run.
            Assert.True(File.Exists(settingsFile));
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    /// <summary>
    /// Issue #155: a settings file corrupted by a mid-write crash, disk error, or hand-edit must not
    /// put the app into a fail-to-start state (GUI or headless --mcp). Load must return a
    /// parse-error RESULT (no throw, no dialog — #216) with defaults, and must not silently
    /// overwrite the corrupt file (so the user can recover it).
    /// </summary>
    [Theory]
    [InlineData("<?xml version=\"1.0\"?><AppSettings><AutoStart>tru")] // truncated mid-write
    [InlineData("this is not xml at all")] // hand-edit / wrong file
    [InlineData("<?xml version=\"1.0\"?><NotAppSettings/>")] // wrong root element
    public void Load_CorruptSettingsFile_ReturnsParseError_AndPreservesFile(string content)
    {
        string settingsFile = NewTempSettingsPath();
        try
        {
            File.WriteAllText(settingsFile, content);

            SettingsLoadResult result = SettingsStore.Load(settingsFile);

            Assert.Equal(SettingsLoadOutcome.ParseError, result.Outcome);
            Assert.NotNull(result.Error);
            Assert.False(string.IsNullOrEmpty(result.ErrorDetail));
            // Defaults were used
            Assert.Equal("localhost", result.Settings.ClientHost);
            Assert.True(result.Settings.ActAsServer);
            // Recovery: the corrupt file must NOT have been overwritten
            Assert.Equal(content, File.ReadAllText(settingsFile));
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    /// <summary>
    /// SECURITY (#216, the real CA3075 fix): mcec.settings is user-writable and MCEC is
    /// network-facing, so a settings file smuggling a DTD (entity-expansion / external-entity
    /// tricks) must be REJECTED — DtdProcessing.Prohibit, XmlResolver = null — and treated like
    /// any other corrupt file: parse-error result, defaults, file preserved.
    /// </summary>
    [Fact]
    public void Load_SettingsFileWithDtd_IsRejected()
    {
        string settingsFile = NewTempSettingsPath();
        string content = "<?xml version=\"1.0\"?>\n" +
            "<!DOCTYPE AppSettings [<!ENTITY x \"injected\">]>\n" +
            "<AppSettings><ClientHost>&x;</ClientHost></AppSettings>";
        try
        {
            File.WriteAllText(settingsFile, content);

            SettingsLoadResult result = SettingsStore.Load(settingsFile);

            Assert.Equal(SettingsLoadOutcome.ParseError, result.Outcome);
            // The DTD must not have been processed: defaults, not the entity-injected value.
            Assert.Equal("localhost", result.Settings.ClientHost);
            Assert.Equal(content, File.ReadAllText(settingsFile));
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    /// <summary>
    /// Issue #155 (second bug): the UnauthorizedAccessException path left settings null and then
    /// dereferenced it — the "handled" branch itself crashed with an NRE. Opening a directory as a
    /// file throws UnauthorizedAccessException — the same exception type as an ACL-denied settings
    /// file — without touching real ACLs.
    /// </summary>
    [Fact]
    public void Load_UnauthorizedAccess_ReturnsAccessDenied_WithDefaults()
    {
        string dirPath = Path.Combine(Path.GetTempPath(), $"mcec-uae-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirPath);
        try
        {
            SettingsLoadResult result = SettingsStore.Load(dirPath);

            Assert.Equal(SettingsLoadOutcome.AccessDenied, result.Outcome);
            Assert.NotNull(result.Error);
            Assert.NotNull(result.Settings);
            Assert.Equal("localhost", result.Settings.ClientHost);
        }
        finally
        {
            Directory.Delete(dirPath);
        }
    }

    // --- Save ------------------------------------------------------------------------------

    [Fact]
    public void TrySave_Failure_ReturnsFalseWithError_NoThrow()
    {
        // A path inside a directory that does not exist cannot be written.
        string settingsFile = Path.Combine(Path.GetTempPath(), $"mcec-nodir-{Guid.NewGuid():N}", "mcec.settings");

        bool ok = SettingsStore.TrySave(settingsFile, new AppSettings(), out Exception? error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    // --- Round trip / file format ----------------------------------------------------------

    /// <summary>
    /// #216: save/load round trip must preserve EVERY public settable property (reflection with
    /// probe values, so a future property added without XML serialization support fails here).
    /// </summary>
    [Fact]
    public void SaveLoad_RoundTrip_PreservesEveryProperty()
    {
        string settingsFile = NewTempSettingsPath();
        try
        {
            AppSettings original = new AppSettings();
            List<PropertyInfo> props = [];
            foreach (PropertyInfo prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    props.Add(prop);
                    prop.SetValue(original, ProbeValueFor(prop, prop.GetValue(original)));
                }
            }
            Assert.NotEmpty(props);

            Assert.True(SettingsStore.TrySave(settingsFile, original, out Exception? error), $"save failed: {error}");
            SettingsLoadResult result = SettingsStore.Load(settingsFile);
            Assert.Equal(SettingsLoadOutcome.Loaded, result.Outcome);

            foreach (PropertyInfo prop in props)
            {
                object? expected = prop.GetValue(original);
                object? actual = prop.GetValue(result.Settings);
                Assert.True(Equals(expected, actual),
                    $"Property '{prop.Name}' did not round-trip: wrote '{expected}', read '{actual}'.");
            }
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    /// <summary>
    /// #216: the settings FILE FORMAT must not change across the AppSettings/SettingsStore split.
    /// This fixture is verbatim what the pre-split code (XmlSerializer via AppSettings.Serialize)
    /// wrote; it must load with identical values after the split.
    /// </summary>
    [Fact]
    public void Load_CurrentFormatFile_LoadsIdentically()
    {
        string settingsFile = NewTempSettingsPath();
        string content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<AppSettings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n" +
            "  <AutoStart>true</AutoStart>\n" +
            "  <HideOnStartup>false</HideOnStartup>\n" +
            "  <TextBoxLogThreshold>DEBUG</TextBoxLogThreshold>\n" +
            "  <ActAsClient>true</ActAsClient>\n" +
            "  <ActAsServer>false</ActAsServer>\n" +
            "  <ClientDelayTime>12345</ClientDelayTime>\n" +
            "  <CommandPacing>50</CommandPacing>\n" +
            "  <ClientHost>myhost.example</ClientHost>\n" +
            "  <ClientPort>5099</ClientPort>\n" +
            "  <Opacity>80</Opacity>\n" +
            "  <ServerPort>5098</ServerPort>\n" +
            "  <SocketServerBindAddress>127.0.0.1</SocketServerBindAddress>\n" +
            "  <WakeupEnabled>true</WakeupEnabled>\n" +
            "  <SerialServerParity>Even</SerialServerParity>\n" +
            "  <SerialServerStopBits>Two</SerialServerStopBits>\n" +
            "  <WindowLocation>\n    <X>10</X>\n    <Y>20</Y>\n  </WindowLocation>\n" +
            "  <WindowSize>\n    <Width>800</Width>\n    <Height>600</Height>\n  </WindowSize>\n" +
            "  <AgentCommandsEnabled>true</AgentCommandsEnabled>\n" +
            "  <McpServerEnabled>true</McpServerEnabled>\n" +
            "  <McpBindAddress>127.0.0.1</McpBindAddress>\n" +
            "  <McpHttpPort>7777</McpHttpPort>\n" +
            "  <CommandOverlayPosition>Left</CommandOverlayPosition>\n" +
            "  <AgentRecordMaxFps>15</AgentRecordMaxFps>\n" +
            "</AppSettings>";
        try
        {
            File.WriteAllText(settingsFile, content);

            SettingsLoadResult result = SettingsStore.Load(settingsFile);

            Assert.Equal(SettingsLoadOutcome.Loaded, result.Outcome);
            AppSettings s = result.Settings;
            Assert.True(s.AutoStart);
            Assert.False(s.HideOnStartup);
            Assert.Equal("DEBUG", s.TextBoxLogThreshold);
            Assert.True(s.ActAsClient);
            Assert.False(s.ActAsServer);
            Assert.Equal(12345, s.ClientDelayTime);
            Assert.Equal(50, s.CommandPacing);
            Assert.Equal("myhost.example", s.ClientHost);
            Assert.Equal(5099, s.ClientPort);
            Assert.Equal(80, s.Opacity);
            Assert.Equal(5098, s.ServerPort);
            Assert.Equal("127.0.0.1", s.SocketServerBindAddress);
            Assert.True(s.WakeupEnabled);
            Assert.Equal(System.IO.Ports.Parity.Even, s.SerialServerParity);
            Assert.Equal(System.IO.Ports.StopBits.Two, s.SerialServerStopBits);
            Assert.Equal(new Point(10, 20), s.WindowLocation);
            Assert.Equal(new Size(800, 600), s.WindowSize);
            Assert.True(s.AgentCommandsEnabled);
            Assert.True(s.McpServerEnabled);
            Assert.Equal("127.0.0.1", s.McpBindAddress);
            Assert.Equal(7777, s.McpHttpPort);
            Assert.Equal(OverlayPosition.Left, s.CommandOverlayPosition);
            Assert.Equal(15, s.AgentRecordMaxFps);
            // An element absent from the file keeps its initialized default.
            Assert.True(s.CommandOverlayEnabled);
            Assert.Equal(10, s.ActivityMonitorDebounceTime);
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    /// <summary>Builds a probe value guaranteed to differ from the property's default.</summary>
    private static object ProbeValueFor(PropertyInfo prop, object? currentValue)
    {
        Type t = prop.PropertyType;
        if (t == typeof(bool))
        {
            return !(bool)currentValue!;
        }
        if (t == typeof(int))
        {
            return (int)currentValue! + 7;
        }
        if (t == typeof(string))
        {
            return $"probe-{prop.Name}";
        }
        if (t == typeof(Point))
        {
            return new Point(111, 222);
        }
        if (t == typeof(Size))
        {
            return new Size(333, 444);
        }
        if (t.IsEnum)
        {
            foreach (object value in Enum.GetValues(t))
            {
                if (!value.Equals(currentValue))
                {
                    return value;
                }
            }
        }
        throw new NotSupportedException(
            $"No probe value for property '{prop.Name}' of type {t}. Extend ProbeValueFor so the round-trip test covers it.");
    }
}
