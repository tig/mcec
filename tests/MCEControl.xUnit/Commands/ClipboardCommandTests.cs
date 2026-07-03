// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class ClipboardCommandTests {
    [Fact]
    public void BuiltInCommands_ContainsClipboard() {
        Assert.Contains(ClipboardCommand.BuiltInCommands, c => c.Cmd == "clipboard");
    }

    [Fact]
    public void SetAndGet_RoundTripsText() {
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            const string text = "C:\\demo\\winprintdemo.pdf";
            CapturingReply reply = new();
            ClipboardCommand set = new() { Cmd = "clipboard", Enabled = true, Action = "set", Text = text, Reply = reply };
            Assert.True(set.Execute());
            JsonObject setJson = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(setJson["success"]!.GetValue<bool>());
            Assert.True(setJson["data"]!["set"]!.GetValue<bool>());

            reply = new CapturingReply();
            ClipboardCommand get = new() { Cmd = "clipboard", Enabled = true, Action = "get", Reply = reply };
            Assert.True(get.Execute());
            JsonObject getJson = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(getJson["success"]!.GetValue<bool>());
            Assert.Equal(text, getJson["data"]!["text"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Settings = null;
        }
    }
}