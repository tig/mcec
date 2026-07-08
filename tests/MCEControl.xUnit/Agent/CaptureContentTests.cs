// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Tests for <see cref="CaptureContent"/>: image-content block shaping and path-only behavior.
/// </summary>
public class CaptureContentTests {
    [Fact]
    public void TryBuildImageBlock_BuildsImageBlock_AndLeavesBase64InResult() {
        JsonObject result = new() { ["width"] = 10, ["base64"] = "QUJD" };

        JsonObject? block = CaptureContent.TryBuildImageBlock(result);

        Assert.NotNull(block);
        Assert.Equal("image", block["type"]!.GetValue<string>());
        Assert.Equal("QUJD", block["data"]!.GetValue<string>());
        Assert.Equal("image/png", block["mimeType"]!.GetValue<string>());

        // The bytes remain reachable from the result for text-only agents.
        Assert.Equal("QUJD", result["base64"]!.GetValue<string>());
    }

    [Fact]
    public void TryBuildImageBlock_ReturnsNull_WhenNoBase64() {
        Assert.Null(CaptureContent.TryBuildImageBlock(new JsonObject { ["width"] = 10 }));
        Assert.Null(CaptureContent.TryBuildImageBlock(null));
    }

    [Fact]
    public void WantsInlineImage_ReturnsFalse_ForPathOnlyOrReturnImageFalse() {
        Assert.False(CaptureContent.WantsInlineImage(new JsonObject { ["pathOnly"] = true }));
        Assert.False(CaptureContent.WantsInlineImage(new JsonObject { ["returnImage"] = false }));
        Assert.True(CaptureContent.WantsInlineImage([]));
    }

    [Fact]
    public void ApplyPathOnlyMode_WritesArtifact_AndStripsBase64() {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mcec-capture-content-tests", Path.GetRandomFileName());
        AgentSession session = AgentSession.Create(tempRoot);
        JsonObject result = new() {
            ["encoding"] = "png",
            ["base64"] = "QUJD",
            ["width"] = 1,
            ["height"] = 1,
        };

        CaptureContent.ApplyPathOnlyMode(result, new JsonObject { ["pathOnly"] = true }, session);

        Assert.False(result.ContainsKey("base64"));
        string artifact = result["artifact"]!.GetValue<string>();
        Assert.True(File.Exists(artifact));
        Assert.Equal("ABC", File.ReadAllText(artifact));

        Directory.Delete(session.ArtifactDir, true);
    }

    [Theory]
    [InlineData("maxWidth", 0)]
    [InlineData("maxWidth", -1)]
    public void ValidateArgs_RejectsNonPositiveMaxWidth(string key, int value) {
        Assert.NotNull(CaptureContent.ValidateArgs(new JsonObject { [key] = value }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ValidateArgs_RejectsOutOfRangeScale(double value) {
        Assert.NotNull(CaptureContent.ValidateArgs(new JsonObject { ["scale"] = value }));
    }
}
