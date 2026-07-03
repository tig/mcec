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

    // --- #215: the observation payload bomb. A capture observation carries the full base64 PNG;
    // session state must keep only a compact summary + an artifact file path, so a later failure's
    // error.lastObservation never embeds megabytes of stale screenshot.

    private static JsonObject CaptureObservation(out byte[] pngBytes) {
        pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4]; // PNG magic + filler
        return new JsonObject {
            ["handle"] = 42,
            ["width"] = 800,
            ["height"] = 600,
            ["encoding"] = "png",
            ["bytes"] = pngBytes.Length,
            ["base64"] = Convert.ToBase64String(pngBytes),
            ["window"] = new JsonObject { ["handle"] = 42, ["title"] = "App" },
            ["blankCheck"] = new JsonObject { ["blank"] = false, ["dominantFraction"] = 0.1 },
        };
    }

    [Fact]
    public void RecordObservation_CaptureWithBase64_StoresSummaryPlusArtifact_NeverTheBytes() {
        string root = TempRoot();
        AgentSession session = AgentSession.Create(root);
        try {
            session.RecordObservation(CaptureObservation(out byte[] pngBytes));

            JsonObject stored = session.LastObservation!;
            Assert.False(stored.ContainsKey("base64"), "session state must not retain the raw image bytes");
            Assert.Equal("capture-summary", stored["kind"]!.GetValue<string>());
            // The compact summary: window descriptor, dimensions, blankCheck verdict, byte count.
            Assert.Equal(800, stored["width"]!.GetValue<int>());
            Assert.Equal(600, stored["height"]!.GetValue<int>());
            Assert.Equal(pngBytes.Length, stored["bytes"]!.GetValue<int>());
            Assert.Equal("App", stored["window"]!["title"]!.GetValue<string>());
            Assert.False(stored["blankCheck"]!["blank"]!.GetValue<bool>());
            // ...plus the artifact path, holding the actual PNG under the session's artifact dir.
            string artifact = stored["artifact"]!.GetValue<string>();
            Assert.StartsWith(session.ArtifactDir, artifact);
            Assert.Equal(pngBytes, File.ReadAllBytes(artifact));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordObservation_CaptureSummary_FlowsIntoErrorLastObservation_WithoutBase64() {
        // The end-to-end point of the compaction: a failure AFTER a capture carries the summary (not
        // the screenshot) in error.lastObservation, exactly as the executor builds it; it snapshots
        // session.LastObservation into the AgentError.
        string root = TempRoot();
        AgentSession session = AgentSession.Create(root);
        try {
            session.RecordToolOutcome("capture", AgentToolResult.Success(CaptureObservation(out _)));

            AgentToolResult failure = AgentToolResult.Failure(
                new AgentError("element-not-found", AgentErrorCategory.NoTarget, "gone", session.LastObservation));
            JsonObject error = failure.ToJsonObject()["error"]!.AsObject();

            JsonObject last = error["lastObservation"]!.AsObject();
            Assert.False(last.ContainsKey("base64"), "error.lastObservation must never carry raw base64");
            Assert.True(last.ContainsKey("artifact"));
            Assert.DoesNotContain("base64", failure.ToJson(), StringComparison.Ordinal);
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordObservation_NonImagePayload_IsStoredUnchanged() {
        // Query trees / find results have no inline image bytes; they keep the historical behavior.
        AgentSession session = AgentSession.Create(TempRoot());
        session.RecordObservation(new JsonObject { ["tree"] = "x", ["nodeCount"] = 3 });

        JsonObject stored = session.LastObservation!;
        Assert.Equal("x", stored["tree"]!.GetValue<string>());
        Assert.False(stored.ContainsKey("kind"));
        Assert.False(Directory.Exists(session.ArtifactDir), "no artifact should be written for a non-image observation");
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
