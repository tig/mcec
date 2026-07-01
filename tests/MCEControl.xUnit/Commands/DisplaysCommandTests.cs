// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class DisplaysCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        DisplaysCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsDisplays() {
        List<Command> builtIns = DisplaysCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "displays");
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            DisplaysCommand cmd = new() { Cmd = "displays", Enabled = true, Reply = reply };

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
    public void Execute_WhenEnabled_ReportsDisplayGeometryAndDpi() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            DisplaysCommand cmd = new() { Cmd = "displays", Enabled = true, Reply = reply };

            bool result = cmd.Execute();

            Assert.True(result);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>());

            JsonObject data = json["data"]!.AsObject();

            // The union desktop always has a positive size, even on a single-monitor host.
            JsonObject virt = data["virtualBounds"]!.AsObject();
            Assert.True(virt["width"]!.GetValue<int>() > 0);
            Assert.True(virt["height"]!.GetValue<int>() > 0);

            JsonArray displays = data["displays"]!.AsArray();
            Assert.Equal(data["count"]!.GetValue<int>(), displays.Count);
            Assert.NotEmpty(displays);

            JsonObject first = displays[0]!.AsObject();
            JsonObject bounds = first["bounds"]!.AsObject();
            Assert.True(bounds["width"]!.GetValue<int>() > 0);
            Assert.True(bounds["height"]!.GetValue<int>() > 0);
            Assert.True(first.ContainsKey("workingArea"));
            Assert.True(first.ContainsKey("primary"));

            // DPI falls back to 96 (scale 1.0) if GetDpiForMonitor is unavailable, so it is always positive.
            Assert.True(first["dpi"]!.GetValue<int>() > 0);
            Assert.True(first["scale"]!.GetValue<double>() > 0);
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }
}
