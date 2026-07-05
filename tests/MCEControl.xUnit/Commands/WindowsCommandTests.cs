// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Tests the #77 top-level window discovery command (<c>windows</c>): the disabled-by-default security
/// posture, the list/filter shape, and the no-criteria-wait safety refusal. The listing itself is
/// exercised headless-safely (a CI agent may have zero visible windows), so the enabled test asserts the
/// envelope SHAPE, not a specific window count.
/// </summary>
[Collection("AgentSerial")]
public class WindowsCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        WindowsCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsWindows() {
        List<Command> builtIns = WindowsCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "windows");
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, Reply = reply };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WhenEnabled_ListsTopLevelWindows_WithCountMatchingArray() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, Reply = reply };

            bool result = cmd.Execute();

            Assert.True(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>());

            JsonObject data = json["data"]!.AsObject();
            JsonArray windows = data["windows"]!.AsArray();
            // Count is authoritative and must match the array length (headless-safe: may be 0).
            Assert.Equal(data["count"]!.GetValue<int>(), windows.Count);

            // Any window that IS present carries the full structured descriptor (#77 acceptance criteria).
            foreach (JsonNode? node in windows) {
                JsonObject w = node!.AsObject();
                foreach (string key in (string[])["handle", "title", "className", "processName", "processId", "x", "y", "width", "height"]) {
                    Assert.True(w.ContainsKey(key), $"window descriptor missing '{key}'");
                }
            }
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WithFilterMatchingNothing_SucceedsWithEmptyList() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // A class name no real window uses; the filtered list is empty but that is a clean success,
            // not an error (discovery found nothing to target).
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, ClassName = "MCEC_NoSuchWindowClass_77", Reply = reply };

            bool result = cmd.Execute();

            Assert.True(result);
            JsonObject data = JsonNode.Parse(reply.Captured.Trim())!.AsObject()["data"]!.AsObject();
            Assert.Equal(0, data["count"]!.GetValue<int>());
            Assert.Empty(data["windows"]!.AsArray());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_TimeoutWithNoFilter_RefusesWithInvalidArgument() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // Waiting for "any window" is the arbitrary-target hazard; refuse rather than return window #1.
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, Timeout = 50, Reply = reply };

            bool result = cmd.Execute();

            Assert.False(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.False(json["success"]!.GetValue<bool>());
            Assert.Equal("windows-no-criteria", json["errorCode"]!.GetValue<string>());
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void Execute_WaitWithFilterMatchingNothing_ReturnsEmptyAfterTimeout_NotAnError() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // A filter + short timeout exercises the wait/poll path; nothing matches, so it returns an
            // empty list (count:0) after the timeout; a miss is success, like a one-shot find miss.
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, ClassName = "MCEC_NoSuchWindowClass_77", Timeout = 50, Reply = reply };

            bool result = cmd.Execute();

            Assert.True(result);
            JsonObject data = JsonNode.Parse(reply.Captured.Trim())!.AsObject()["data"]!.AsObject();
            Assert.Equal(0, data["count"]!.GetValue<int>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact] // #112: disappears
    public void Execute_DisappearsWithFilterMatchingNothing_IsSatisfied() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // Nothing matches the fake class, so there is nothing to wait for; it is already "gone".
            WindowsCommand cmd = new() {
                Cmd = "windows", Enabled = true, Reply = reply,
                Condition = "disappears", ClassName = "MCEC_NoSuchWindowClass_112",
            };

            Assert.True(cmd.Execute());
            JsonObject data = JsonNode.Parse(reply.Captured.Trim())!.AsObject()["data"]!.AsObject();
            Assert.Equal("disappears", data["condition"]!.GetValue<string>());
            Assert.True(data["satisfied"]!.GetValue<bool>());
            Assert.Equal(0, data["count"]!.GetValue<int>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact] // #112: foreground predicate, unsatisfied, carries triage diagnostics
    public void Execute_ForegroundWithFilterMatchingNothing_NotSatisfied_WithDiagnostics() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            // The foreground window never has this fake class; one-shot (no timeout) so it is fast.
            WindowsCommand cmd = new() {
                Cmd = "windows", Enabled = true, Reply = reply,
                Condition = "foreground", ClassName = "MCEC_NoSuchWindowClass_112",
            };

            Assert.True(cmd.Execute());
            JsonObject data = JsonNode.Parse(reply.Captured.Trim())!.AsObject()["data"]!.AsObject();
            Assert.Equal("foreground", data["condition"]!.GetValue<string>());
            Assert.False(data["satisfied"]!.GetValue<bool>());
            Assert.True(data.ContainsKey("waitedFor"));
            Assert.True(data.ContainsKey("lastObservedWindows"));
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact] // #112: appears timeout miss carries actionable diagnostics (waitedFor + lastObservedWindows)
    public void Execute_AppearsTimeoutMiss_IncludesDiagnostics() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            WindowsCommand cmd = new() {
                Cmd = "windows", Enabled = true, Reply = reply,
                ClassName = "MCEC_NoSuchWindowClass_112", Timeout = 50,
            };

            Assert.True(cmd.Execute());
            JsonObject data = JsonNode.Parse(reply.Captured.Trim())!.AsObject()["data"]!.AsObject();
            Assert.False(data["satisfied"]!.GetValue<bool>());
            Assert.Equal("MCEC_NoSuchWindowClass_112", data["waitedFor"]!.AsObject()["className"]!.GetValue<string>());
            Assert.True(data.ContainsKey("lastObservedWindows"));
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    [Fact] // #112: disappears without a filter is the arbitrary-target hazard; refused
    public void Execute_DisappearsWithNoFilter_Refused() {
        AssertConditionRefusedWithoutFilter("disappears");
    }

    [Fact] // #112: foreground without a filter is refused for the same reason
    public void Execute_ForegroundWithNoFilter_Refused() {
        AssertConditionRefusedWithoutFilter("foreground");
    }

    [Fact] // #112: an unrecognized condition is a structured invalid-argument refusal
    public void Execute_UnknownCondition_Refused() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            WindowsCommand cmd = new() {
                Cmd = "windows", Enabled = true, Reply = reply,
                Condition = "sideways", ClassName = "MCEC_NoSuchWindowClass_112",
            };

            Assert.False(cmd.Execute());
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.Equal("windows-unknown-condition", json["errorCode"]!.GetValue<string>());
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }

    private static void AssertConditionRefusedWithoutFilter(string condition) {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            WindowsCommand cmd = new() { Cmd = "windows", Enabled = true, Reply = reply, Condition = condition };

            Assert.False(cmd.Execute());
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.Equal("windows-no-criteria", json["errorCode"]!.GetValue<string>());
            Assert.Equal("invalid-argument", json["errorCategory"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }
}
