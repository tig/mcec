// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class DragCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        DragCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsDrag() {
        List<Command> builtIns = DragCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "drag");
    }

    [Fact]
    public void Clone_CopiesEndpoints_AndIsIndependent() {
        DragCommand original = new() {
            Cmd = "drag",
            Enabled = true,
            FromValue = "Slider",
            ToX = 300,
            ToY = 120,
            PathSpec = "10,10;20,20",
        };

        DragCommand clone = (DragCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("Slider", clone.FromValue);
        Assert.Equal(300, clone.ToX);
        Assert.Equal(120, clone.ToY);
        Assert.Equal("10,10;20,20", clone.PathSpec);

        clone.FromValue = "changed";
        Assert.Equal("Slider", original.FromValue);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            DragCommand cmd = new() { Cmd = "drag", Enabled = true, Reply = reply, FromX = 0, FromY = 0, ToX = 10, ToY = 10 };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    // #262 review (Copilot): a two-endpoint drag must name WHICH end failed in the detail for EVERY
    // failure category, not only the not-found path. Before the fix, ambiguous/stale/elevation
    // failures came straight from the shared UiaFindFailureFor mapper with no "Drag start"/"Drag end"
    // prefix, contradicting TryResolvePoint's doc comment. LabelEndpoint is the shared prefixing step.

    [Theory]
    [InlineData("Drag start")]
    [InlineData("Drag end")]
    public void LabelEndpoint_PrefixesDetail_WhilePreservingCodeAndCategory(string endpoint) {
        CommandResult raw = CommandResult.Fail(
            "drag", "The selector (name='OK') matched 3 elements; refusing to guess which one.",
            "selector-matched-3", "ambiguous-selector");

        CommandResult labeled = DragCommand.LabelEndpoint(endpoint, raw);

        Assert.False(labeled.Success);
        Assert.StartsWith(endpoint + ":", labeled.Error);
        Assert.Contains("matched 3 elements", labeled.Error);      // original detail retained
        Assert.Equal("selector-matched-3", labeled.ErrorCode);     // code/category untouched
        Assert.Equal("ambiguous-selector", labeled.ErrorCategory);
    }

    [Theory]
    // Every classified endpoint failure keeps its taxonomy AND gains the endpoint label.
    [InlineData("window-closed", "stale-element")]
    [InlineData("target-elevated", "elevation")]
    [InlineData("element-not-found", "no-target")]
    public void LabelEndpoint_AppliesToEveryCategory(string code, string category) {
        CommandResult raw = CommandResult.Fail("drag", "detail text", code, category);

        CommandResult labeled = DragCommand.LabelEndpoint("Drag end", raw);

        Assert.StartsWith("Drag end:", labeled.Error);
        Assert.Equal(code, labeled.ErrorCode);
        Assert.Equal(category, labeled.ErrorCategory);
    }
}
