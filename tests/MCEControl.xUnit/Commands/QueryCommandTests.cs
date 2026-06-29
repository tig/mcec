// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class QueryCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        QueryCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsQuery() {
        List<Command> builtIns = QueryCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "query");
    }

    [Fact]
    public void Clone_CopiesBaseProperties_AndIsIndependent() {
        QueryCommand original = new() { Cmd = "query", Args = "title=Notepad", Enabled = true };

        QueryCommand clone = (QueryCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("query", clone.Cmd);
        Assert.Equal("title=Notepad", clone.Args);
        Assert.True(clone.Enabled);

        clone.Cmd = "changed";
        Assert.Equal("query", original.Cmd);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            QueryCommand cmd = new() { Cmd = "query", Enabled = true, Reply = reply };

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
