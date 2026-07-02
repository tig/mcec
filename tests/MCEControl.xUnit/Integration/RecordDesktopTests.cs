// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Integration;

/// <summary>
/// Opt-in, desktop-dependent test for real GIF recording: it captures a small live screen region for
/// a bounded one-shot and proves a non-empty, valid animated GIF is written. It touches the real
/// desktop (screen grab), so it is skipped unless <c>MCEC_DESKTOP_E2E=1</c> is set; a normal
/// <c>dotnet test</c> / CI run never records the screen.
/// Run it deliberately on an interactive session:
///   <c>$env:MCEC_DESKTOP_E2E=1; dotnet test --filter Category=DesktopE2E</c>
/// </summary>
[Collection("AgentSerial")]
public class RecordDesktopTests {
    [Fact]
    [Trait("Category", "DesktopE2E")]
    public void Record_Oneshot_Region_WritesAnimatedGif() {
        if (Environment.GetEnvironmentVariable("MCEC_DESKTOP_E2E") != "1") {
            return; // gated off (CI / normal test runs do not capture the screen)
        }

        AgentTestSupport.EnsureTelemetry();
        string outFile = Path.Combine(Path.GetTempPath(), $"mcec-rec-test-{Guid.NewGuid():N}.gif");
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        try {
            CapturingReply reply = new();
            RecordCommand cmd = new() {
                Cmd = "record",
                Enabled = true,
                Reply = reply,
                X = 0, Y = 0, Width = 320, Height = 240,
                Fps = 5,
                DurationMs = 1000,
                File = outFile,
            };

            bool ok = cmd.Execute();

            Assert.True(ok);
            JsonObject json = JsonNode.Parse(reply.Captured.Trim())!.AsObject();
            Assert.True(json["success"]!.GetValue<bool>(), reply.Captured);

            JsonObject data = json["data"]!.AsObject();
            Assert.True(data["frames"]!.GetValue<int>() >= 2);
            Assert.Equal(outFile, data["file"]!.GetValue<string>());

            Assert.True(File.Exists(outFile));
            byte[] gif = File.ReadAllBytes(outFile);
            Assert.True(gif.Length > 0);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
        }
        finally {
            AgentRuntime.Settings = null;
            try { File.Delete(outFile); } catch (IOException) { /* best effort cleanup */ }
        }
    }
}
