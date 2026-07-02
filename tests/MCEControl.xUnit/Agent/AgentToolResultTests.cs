// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Validates the #101 result envelope (<see cref="AgentToolResult"/>) and the Phase 1 (#86) translation
/// from the legacy <see cref="CommandResult"/>. There is no JSON-Schema validator package referenced, so
/// these tests assert the schema's structural invariants directly (see
/// <c>docs/design/agent-tool-result.schema.json</c>): <c>ok</c> required; <c>ok:false</c> ⟹ a complete
/// <c>error</c>; <c>ok:true</c> ⟹ no <c>error</c>; <c>category</c> from the closed taxonomy.
/// </summary>
public class AgentToolResultTests {
    private static readonly HashSet<string> _categoryEnum = [
        "timeout", "ambiguous-selector", "stale-element", "no-target", "invalid-argument",
        "capture-blank", "focus", "elevation", "foreground", "internal",
    ];

    /// <summary>Asserts an emitted envelope honors the schema's required-field and success/failure invariants.</summary>
    private static void AssertValidEnvelope(JsonObject env) {
        Assert.True(env["ok"]?.GetValueKind() is JsonValueKind.True or JsonValueKind.False, "envelope must carry a boolean 'ok'");
        bool ok = env["ok"]!.GetValue<bool>();

        if (ok) {
            Assert.False(env.ContainsKey("error"), "a success envelope must not carry 'error'");
        }
        else {
            JsonObject error = Assert.IsType<JsonObject>(env["error"]);
            Assert.False(string.IsNullOrEmpty(error["code"]?.GetValue<string>()), "error.code is required");
            Assert.False(string.IsNullOrEmpty(error["detail"]?.GetValue<string>()), "error.detail is required");
            string category = error["category"]!.GetValue<string>();
            Assert.Contains(category, _categoryEnum);
            Assert.False(env.ContainsKey("result"), "a failure envelope must not carry 'result'");
        }

        if (env["warnings"] is { } warnings) {
            JsonArray arr = Assert.IsType<JsonArray>(warnings);
            foreach (JsonNode? w in arr) {
                JsonObject wo = Assert.IsType<JsonObject>(w);
                Assert.False(string.IsNullOrEmpty(wo["code"]?.GetValue<string>()), "warning.code is required");
                Assert.False(string.IsNullOrEmpty(wo["detail"]?.GetValue<string>()), "warning.detail is required");
            }
        }
    }

    [Fact]
    public void Success_CarriesResult_OmitsError() {
        JsonObject env = AgentToolResult.Success(new JsonObject { ["found"] = true }).ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.True(env["result"]!["found"]!.GetValue<bool>());
        Assert.False(env.ContainsKey("error"));
    }

    [Fact]
    public void Success_WithNoPayload_StillValid() {
        JsonObject env = AgentToolResult.Success(null).ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
    }

    [Fact]
    public void Failure_CarriesErrorTriple_OmitsResult() {
        JsonObject env = AgentToolResult
            .Failure(new AgentError("wait-condition-timeout", AgentErrorCategory.Timeout, "timed out"))
            .ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("wait-condition-timeout", env["error"]!["code"]!.GetValue<string>());
        Assert.Equal("timeout", env["error"]!["category"]!.GetValue<string>());
        Assert.False(env.ContainsKey("result"));
    }

    [Fact]
    public void Failure_WithPartialResult_IncludesIt_DistinctFromLastObservation() {
        // #206: a blank capture's own (suspect) payload rides in error.partialResult; the image the
        // command paid to keep must not be discarded; while error.lastObservation stays the last
        // GOOD state from a prior call.
        JsonObject prior = new() { ["window"] = "About" };
        JsonObject partial = new() { ["base64"] = "iVBORw0K", ["encoding"] = "png" };
        JsonObject env = AgentToolResult
            .Failure(new AgentError("frame-all-black", AgentErrorCategory.CaptureBlank, "blank frame", prior, partial))
            .ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("About", env["error"]!["lastObservation"]!["window"]!.GetValue<string>());
        Assert.Equal("iVBORw0K", env["error"]!["partialResult"]!["base64"]!.GetValue<string>());
    }

