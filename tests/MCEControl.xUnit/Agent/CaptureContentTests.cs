// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Tests for <see cref="CaptureContent"/>: building the MCP image content block for a capture result
/// WITHOUT stripping the base64 bytes from the envelope's <c>result</c>. The contract says the PNG is
/// "additionally" emitted as an image block while "the same image is referenced from result for
/// text-only agents"; so clients that parse the envelope text but ignore MCP image blocks must still
/// get the bytes (Codex P2 on #115).
/// </summary>
public class CaptureContentTests {
    [Fact]
    public void TryBuildImageBlock_BuildsImageBlock_AndLeavesBase64InResult() {
        JsonObject result = new() { ["width"] = 10, ["base64"] = "QUJD" };

        JsonObject? block = CaptureContent.TryBuildImageBlock(result);

        Assert.NotNull(block);
        Assert.Equal("image", block!["type"]!.GetValue<string>());
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
}
