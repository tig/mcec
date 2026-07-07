// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public void AgentTab_ProvisionButton_TracksTheOptInCheckbox() {
        // "Provision new…" (#296) mirrors the MCP tool's AllowSessionProvisioning gate: available only
        // when the opt-in is ticked, and it follows the checkbox live.
        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings { AllowSessionProvisioning = false });

        var provision = FindControl<Button>(tab, "_buttonProvision");
        var checkbox = FindControl<CheckBox>(tab, "_checkBoxAllowProvisioning");
        Assert.False(provision.Enabled);

        checkbox.Checked = true;
        Assert.True(provision.Enabled);

        checkbox.Checked = false;
        Assert.False(provision.Enabled);
    }

    [Fact]
    public void ProvisionButton_EnabledWhenBoundToAuthorizedSettings() {
        using var tab = new AgentSettingsTab();
        tab.Bind(new AppSettings { AllowSessionProvisioning = true });
        Assert.True(FindControl<Button>(tab, "_buttonProvision").Enabled);
    }

    private static ProvisionedSession SampleSession(bool mcpServerEnabled) => new() {
        SessionId = "0123456789ab",
        Directory = @"C:\sessions\0123456789ab",
        ExePath = @"C:\sessions\0123456789ab\mcec.exe",
        McpServerEnabled = mcpServerEnabled,
        BindAddress = "127.0.0.1",
        Port = 51515,
        Token = "deadbeefcafef00d",
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProvisionedInstanceDialog_HandoffText_HasLaunchTokenAndConditionalEndpoint(bool mcpServerEnabled) {
        string text = ProvisionedInstanceDialog.BuildHandoffText(SampleSession(mcpServerEnabled));

        // Always: the stdio launch line, the MCP-client registration example, AND the session
        // identity + teardown token (#308 review: a stdio-only handoff must still carry the
        // credential; the operator may only ever copy this block).
        Assert.Contains(@"C:\sessions\0123456789ab\mcec.exe"" --mcp", text);
        Assert.Contains("claude mcp add mcec", text);
        Assert.Contains("Session id: 0123456789ab", text);
        Assert.Contains(@"C:\sessions\0123456789ab", text);
        Assert.Contains("deadbeefcafef00d", text);
        // The HTTP endpoint + bearer line only when the session's MCP server is enabled.
        Assert.Equal(mcpServerEnabled, text.Contains("http://127.0.0.1:51515/mcp"));
        Assert.Equal(mcpServerEnabled, text.Contains("Authorization: Bearer deadbeefcafef00d"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProvisionedInstanceDialog_AgentPrompt_CarriesSessionFactsAndRulesOfEngagement(bool mcpServerEnabled) {
        string prompt = ProvisionedInstanceDialog.BuildAgentPrompt(SampleSession(mcpServerEnabled));

        // The session identity and token custody the connect-time instructions cannot know.
        Assert.Contains("0123456789ab", prompt);
        Assert.Contains(@"C:\sessions\0123456789ab", prompt);
        Assert.Contains("deadbeefcafef00d", prompt);
        Assert.Equal(mcpServerEnabled, prompt.Contains("http://127.0.0.1:51515/mcp"));

        // The rules of engagement (#307): consent path, never edit config files, e-stop, teardown.
        Assert.Contains("request-command-access", prompt);
        Assert.Contains("command-disabled", prompt);
        Assert.Contains("NEVER edit mcec.commands", prompt);
        Assert.Contains("emergency-stopped", prompt);
        Assert.Contains("end-session", prompt);
        // #308 review: mcec:exit is DISABLED in every provisioned instance (not ProvisionedByDefault),
        // so the briefing must not instruct it as the teardown path; disconnecting stops stdio.
        Assert.DoesNotContain("mcec:exit", prompt);

        // A template the operator appends their task to.
        Assert.EndsWith("<describe the task here>", prompt);
    }

    [Fact]
    public void ProvisionedInstanceDialog_AgentPrompt_MentionsOnlyToolsTheFullSurfaceServes() {
        // The prompt must never drift from the served surface: every tool name it tells the agent to
        // call has to exist in the full tools/list.
        string prompt = ProvisionedInstanceDialog.BuildAgentPrompt(SampleSession(true));
        var listRequest = new System.Text.Json.Nodes.JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/list",
            ["params"] = new System.Text.Json.Nodes.JsonObject(),
        };
        var tools = AgentServer.Dispatch(listRequest)!["result"]!.AsObject()["tools"]!.AsArray();
        var served = new HashSet<string>(tools.Select(t => t!["name"]!.GetValue<string>()));

        // send_command dropped out of the briefing when teardown moved to disconnect + end-session
        // (#308 review: mcec:exit is disabled in provisioned instances), so it is no longer pinned.
        foreach (string tool in (string[])["request-command-access", "end-session"]) {
            Assert.Contains(tool, prompt);
            Assert.Contains(tool, served);
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
        Assert.Equal(expected, SessionDisplayFormat.Age(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatAge_ClampsNegativeToJustNow() {
        // A directory created "in the future" (clock skew) must not render a negative age.
        Assert.Equal("just now", SessionDisplayFormat.Age(TimeSpan.FromSeconds(-30)));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(34 * 1024, "34 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(126257725, "120.4 MB")]
    public void FormatSize_FormatsCompactly(long bytes, string expected) {
        Assert.Equal(expected, SessionDisplayFormat.Size(bytes));
    }
}
