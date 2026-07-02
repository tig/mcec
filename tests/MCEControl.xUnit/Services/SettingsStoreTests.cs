using System;
using System.IO;
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
}