    [Fact]
    public void Failure_WithLastObservation_IncludesIt() {
        JsonObject obs = new() { ["window"] = "About" };
        JsonObject env = AgentToolResult
            .Failure(new AgentError("element-not-found", AgentErrorCategory.NoTarget, "no element", obs))
            .ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("About", env["error"]!["lastObservation"]!["window"]!.GetValue<string>());
    }

    [Fact]
    public void SessionId_OmittedWhenNull_PresentWhenSet() {
        JsonObject anon = AgentToolResult.Success(null).ToJsonObject();
        Assert.False(anon.ContainsKey("sessionId"));

        JsonObject scoped = AgentToolResult.Success(null, sessionId: "s-1234").ToJsonObject();
        Assert.Equal("s-1234", scoped["sessionId"]!.GetValue<string>());
    }

    [Fact]
    public void Warnings_SerializeAsCodeDetailArray() {
        AgentToolResult result = AgentToolResult.Success(
            [],
            warnings: [new AgentWarning("minimized-window", "Target was minimized; restored before capture.")]);

        JsonObject env = result.ToJsonObject();
        AssertValidEnvelope(env);
        Assert.Equal("minimized-window", env["warnings"]![0]!["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(AgentErrorCategory.Timeout, "timeout")]
    [InlineData(AgentErrorCategory.AmbiguousSelector, "ambiguous-selector")]
    [InlineData(AgentErrorCategory.StaleElement, "stale-element")]
    [InlineData(AgentErrorCategory.NoTarget, "no-target")]
    [InlineData(AgentErrorCategory.InvalidArgument, "invalid-argument")]
    [InlineData(AgentErrorCategory.CaptureBlank, "capture-blank")]
    [InlineData(AgentErrorCategory.Focus, "focus")]
    [InlineData(AgentErrorCategory.Elevation, "elevation")]
    [InlineData(AgentErrorCategory.Foreground, "foreground")]
    [InlineData(AgentErrorCategory.Internal, "internal")]
    public void CategoryWire_MapsEveryCategoryToSchemaEnum(AgentErrorCategory category, string expected) {
        string wire = new AgentError("c", category, "d").CategoryWire;
        Assert.Equal(expected, wire);
        Assert.Contains(wire, _categoryEnum);
    }

    // #206: the prose-sniffing Categorize shim is GONE. The envelope is built from the CommandResult
    // OBJECT the command returned; the tests below pin codes/categories; never error-message text.

    [Fact]
    public void FromCommandResult_Success_MapsDataToResult_SameInstance() {
        // The object flows through: result IS the command's Data (no serialize → parse → clone chain,
        // which used to materialize a capture's base64 PNG three to four times).
        JsonObject data = new() { ["tree"] = "x" };
        CommandResult command = CommandResult.Ok("query", data);

        AgentToolResult env = AgentToolResult.FromCommandResult(command, "query");

        Assert.True(env.Ok);
        Assert.Same(data, env.Result);
        AssertValidEnvelope(env.ToJsonObject());
    }

    [Fact]
    public void FromCommandResult_Failure_CarriesStructuredCodeAndCategory() {
        CommandResult command = CommandResult
            .Fail("capture", "Captured frame is blank (a flat fill).", "frame-all-black", "capture-blank");

        JsonObject env = AgentToolResult.FromCommandResult(command, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("capture-blank", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("frame-all-black", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_Failure_KeptData_RidesInPartialResult() {
        // A blank capture deliberately keeps the (suspect) PNG in its failure Data; the envelope must
        // carry it in error.partialResult rather than discarding the image the command paid to keep.
        JsonObject kept = new() { ["base64"] = "iVBORw0K", ["encoding"] = "png" };
        CommandResult command = CommandResult
            .Fail("capture", "Captured frame is blank.", "frame-all-black", "capture-blank", kept);

        JsonObject env = AgentToolResult.FromCommandResult(command, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env.ContainsKey("result"));
        Assert.Equal("iVBORw0K", env["error"]!["partialResult"]!["base64"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_Failure_WithoutCode_FallsBackToUnhandledInternal() {
        // A bare-string Fail (which AgentCommand's template also normalizes) must still yield a
        // categorical envelope; never a prose-derived one.
        CommandResult command = CommandResult.Fail("capture", "something nobody coded");

        JsonObject env = AgentToolResult.FromCommandResult(command, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("unhandled", env["error"]!["code"]!.GetValue<string>());
        Assert.Equal("internal", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("something nobody coded", env["error"]!["detail"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_Failure_UnknownCategory_FallsBackToInternal_KeepsCode() {
        // If a command ever emits a category outside the closed set, don't propagate it;
        // error.category must always validate against the schema enum. The (open-set) code survives.
        CommandResult command = CommandResult.Fail("capture", "No matching window", "weird-code", "not-a-category");

        JsonObject env = AgentToolResult.FromCommandResult(command, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("internal", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("weird-code", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_CarriesWarnings_OnSuccessAndFailure() {
        CommandResult okCommand = CommandResult.Ok("query", new JsonObject { ["truncated"] = true })
            .Warn("tree-truncated", "hit the node cap");
        JsonObject okEnv = AgentToolResult.FromCommandResult(okCommand, "query").ToJsonObject();
        AssertValidEnvelope(okEnv);
        Assert.Equal("tree-truncated", okEnv["warnings"]![0]!["code"]!.GetValue<string>());

        CommandResult failCommand = CommandResult
            .Fail("capture", "Captured frame is blank.", "frame-uniform", "capture-blank")
            .Warn("capture-fallback", "PrintWindow refused; used an on-screen blit");
        JsonObject failEnv = AgentToolResult.FromCommandResult(failCommand, "capture").ToJsonObject();
        AssertValidEnvelope(failEnv);
        Assert.Equal("capture-fallback", failEnv["warnings"]![0]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_WaitForMiss_BecomesTimeoutFailure() {
        // FindCommand returns Ok{found:false} when a wait-for exhausts its timeout. An agent branches on
        // `ok`, so that must surface as a timeout failure, not ok:true (Codex P2 on #115).
        CommandResult command = CommandResult.Ok("wait-for", new JsonObject { ["found"] = false, ["element"] = null });

        JsonObject env = AgentToolResult.FromCommandResult(command, "wait-for").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("timeout", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("wait-condition-timeout", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromCommandResult_WaitForHit_StaysSuccess() {
        CommandResult command = CommandResult.Ok("wait-for",
            new JsonObject { ["found"] = true, ["element"] = new JsonObject { ["name"] = "OK" } });

        JsonObject env = AgentToolResult.FromCommandResult(command, "wait-for").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.True(env["result"]!["found"]!.GetValue<bool>());
    }

    [Fact]
    public void FromCommandResult_FindMiss_StaysSuccess() {
        // A one-shot `find` miss is not an error ("a miss is not an error"); only a timed-out wait-for is.
        CommandResult command = CommandResult.Ok("find", new JsonObject { ["found"] = false });

        JsonObject env = AgentToolResult.FromCommandResult(command, "find").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.False(env["result"]!["found"]!.GetValue<bool>());
    }

    [Fact]
    public void FromCommandResult_Failure_AttachesSessionIdAndPriorObservation() {
        CommandResult command = CommandResult.Fail("invoke", "No matching window", "window-not-found", "no-target");
        JsonObject prior = new() { ["window"] = "About" };

        JsonObject env = AgentToolResult.FromCommandResult(command, "invoke", "s-1234", prior).ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("s-1234", env["sessionId"]!.GetValue<string>());
        Assert.Equal("About", env["error"]!["lastObservation"]!["window"]!.GetValue<string>());
    }

    [Fact]
    public void Envelope_IsCamelCaseAndOmitsNulls() {
        string json = AgentToolResult.Success(new JsonObject { ["found"] = false }).ToJson();

        Assert.Contains("\"ok\":true", json);
        Assert.DoesNotContain("sessionId", json); // null -> omitted
        Assert.DoesNotContain("\"error\"", json);
    }
}
