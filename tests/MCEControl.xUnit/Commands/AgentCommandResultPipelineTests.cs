// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Pins the #206 object pipeline in <see cref="AgentCommand"/>'s sealed Execute template: the
/// <see cref="CommandResult"/> flows to <see cref="CapturingReply.Result"/> as the SAME instance (no
/// serialize → re-parse), the legacy TCP/serial transport still receives the JSON line, and every
/// failure is normalized onto the closed taxonomy (no free-text-only failures left to prose-sniff).
/// </summary>
[Collection("AgentSerial")]
public class AgentCommandResultPipelineTests {
    [Fact]
    public void Execute_HandsTheResultObjectToCapturingReply_SameInstance() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CommandResult produced = CommandResult.Ok("stub", new JsonObject { ["x"] = 1 });
            CapturingReply reply = new();
            StubResultAgentCommand cmd = new() { Cmd = "stub", Enabled = true, Reply = reply, Producer = () => produced };

            bool ok = cmd.Execute();

            Assert.True(ok);
            // Identity, not equality: the object the command built IS what the MCP server consumes —
            // the seam that proves no intermediate serialize/parse copies exist on this path.
            Assert.Same(produced, reply.Result);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WritesTheJsonLine_ToALegacyReply() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            RecordingReply reply = new();
            StubResultAgentCommand cmd = new() {
                Cmd = "stub", Enabled = true, Reply = reply,
                Producer = () => CommandResult.Ok("stub", new JsonObject { ["x"] = 1 }),
            };

            cmd.Execute();

            // The legacy TCP/serial transport still gets the same single JSON line as before #206.
            Assert.EndsWith(Environment.NewLine, reply.Text, StringComparison.Ordinal);
            JsonObject json = JsonNode.Parse(reply.Text.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>());
            Assert.Equal(1, json["data"]!["x"]!.GetValue<int>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_NormalizesABareStringFailure_OntoTheClosedTaxonomy() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            StubResultAgentCommand cmd = new() {
                Cmd = "stub", Enabled = true, Reply = reply,
                Producer = () => CommandResult.Fail("stub", "bare prose only"),
            };

            bool ok = cmd.Execute();

            // Structural guarantee: no agent failure leaves the template without a code/category.
            Assert.False(ok);
            Assert.Equal("unhandled", reply.Result!.ErrorCode);
            Assert.Equal("internal", reply.Result.ErrorCategory);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WhenAgentDisabled_FailureIsCategorical() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            StubResultAgentCommand cmd = new() {
                Cmd = "stub", Enabled = true, Reply = reply,
                Producer = () => throw new InvalidOperationException("gate must block before the body"),
            };

            bool ok = cmd.Execute();

            Assert.False(ok);
            Assert.Equal("agent-commands-disabled", reply.Result!.ErrorCode);
            Assert.Equal("internal", reply.Result.ErrorCategory);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Find_WindowNotFound_CarriesStructuredCode() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            // A selector that cannot match anything on any desktop: find used to fail with the bare
            // string "No matching window" (prose the deleted Categorize shim had to sniff).
            CapturingReply reply = new();
            FindCommand cmd = new() {
                Cmd = "find", Enabled = true, Reply = reply,
                Window = $"no-such-window-{Guid.NewGuid():N}", Value = "OK",
            };

            bool ok = cmd.Execute();

            Assert.False(ok);
            Assert.Equal("window-not-found", reply.Result!.ErrorCode);
            Assert.Equal("no-target", reply.Result.ErrorCategory);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Drag_ElementEndpointWithNoMatchingWindow_CarriesStructuredCode() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            // An element endpoint forces window resolution; a selector that matches nothing used to
            // produce the bare "No matching window" string. No input is ever synthesized.
            CapturingReply reply = new();
            DragCommand cmd = new() {
                Cmd = "drag", Enabled = true, Reply = reply,
                Window = $"no-such-window-{Guid.NewGuid():N}",
                FromValue = "Slider", ToX = 10, ToY = 10,
            };

            bool ok = cmd.Execute();

            Assert.False(ok);
            Assert.Equal("window-not-found", reply.Result!.ErrorCode);
            Assert.Equal("no-target", reply.Result.ErrorCategory);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }
}
