// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

public class UiaServiceTests {
    [Theory]
    [InlineData("invoke")]
    [InlineData("Invoke")]   // case-insensitive
    [InlineData("toggle")]
    [InlineData("setvalue")]
    [InlineData("setfocus")]
    public void IsSupportedAction_KnownActions_ReturnsTrue(string action) {
        Assert.True(UiaService.IsSupportedAction(action));
    }

    [Theory]
    [InlineData("click")]       // CR P2: a coordinate-click typo must not fall through to Invoke
    [InlineData("set-value")]   // typo of setvalue
    [InlineData("")]
    [InlineData("foo")]
    public void IsSupportedAction_UnknownActions_ReturnsFalse(string action) {
        Assert.False(UiaService.IsSupportedAction(action));
    }

    [Fact]
    public void DumpTree_ZeroHandle_ReturnsEmptyUntruncatedResult() {
        // The guard path must return a well-formed result (null root, no nodes, not truncated) rather
        // than null, so query can always read nodeCount/truncated.
        UiaTreeResult result = UiaService.DumpTree(IntPtr.Zero, maxDepth: 6, maxNodes: 1000);

        Assert.NotNull(result);
        Assert.Null(result.Root);
        Assert.Equal(0, result.NodeCount);
        Assert.False(result.Truncated);
    }
}
