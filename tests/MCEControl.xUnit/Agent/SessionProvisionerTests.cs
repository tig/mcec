// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Unit tests for isolated session provisioning (#138): the provisioned copy is isolated from the
/// installed instance, agent commands are enabled only inside it, and teardown/reaping clean up.
/// Mutates the process-global <see cref="SessionProvisioner.SessionsRoot"/>/<c>BinariesDir</c>, so it
/// joins the serial agent collection; each test also uses a unique temp root.
/// </summary>
[Collection("AgentSerial")]
public class SessionProvisionerTests : IDisposable {
    private readonly string _root;
    private readonly string _fakeBinaries;
    private readonly string _origRoot;
    private readonly string _origBinaries;

    public SessionProvisionerTests() {
        _origRoot = SessionProvisioner.SessionsRoot;
        _origBinaries = SessionProvisioner.BinariesDir;

        string baseTemp = Path.Combine(Path.GetTempPath(), "mcec-provision-test", Path.GetRandomFileName());
        _root = Path.Combine(baseTemp, "sessions");
        _fakeBinaries = Path.Combine(baseTemp, "install");
        Directory.CreateDirectory(_fakeBinaries);

        // A minimal fake "installed" layout: an exe + a dll (copied), and the installed instance's mutable
        // config + a log (must NOT be copied; the session gets its own fresh config).
        File.WriteAllText(Path.Combine(_fakeBinaries, "mcec.exe"), "stub");
        File.WriteAllText(Path.Combine(_fakeBinaries, "mcec.dll"), "stub");
        File.WriteAllText(Path.Combine(_fakeBinaries, SettingsStore.SettingsFileName), "<AppSettings><AgentCommandsEnabled>false</AgentCommandsEnabled></AppSettings>");
        File.WriteAllText(Path.Combine(_fakeBinaries, "mcec.commands"), "<installed/>");
        File.WriteAllText(Path.Combine(_fakeBinaries, "mcec.log"), "old log");
        Directory.CreateDirectory(Path.Combine(_fakeBinaries, "runtimes"));
        File.WriteAllText(Path.Combine(_fakeBinaries, "runtimes", "native.dll"), "stub");

        SessionProvisioner.SessionsRoot = _root;
        SessionProvisioner.BinariesDir = _fakeBinaries;
    }

    public void Dispose() {
        SessionProvisioner.SessionsRoot = _origRoot;
        SessionProvisioner.BinariesDir = _origBinaries;
        try {
            string baseTemp = Directory.GetParent(_root)!.FullName;
            if (Directory.Exists(baseTemp)) {
                Directory.Delete(baseTemp, recursive: true);
            }
        }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void Provision_CopiesBinaries_ButNotInstalledConfigOrLogs() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: true);

        Assert.True(Directory.Exists(session.Directory));
        Assert.True(File.Exists(Path.Combine(session.Directory, "mcec.exe")));
        Assert.True(File.Exists(Path.Combine(session.Directory, "mcec.dll")));
        Assert.True(File.Exists(Path.Combine(session.Directory, "runtimes", "native.dll")));

        // The installed instance's log is never carried into the copy.
        Assert.False(File.Exists(Path.Combine(session.Directory, "mcec.log")));

