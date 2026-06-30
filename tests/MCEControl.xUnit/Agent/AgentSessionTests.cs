// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Unit tests for the #86 session store (<see cref="AgentSession"/>): id format, lazily-allocated
/// per-session artifact directory, and the target/observation/action/error state an agent and the
/// evidence bundle read back.
/// </summary>
public class AgentSessionTests {
    private static string TempRoot() =>
        Path.Combine(Path.GetTempPath(), "mcec-session-test", Path.GetRandomFileName());

    [Fact]
    public void Create_GeneratesTwelveHexCharId() {
        AgentSession session = AgentSession.Create(TempRoot());

        Assert.Matches(new Regex("^[0-9a-f]{12}$"), session.SessionId);
    }

    [Fact]
    public void Create_GivesEachSessionADistinctId() {
        string root = TempRoot();
        Assert.NotEqual(AgentSession.Create(root).SessionId, AgentSession.Create(root).SessionId);
    }

    [Fact]
    public void ArtifactDir_IsUnderRootAndCarriesId_ButIsNotCreatedUntilEnsured() {
        string root = TempRoot();
        AgentSession session = AgentSession.Create(root);

        Assert.StartsWith(root, session.ArtifactDir);
        Assert.Contains(session.SessionId, session.ArtifactDir);
        Assert.False(Directory.Exists(session.ArtifactDir), "artifact dir must be reserved, not created, until needed");
    }

    [Fact]
    public void EnsureArtifactDir_CreatesTheDirectory() {
        string root = TempRoot();
        AgentSession session = AgentSession.Create(root);
        try {
            string dir = session.EnsureArtifactDir();

            Assert.True(Directory.Exists(dir));
            Assert.Equal(session.ArtifactDir, dir);
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordObservation_SetsLastObservationAndActiveTarget() {
        AgentSession session = AgentSession.Create(TempRoot());
        JsonObject window = new() { ["handle"] = 42 };

        session.RecordObservation(new JsonObject { ["tree"] = "x", ["window"] = window.DeepClone() }, window);

        Assert.Equal("x", session.LastObservation!["tree"]!.GetValue<string>());
        Assert.Equal(42, session.ActiveTarget!["handle"]!.GetValue<int>());
    }

    [Fact]
    public void Getters_ReturnCopies_SoCallersCannotMutateSessionState() {
        AgentSession session = AgentSession.Create(TempRoot());
        session.RecordObservation(new JsonObject { ["tree"] = "x" });

        session.LastObservation!["tree"] = "mutated";

        Assert.Equal("x", session.LastObservation!["tree"]!.GetValue<string>());
    }

    [Fact]
    public void RecordActionAndError_AreReflectedInStatus() {
        AgentSession session = AgentSession.Create(TempRoot());
        session.RecordAction("query");
        session.RecordError(new JsonObject { ["category"] = "no-target", ["code"] = "window-not-found" });

        JsonObject status = session.ToStatusJson();

        Assert.Equal(session.SessionId, status["sessionId"]!.GetValue<string>());
        Assert.Equal("query", status["lastAction"]!.GetValue<string>());
        Assert.Equal("no-target", status["lastError"]!["category"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(status["artifactDir"]!.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(status["startedAt"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("query")]
    [InlineData("capture")]
    [InlineData("find")]
    [InlineData("wait-for")]
    public void RecordToolOutcome_Success_RecordsObservationAndTargetForEveryObservationTool(string tool) {
        // The Codex P2 on #116: wait-for was skipped and find/wait-for passed a null target. Each
        // observation tool that resolves a window must record both the observation and the active target.
        AgentSession session = AgentSession.Create(TempRoot());
        JsonObject result = new() { ["found"] = true, ["window"] = new JsonObject { ["handle"] = 7 } };

        session.RecordToolOutcome(tool, AgentToolResult.Success(result));

        Assert.NotNull(session.LastObservation);
        Assert.Equal(7, session.ActiveTarget!["handle"]!.GetValue<int>());
    }

    [Fact]
    public void RecordToolOutcome_Action_DoesNotRecordObservation() {
        AgentSession session = AgentSession.Create(TempRoot());

        session.RecordToolOutcome("invoke", AgentToolResult.Success(new JsonObject { ["invoked"] = true }));

        Assert.Null(session.LastObservation);
        Assert.Null(session.ActiveTarget);
    }

    [Fact]
    public void RecordToolOutcome_Failure_RecordsErrorNotObservation() {
        AgentSession session = AgentSession.Create(TempRoot());
        AgentToolResult failure = AgentToolResult.Failure(
            new AgentError("window-not-found", AgentErrorCategory.NoTarget, "no window"));

        session.RecordToolOutcome("query", failure);

        Assert.Null(session.LastObservation);
        Assert.Equal("no-target", session.LastError!["category"]!.GetValue<string>());
    }
}
