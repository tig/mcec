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
    private static readonly HashSet<string> CategoryEnum = [
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
            Assert.Contains(category, CategoryEnum);
            Assert.False(env.ContainsKey("result"), "a failure envelope must not carry 'result'");
        }

        if (env["warnings"] is JsonNode warnings) {
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
        // #206: a blank capture's own (suspect) payload rides in error.partialResult — the image the
        // command paid to keep must not be discarded — while error.lastObservation stays the last
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
        Assert.Contains(wire, CategoryEnum);
    }

    [Theory]
    [InlineData("No matching window", "no-target", "window-not-found")]
    [InlineData("Invoke failed (element not found or pattern unsupported)", "no-target", "element-not-found")]
    [InlineData("Capture failed: boom", "internal", "capture-exception")]
    [InlineData("Agent commands are disabled (AgentCommandsEnabled=false).", "internal", "agent-commands-disabled")]
    [InlineData("command produced no output", "internal", "no-output")]
    [InlineData("something nobody mapped", "internal", "unhandled")]
    public void Categorize_MapsKnownErrorStrings(string message, string category, string code) {
        AgentError error = AgentToolResult.Categorize("invoke", message);

        Assert.Equal(category, error.CategoryWire);
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Detail);
    }

    [Fact]
    public void FromLegacy_Success_MapsDataToResult() {
        JsonObject legacy = CommandResult.Ok("query", new JsonObject { ["tree"] = "x" }).ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "query").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.Equal("x", env["result"]!["tree"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_Failure_MapsErrorViaTaxonomy() {
        JsonObject legacy = CommandResult.Fail("capture", "No matching window").ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("no-target", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("window-not-found", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_Failure_PrefersStructuredCategoryAndCode() {
        // A blank window capture writes a structured Fail (code + category), not a bare string. The
        // translator must preserve those so an agent takes the documented capture-blank recovery path
        // rather than seeing internal/unhandled (Codex P2 on #171).
        JsonObject legacy = CommandResult
            .Fail("capture", "Captured frame is blank (a flat fill).", "frame-all-black", "capture-blank")
            .ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("capture-blank", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("frame-all-black", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_Failure_UnknownStructuredCategory_FallsBackToTaxonomy() {
        // If a command ever emits a category outside the closed set, don't propagate it — fall back to
        // free-text categorization so error.category always validates against the schema enum.
        JsonObject legacy = CommandResult
            .Fail("capture", "No matching window", "weird-code", "not-a-category")
            .ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "capture").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.Equal("no-target", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("window-not-found", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_CarriesWarnings_OnSuccessAndFailure() {
        JsonObject okLegacy = CommandResult.Ok("query", new JsonObject { ["truncated"] = true })
            .Warn("tree-truncated", "hit the node cap").ToJsonObject();
        JsonObject okEnv = AgentToolResult.FromLegacy(okLegacy, "query").ToJsonObject();
        AssertValidEnvelope(okEnv);
        Assert.Equal("tree-truncated", okEnv["warnings"]![0]!["code"]!.GetValue<string>());

        JsonObject failLegacy = CommandResult
            .Fail("capture", "Captured frame is blank.", "frame-uniform", "capture-blank")
            .Warn("capture-fallback", "PrintWindow refused; used an on-screen blit").ToJsonObject();
        JsonObject failEnv = AgentToolResult.FromLegacy(failLegacy, "capture").ToJsonObject();
        AssertValidEnvelope(failEnv);
        Assert.Equal("capture-fallback", failEnv["warnings"]![0]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_WaitForMiss_BecomesTimeoutFailure() {
        // FindCommand writes Ok{found:false} when a wait-for exhausts its timeout. An agent branches on
        // `ok`, so that must surface as a timeout failure, not ok:true (Codex P2 on #115).
        JsonObject legacy = CommandResult.Ok("wait-for", new JsonObject { ["found"] = false, ["element"] = null }).ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "wait-for").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.False(env["ok"]!.GetValue<bool>());
        Assert.Equal("timeout", env["error"]!["category"]!.GetValue<string>());
        Assert.Equal("wait-condition-timeout", env["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void FromLegacy_WaitForHit_StaysSuccess() {
        JsonObject legacy = CommandResult.Ok("wait-for",
            new JsonObject { ["found"] = true, ["element"] = new JsonObject { ["name"] = "OK" } }).ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "wait-for").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.True(env["result"]!["found"]!.GetValue<bool>());
    }

    [Fact]
    public void FromLegacy_FindMiss_StaysSuccess() {
        // A one-shot `find` miss is not an error ("a miss is not an error"); only a timed-out wait-for is.
        JsonObject legacy = CommandResult.Ok("find", new JsonObject { ["found"] = false }).ToJsonObject();

        JsonObject env = AgentToolResult.FromLegacy(legacy, "find").ToJsonObject();

        AssertValidEnvelope(env);
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.False(env["result"]!["found"]!.GetValue<bool>());
    }

    [Fact]
    public void FromLegacy_Failure_AttachesSessionIdAndPriorObservation() {
        JsonObject legacy = CommandResult.Fail("invoke", "No matching window").ToJsonObject();
        JsonObject prior = new() { ["window"] = "About" };

        JsonObject env = AgentToolResult.FromLegacy(legacy, "invoke", "s-1234", prior).ToJsonObject();

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
