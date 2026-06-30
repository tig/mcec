// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Serialization of the structured result fields added for observation hardening (#90): warnings and
/// the taxonomy-aligned error code/category, kept additive over the legacy success/error/data shape.
/// </summary>
public class CommandResultTests {
    [Fact]
    public void Ok_NoWarnings_OmitsWarningsArray() {
        JsonObject json = CommandResult.Ok("query", new JsonObject { ["x"] = 1 }).ToJsonObject();

        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Null(json["warnings"]);
        Assert.Null(json["errorCode"]);
    }

    [Fact]
    public void Warn_OnSuccess_EmitsWarningsArray() {
        CommandResult res = CommandResult.Ok("capture", [])
            .Warn("capture-fallback", "used blit");

        JsonObject json = res.ToJsonObject();

        Assert.True(json["success"]!.GetValue<bool>());
        JsonArray warnings = json["warnings"]!.AsArray();
        Assert.Single(warnings);
        Assert.Equal("capture-fallback", warnings[0]!["code"]!.GetValue<string>());
        Assert.Equal("used blit", warnings[0]!["detail"]!.GetValue<string>());
    }

    [Fact]
    public void StructuredFail_EmitsCodeCategoryAndCarriesData() {
        JsonObject data = new() { ["base64"] = "AAAA" };

        JsonObject json = CommandResult
            .Fail("capture", "Captured frame is blank", "frame-all-black", "capture-blank", data)
            .ToJsonObject();

        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Equal("Captured frame is blank", json["error"]!.GetValue<string>());
        Assert.Equal("frame-all-black", json["errorCode"]!.GetValue<string>());
        Assert.Equal("capture-blank", json["errorCategory"]!.GetValue<string>());
        // The suspect image is preserved (contract's lastObservation), not dropped on failure.
        Assert.Equal("AAAA", json["data"]!["base64"]!.GetValue<string>());
    }

    [Fact]
    public void LegacyFail_OmitsStructuredErrorFields() {
        JsonObject json = CommandResult.Fail("query", "No matching window").ToJsonObject();

        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Equal("No matching window", json["error"]!.GetValue<string>());
        Assert.Null(json["errorCode"]);
        Assert.Null(json["errorCategory"]);
    }
}
