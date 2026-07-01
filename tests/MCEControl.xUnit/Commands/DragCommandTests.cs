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
}
