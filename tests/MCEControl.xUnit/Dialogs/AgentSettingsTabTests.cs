// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Windows.Forms;
using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Headless tests for the Settings dialog's Agent tab (#259): the
/// <see cref="AppSettings.AllowSessionProvisioning"/> opt-in follows the #213 Bind/clone contract,
/// and the session list/delete surface drives <see cref="SessionProvisioner"/>. Points the
/// process-global <see cref="SessionProvisioner.SessionsRoot"/> at a temp directory, so it joins
/// the serial agent collection.
/// </summary>
[Collection("AgentSerial")]
public class AgentSettingsTabTests : IDisposable {
    private readonly string _root;
    private readonly string _origRoot;

    public AgentSettingsTabTests() {
        _origRoot = SessionProvisioner.SessionsRoot;
        _root = Path.Combine(Path.GetTempPath(), "mcec-agenttab-test", Path.GetRandomFileName());
        SessionProvisioner.SessionsRoot = _root;
    }

    public void Dispose() {
        SessionProvisioner.SessionsRoot = _origRoot;
        try {
            string baseTemp = Directory.GetParent(_root)!.FullName;
            if (Directory.Exists(baseTemp)) {
                Directory.Delete(baseTemp, recursive: true);
            }
        }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>Creates a fake session directory (a 12-hex id with one file) under the temp root.</summary>
    private string CreateFakeSession(string id) {
        string dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mcec.exe"), "stub");
        return dir;
    }

    private static T FindControl<T>(Control root, string name) where T : Control {
        return (T)root.Controls.Find(name, searchAllChildren: true)[0];
    }

    [Fact]
    public void AgentTab_BindPopulatesAndMutatesOnlyTheBoundClone() {
        var original = new AppSettings { AllowSessionProvisioning = false };
        var clone = (AppSettings)original.Clone();

        using var tab = new AgentSettingsTab();
        tab.Bind(clone);

        var checkbox = FindControl<CheckBox>(tab, "_checkBoxAllowProvisioning");
        Assert.False(checkbox.Checked);
        // The tab has no invalid states.
        Assert.True(tab.IsValid);
        Assert.True(tab.ValidateSection());

        int changes = 0;
        tab.ValidityChanged += (_, _) => changes++;

        checkbox.Checked = true;
        Assert.True(clone.AllowSessionProvisioning);
        Assert.False(original.AllowSessionProvisioning); // Cancel semantics depend on this
        Assert.True(changes > 0);
        Assert.True(tab.IsValid);
    }

    [Fact]
    public void AgentTab_ListsSessions_WithIdAgeSizeAndStatus() {
        _ = CreateFakeSession("0123456789ab");
        _ = CreateFakeSession("ba9876543210");

        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings());

        var grid = FindControl<DataGridView>(tab, "_gridSessions");
        Assert.Equal(2, grid.Rows.Count);
        var deleteAll = FindControl<Button>(tab, "_buttonDeleteAll");
        Assert.True(deleteAll.Enabled);

        // Each row carries the id, a formatted age/size, a status, and the Delete button caption.
        foreach (DataGridViewRow row in grid.Rows) {
            Assert.Matches("^[0-9a-f]{12}$", (string)row.Cells[0].Value!);
            Assert.False(string.IsNullOrEmpty((string)row.Cells[1].Value!));
            Assert.False(string.IsNullOrEmpty((string)row.Cells[2].Value!));
            Assert.Equal("Stale", (string)row.Cells[3].Value!);
            Assert.Equal("Delete", (string)row.Cells[4].Value!);
        }
    }

    [Fact]
    public void AgentTab_EmptyList_DisablesDeleteAllAndShowsNoSessionsLabel() {
        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings());

        Assert.Equal(0, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);
        Assert.False(FindControl<Button>(tab, "_buttonDeleteAll").Enabled);
        Assert.True(FindControl<Label>(tab, "_labelNoSessions").Visible);
    }

    [Fact]
    public void AgentTab_DeleteSessions_RemovesDirectoriesAndRefreshes() {
        string dir1 = CreateFakeSession("0123456789ab");
        string dir2 = CreateFakeSession("ba9876543210");

        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings());
        Assert.Equal(2, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);

        tab.DeleteSessions(["0123456789ab"]);

        Assert.False(Directory.Exists(dir1));
        Assert.True(Directory.Exists(dir2));
        Assert.Equal(1, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);

        tab.DeleteSessions(["ba9876543210"]);
        Assert.False(Directory.Exists(dir2));
        Assert.Equal(0, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);
        Assert.False(FindControl<Button>(tab, "_buttonDeleteAll").Enabled);
    }

    [Fact]
    public void AgentTab_DeletesStrayNonSessionIdDirectory() {
        // ListSessions enumerates ALL directories under the sessions root, so a stray folder whose name
        // is not a 12-hex session id can appear in the list. The operator must still be able to delete it
        // (the MCP end-session id gate does not apply to this local management surface), and it must not
        // be misreported as "running".
        string stray = Path.Combine(_root, "not-a-session-id");
        Directory.CreateDirectory(stray);
        File.WriteAllText(Path.Combine(stray, "leftover.txt"), "stub");

        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings());
        Assert.Equal(1, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);

        tab.DeleteSessions(["not-a-session-id"]);

        Assert.False(Directory.Exists(stray));
        Assert.Equal(0, FindControl<DataGridView>(tab, "_gridSessions").Rows.Count);
    }

    // --- The pure formatting helpers the list renders with.

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min")]
    [InlineData(59 * 60, "59 min")]
    [InlineData(60 * 60, "1 h")]
    [InlineData(23 * 60 * 60, "23 h")]
    [InlineData(24 * 60 * 60, "1 d")]
    [InlineData(72 * 60 * 60, "3 d")]
    public void FormatAge_FormatsCompactly(int seconds, string expected) {
        Assert.Equal(expected, AgentSettingsTab.FormatAge(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatAge_ClampsNegativeToJustNow() {
        // A directory created "in the future" (clock skew) must not render a negative age.
        Assert.Equal("just now", AgentSettingsTab.FormatAge(TimeSpan.FromSeconds(-30)));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(34 * 1024, "34 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(126257725, "120.4 MB")]
    public void FormatSize_FormatsCompactly(long bytes, string expected) {
        Assert.Equal(expected, AgentSettingsTab.FormatSize(bytes));
    }
}
