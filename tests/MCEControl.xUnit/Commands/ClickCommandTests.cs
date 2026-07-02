// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class ClickCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        ClickCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsClick() {
        List<Command> builtIns = ClickCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "click");
    }

    [Fact]
    public void Defaults_ButtonLeft_CountOne() {
        ClickCommand cmd = new();

        Assert.Equal("left", cmd.Button);
        Assert.Equal(1, cmd.Count);
    }

    [Fact]
    public void Clone_CopiesEndpointAndButton_AndIsIndependent() {
        ClickCommand original = new() {
            Cmd = "click",
            Enabled = true,
            Value = "OK",
            X = 300,
            Y = 120,
            Button = "right",
            Count = 2,
        };

        ClickCommand clone = (ClickCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("OK", clone.Value);
        Assert.Equal(300, clone.X);
        Assert.Equal(120, clone.Y);
        Assert.Equal("right", clone.Button);
        Assert.Equal(2, clone.Count);

        clone.Value = "changed";
        Assert.Equal("OK", original.Value);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        // Disabled path never actuates (no real mouse input in tests); it must fail closed.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            ClickCommand cmd = new() { Cmd = "click", Enabled = true, Reply = reply, X = 10, Y = 10 };

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
