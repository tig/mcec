// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 tersifier: condensed, log-view-style one-liners for the overlay.</summary>
public class CommandTersifierTests {
    [Fact]
    public void Capture_ShowsTarget() {
        string s = CommandTersifier.ForAgentTool("capture", new JsonObject { ["window"] = "About" }, CommandOutcome.Ok);
        Assert.Equal("capture window=\"About\"", s);
    }

    [Fact]
    public void Query_FallsBackToProcessWhenNoWindow() {
        string s = CommandTersifier.ForAgentTool("query", new JsonObject { ["process"] = "notepad" }, CommandOutcome.Ok);
        Assert.Equal("query process=\"notepad\"", s);
    }

    [Fact]
    public void Target_PrefersHandleOverStaleWindowProcess_MatchingResolverPrecedence() {
        // WindowResolver targets handle > foreground > filters, so when an agent reuses a handle the
        // overlay must show the handle — not a stale window/process filter that didn't decide the target.
        JsonObject args = new() { ["handle"] = 0x1234L, ["window"] = "Stale", ["process"] = "old" };
        string s = CommandTersifier.ForAgentTool("capture", args, CommandOutcome.Ok);
        Assert.Equal("capture handle=0x1234", s);
    }

    [Fact]
    public void Target_PrefersForegroundOverFilters() {
        JsonObject args = new() { ["foreground"] = true, ["process"] = "old" };
        string s = CommandTersifier.ForAgentTool("capture", args, CommandOutcome.Ok);
        Assert.Equal("capture foreground", s);
    }

    [Fact]
    public void Invoke_ShowsActionAndValue() {
        JsonObject args = new() { ["action"] = "expand", ["value"] = "Help", ["by"] = "name" };
        string s = CommandTersifier.ForAgentTool("invoke", args, CommandOutcome.Ok);
        Assert.Equal("invoke expand \"Help\"", s);
    }

    [Fact]
    public void WaitFor_Miss_AppendsFailureDetail() {
        JsonObject args = new() { ["value"] = "OK", ["by"] = "name" };
        string s = CommandTersifier.ForAgentTool("wait-for", args, CommandOutcome.Failed, "timeout");
        Assert.Equal("wait-for \"OK\" → timeout", s);
    }

    [Fact]
    public void Find_NonNameSelector_ShowsByEqualsValue() {
        JsonObject args = new() { ["value"] = "saveBtn", ["by"] = "automationId" };
        string s = CommandTersifier.ForAgentTool("find", args, CommandOutcome.Ok);
        Assert.Equal("find automationId=\"saveBtn\"", s);
    }

    [Fact]
    public void Invoke_Pending_AppendsEllipsis() {
        JsonObject args = new() { ["value"] = "About", ["action"] = "invoke" };
        string s = CommandTersifier.ForAgentTool("invoke", args, CommandOutcome.Pending);
        Assert.Equal("invoke invoke \"About\" …", s);
    }

    [Fact]
    public void RawCommand_PrefixesSend_AndClipsLongCommands() {
        Assert.Equal("send winr", CommandTersifier.ForRawCommand("winr"));
        Assert.StartsWith("send ", CommandTersifier.ForRawCommand(new string('x', 80)));
        Assert.EndsWith("…", CommandTersifier.ForRawCommand(new string('x', 80)));
    }
}
