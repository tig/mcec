// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class InvokeCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        InvokeCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsInvoke() {
        List<Command> builtIns = InvokeCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "invoke");
    }

    [Fact]
    public void Clone_CopiesBaseProperties_AndIsIndependent() {
        InvokeCommand original = new() { Cmd = "invoke", Args = "name=OK", Enabled = true };

        InvokeCommand clone = (InvokeCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("invoke", clone.Cmd);
        Assert.Equal("name=OK", clone.Args);
        Assert.True(clone.Enabled);

        clone.Cmd = "changed";
        Assert.Equal("invoke", original.Cmd);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            InvokeCommand cmd = new() { Cmd = "invoke", Enabled = true, Reply = reply };

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
