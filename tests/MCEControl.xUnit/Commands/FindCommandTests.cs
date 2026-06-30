// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class FindCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        FindCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsFindAndWaitFor() {
        List<Command> builtIns = FindCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "find");
        Assert.Contains(builtIns, c => c.Cmd == "wait-for");
    }

    [Fact]
    public void Clone_CopiesBaseProperties_AndIsIndependent() {
        FindCommand original = new() { Cmd = "find", Args = "title=Notepad", Enabled = true };

        FindCommand clone = (FindCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("find", clone.Cmd);
        Assert.Equal("title=Notepad", clone.Args);
        Assert.True(clone.Enabled);

        clone.Cmd = "changed";
        Assert.Equal("find", original.Cmd);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            FindCommand cmd = new() { Cmd = "find", Enabled = true, Reply = reply };

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
