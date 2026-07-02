// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Pins the #206 typed-result handoff: an agent command hands its <see cref="CommandResult"/> to
/// <see cref="CapturingReply.Result"/> as an OBJECT; <see cref="CapturingReply.Captured"/> serializes
/// it lazily only when legacy text is actually wanted (send_command output, tests), so the observable
/// wire text is unchanged while the MCP path never re-parses its own output.
/// </summary>
public class CapturingReplyTests {
    [Fact]
    public void Captured_Empty_WhenNothingWrittenAndNoResult() {
        CapturingReply reply = new();

        Assert.Equal("", reply.Captured);
        Assert.Null(reply.Result);
    }

    [Fact]
    public void Captured_ReturnsBufferedText_ForLegacyWrites() {
        CapturingReply reply = new();
        reply.WriteLine("plain text output");

        Assert.Equal("plain text output", reply.Captured.Trim());
    }

    [Fact]
    public void Captured_LazilySerializesTypedResult_WhenNothingWasWritten() {
        CapturingReply reply = new() {
            Result = CommandResult.Ok("displays", new JsonObject { ["count"] = 2 }),
        };

        JsonObject json = JsonNode.Parse(reply.Captured)!.AsObject();
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal(2, json["data"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public void Captured_BufferedText_TakesPrecedenceOverTypedResult() {
        // A command that wrote text AND set a typed result (not a shape agent commands produce, but
        // the contract must be deterministic): the buffered text wins; Result is the typed side-channel.
        CapturingReply reply = new() { Result = CommandResult.Ok("x") };
        reply.Write("buffered");

        Assert.Equal("buffered", reply.Captured);
    }
}
