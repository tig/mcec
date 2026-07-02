// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// First-run behavior: a missing mcec.commands is not an error — LoadCommands creates the default
/// file (the full built-in catalog, every command Enabled="false", version-stamped, guidance
/// comments) the same way SettingsStore creates a default mcec.settings, then loads it. This is the
/// contract docs/home-automation.md documents. Creation is quiet on failure; MCEC runs on built-ins
/// alone.
/// </summary>
public class DefaultCommandsFileTests {
    private static string TempCommandsPath() =>
        Path.Combine(Path.GetTempPath(), $"mcec-test-{Guid.NewGuid():N}", "mcec.commands");

    [Fact]
    public void LoadCommands_MissingFile_CreatesDefaultAndLoadsIt() {
        string path = TempCommandsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {
            SerializedCommands cmds = SerializedCommands.LoadCommands(path, "3.0.0");

            Assert.True(File.Exists(path), "default commands file was not created");
            Assert.NotNull(cmds);
            // The full built-in catalog, with NOTHING enabled — the actuation surface must not
            // change just because the file now exists (the security model's default-deny).
            Assert.True(cmds.Count > 100, $"expected the built-in catalog, got {cmds.Count} commands");
            Assert.All(cmds.commandArray, c => Assert.False(c.Enabled, $"'{c.Cmd}' must not be enabled in the default file"));
            Assert.Equal("3.0.0", cmds.Version);

            // The template carries the standard guidance comments for the user to edit.
            string content = File.ReadAllText(path);
            Assert.Contains("<!--", content, StringComparison.Ordinal);

            // A second load round-trips the created file cleanly.
            SerializedCommands again = SerializedCommands.LoadCommands(path, "3.0.0");
            Assert.NotNull(again);
            Assert.Equal(cmds.Count, again.Count);
        }
        finally {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void LoadCommands_ExistingFile_IsNotOverwritten() {
        string path = TempCommandsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {
            var existing = new SerializedCommands {
                commandArray = [new PauseCommand { Cmd = "userpause", Args = "50", Enabled = true }],
            };
            // Same version as the load below so the upgrade-rewrite path stays out of the way.
            SerializedCommands.SaveCommands(path, existing, "3.0.0");
            byte[] before = File.ReadAllBytes(path);

            SerializedCommands cmds = SerializedCommands.LoadCommands(path, "3.0.0");

            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(1, cmds.Count);
            Assert.Equal("userpause", cmds.commandArray[0].Cmd);
        }
        finally {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TryCreateDefaultCommandsFile_UnwritableLocation_DoesNotThrow() {
        // Nonexistent directory → DirectoryNotFoundException (an IOException) inside the helper.
        string path = Path.Combine(Path.GetTempPath(), $"mcec-test-{Guid.NewGuid():N}", "nope", "mcec.commands");

        Exception? ex = Record.Exception(() => SerializedCommands.TryCreateDefaultCommandsFile(path, "3.0.0"));

        Assert.Null(ex);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CommandInvoker_Create_MissingFile_CreatesDefaultAndRegistersBuiltIns() {
        string path = TempCommandsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {
            CommandInvoker invoker = CommandInvoker.Create(path, "3.0.0", disableInternalCommands: false);

            Assert.True(File.Exists(path), "default commands file was not created");
            Assert.True(invoker.Count > 0, "built-in commands were not registered");
        }
        finally {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
