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
        InvokeCommand original = new() {
            Cmd = "invoke",
            Args = "name=OK",
            Enabled = true,
            By = "name",
            Value = "General",
            Action = "select",
            Text = ""
        };

        InvokeCommand clone = (InvokeCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("invoke", clone.Cmd);
        Assert.Equal("name=OK", clone.Args);
        Assert.True(clone.Enabled);
        Assert.Equal("select", clone.Action);
        Assert.Equal("General", clone.Value);

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

    [Theory]
    // #206: each UiaInvokeResult failure maps to a DISTINCT code/category so agent recovery differs:
    // no-target => wait/re-find; invalid-argument => fix the call (re-finding cannot help); internal
    // => report. The old bare bool collapsed all four into one prose string.
    [InlineData(UiaInvokeResult.ElementNotFound, "element-not-found", "no-target")]
    [InlineData(UiaInvokeResult.PatternUnsupported, "pattern-unsupported", "invalid-argument")]
    [InlineData(UiaInvokeResult.ActionUnknown, "action-unknown", "invalid-argument")]
    [InlineData(UiaInvokeResult.Faulted, "invoke-faulted", "internal")]
    public void FailureFor_MapsEachOutcomeToADistinctCodeAndCategory(UiaInvokeResult outcome, string code, string category) {
        InvokeCommand cmd = new() { Cmd = "invoke", By = "name", Value = "OK", Action = "toggle" };

        CommandResult result = cmd.FailureFor(outcome);

        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(category, result.ErrorCategory);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void IsSupportedAction_IncludesSelect() {
        Assert.True(UiaService.IsSupportedAction("select"));
        Assert.True(UiaService.IsSupportedAction("SELECT"));
        Assert.False(UiaService.IsSupportedAction("click"));
        Assert.False(UiaService.IsSupportedAction(""));
    }
}