        // The session's mcec.commands is the FRESH one (not the installed "<installed/>" stub).
        string commands = File.ReadAllText(Path.Combine(session.Directory, "mcec.commands"));
        Assert.DoesNotContain("<installed/>", commands);
    }

    [Fact]
    public void Provision_WritesAgentReadyConfig_EnabledOnlyInTheCopy() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: true);

        string settings = File.ReadAllText(Path.Combine(session.Directory, SettingsStore.SettingsFileName));
        Assert.Contains("<AgentCommandsEnabled>true</AgentCommandsEnabled>", settings);
        Assert.Contains("<McpServerEnabled>true</McpServerEnabled>", settings);
        // Isolation practice: no TCP server (avoids the firewall prompt) and it can't re-provision.
        Assert.Contains("<ActAsServer>false</ActAsServer>", settings);
        Assert.Contains("<AllowSessionProvisioning>false</AllowSessionProvisioning>", settings);

        string commands = File.ReadAllText(Path.Combine(session.Directory, "mcec.commands"));
        Assert.Contains("invoke", commands);
        Assert.Contains("enabled=\"true\"", commands);
    }

    [Fact]
    public void Provision_LeavesInstalledConfigUntouched() {
        string before = File.ReadAllText(Path.Combine(_fakeBinaries, SettingsStore.SettingsFileName));

        SessionProvisioner.Provision(mcpServerEnabled: false);

        string after = File.ReadAllText(Path.Combine(_fakeBinaries, SettingsStore.SettingsFileName));
        Assert.Equal(before, after); // the real install's settings are never written
    }

    [Fact]
    public void Provision_ReturnsConnectionInfo_WhenMcpEnabled() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: true);

        Assert.True(session.McpServerEnabled);
        Assert.True(session.Port > 0);
        Assert.Equal("127.0.0.1", session.BindAddress);
        Assert.EndsWith("mcec.exe", session.ExePath);
        Assert.Matches("^[0-9a-f]{12}$", session.SessionId);

        System.Text.Json.Nodes.JsonObject json = session.ToJsonObject();
        Assert.Equal($"http://127.0.0.1:{session.Port}/mcp", json["mcpEndpoint"]!.GetValue<string>());
    }

    [Fact]
    public void Teardown_DeletesTheSessionDirectory() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);
        Assert.True(Directory.Exists(session.Directory));

        bool removed = SessionProvisioner.Teardown(session.SessionId);

        Assert.True(removed);
        Assert.False(Directory.Exists(session.Directory));
    }

    [Fact]
    public void Teardown_UnknownButValidId_ReturnsTrue() {
        // A well-formed but non-existent session id is idempotent success (nothing to remove).
        Assert.True(SessionProvisioner.Teardown("abc123def456"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("a/b")]
    [InlineData("does-not-exist")]       // wrong shape
    [InlineData("ABC123DEF456")]         // upper-case hex is not the id shape
    public void Teardown_RejectsTraversalAndMalformedIds_WithoutDeletingOutsideRoot(string id) {
        // A sentinel next to the sessions root must survive a traversal-style teardown attempt.
        string parent = Directory.GetParent(_root)!.FullName;
        string sentinel = Path.Combine(parent, "install"); // created in the fixture
        Assert.True(Directory.Exists(sentinel));

        bool removed = SessionProvisioner.Teardown(id);

        Assert.False(removed);
        Assert.True(Directory.Exists(sentinel), "teardown must not delete anything outside the sessions root");
    }

    // --- #215: the session token is a real credential; written into the co-located config as the
    // instance's McpAuthToken and validated by end-session as the teardown credential.

    [Fact]
    public void Provision_WritesTheTokenAsTheSessionsMcpAuthToken() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: true);

        string settings = File.ReadAllText(Path.Combine(session.Directory, SettingsStore.SettingsFileName));
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.Contains($"<McpAuthToken>{session.Token}</McpAuthToken>", settings);
    }

    [Fact]
    public void ValidateTeardownToken_CorrectToken_IsValid() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);

        Assert.Equal(SessionTokenValidation.Valid,
            SessionProvisioner.ValidateTeardownToken(session.SessionId, session.Token));
    }

    [Theory]
    [InlineData("wrong-token")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateTeardownToken_WrongOrMissingToken_IsMismatch(string? token) {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);

        Assert.Equal(SessionTokenValidation.TokenMismatch,
            SessionProvisioner.ValidateTeardownToken(session.SessionId, token));
    }

    [Fact]
    public void ValidateTeardownToken_GoneSession_ReportsSessionGone() {
        Assert.Equal(SessionTokenValidation.SessionGone,
            SessionProvisioner.ValidateTeardownToken("abc123def456", "whatever"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("ABC123DEF456")]
    public void ValidateTeardownToken_MalformedId_ReportsInvalidId(string id) {
        Assert.Equal(SessionTokenValidation.InvalidId,
            SessionProvisioner.ValidateTeardownToken(id, "whatever"));
    }

    [Fact]
    public void ValidateTeardownToken_SessionWithoutConfig_FailsClosed_AndWritesNothing() {
        // A directory that exists but has no readable co-located config cannot be verified; the
        // check must fail closed AND must not write a default settings file into the directory as a
        // side effect (the reaper collects it later).
        string dir = Path.Combine(SessionProvisioner.SessionsRoot, "0123456789ab");
        Directory.CreateDirectory(dir);

        Assert.Equal(SessionTokenValidation.TokenMismatch,
            SessionProvisioner.ValidateTeardownToken("0123456789ab", "whatever"));
        Assert.False(File.Exists(Path.Combine(dir, SettingsStore.SettingsFileName)),
            "token validation must not create files in the session directory");
    }

    // --- #259: ListSessions is the operator's management view (the Settings dialog's Agent tab).

    [Fact]
    public void ListSessions_MissingRoot_ReturnsEmpty() {
        // The fixture points SessionsRoot at a directory Provision hasn't created yet.
        Assert.Empty(SessionProvisioner.ListSessions());
    }

    [Fact]
    public void ListSessions_ReturnsEachSession_WithSizeAndCreationTime() {
        ProvisionedSession first = SessionProvisioner.Provision(mcpServerEnabled: false);
        ProvisionedSession second = SessionProvisioner.Provision(mcpServerEnabled: false);
        // Make the ordering deterministic (directory creation stamps can tie within a tick).
        Directory.SetCreationTimeUtc(first.Directory, DateTime.UtcNow - TimeSpan.FromMinutes(5));

        System.Collections.Generic.IReadOnlyList<ProvisionedSessionInfo> sessions =
            SessionProvisioner.ListSessions();

        Assert.Equal(2, sessions.Count);
        // Newest first.
        Assert.Equal(second.SessionId, sessions[0].SessionId);
        Assert.Equal(first.SessionId, sessions[1].SessionId);
        Assert.All(sessions, s => Assert.True(s.SizeBytes > 0, "a provisioned copy has files"));
        Assert.All(sessions, s => Assert.False(s.IsRunning, "nothing launched these stubs"));
        Assert.All(sessions, s => Assert.True(Directory.Exists(s.Directory)));
    }

    [Fact]
    public void ListSessions_ReportsRunning_WhileTheExeIsLocked() {
        ProvisionedSession session = SessionProvisioner.Provision(mcpServerEnabled: false);
        string exe = Path.Combine(session.Directory, "mcec.exe");

        // Simulate a running session: hold mcec.exe open exclusively (the loader's lock is what
        // ListSessions probes and what makes Teardown/the reaper skip the directory).
        using (FileStream _ = File.Open(exe, FileMode.Open, FileAccess.Read, FileShare.None)) {
            ProvisionedSessionInfo info = Assert.Single(SessionProvisioner.ListSessions());
            Assert.True(info.IsRunning);
            Assert.False(SessionProvisioner.Teardown(session.SessionId),
                "a locked session must be skipped, not force-deleted");
            Assert.True(Directory.Exists(session.Directory));
        }

        // Once released ("process exited"), the same directory is stale and deletable.
        ProvisionedSessionInfo released = Assert.Single(SessionProvisioner.ListSessions());
        Assert.False(released.IsRunning);
        Assert.True(SessionProvisioner.Teardown(session.SessionId));
    }

    [Fact]
    public void ReapOrphans_RemovesOldDirs_KeepsFreshOnes() {
        ProvisionedSession stale = SessionProvisioner.Provision(mcpServerEnabled: false);
        ProvisionedSession fresh = SessionProvisioner.Provision(mcpServerEnabled: false);

        // Backdate the "stale" session's directory well past the reap window.
        Directory.SetCreationTimeUtc(stale.Directory, DateTime.UtcNow - TimeSpan.FromHours(48));

        int reaped = SessionProvisioner.ReapOrphans(TimeSpan.FromHours(12));

        Assert.True(reaped >= 1);
        Assert.False(Directory.Exists(stale.Directory));
        Assert.True(Directory.Exists(fresh.Directory));
    }
}
