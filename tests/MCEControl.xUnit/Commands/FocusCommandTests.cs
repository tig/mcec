// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

[Collection("AgentSerial")]
public class FocusCommandTests {
    [Fact]
    public void Constructor_DisabledByDefault() {
        FocusCommand cmd = new();

        Assert.False(cmd.Enabled);
    }

    [Fact]
    public void BuiltInCommands_NonEmpty_ContainsFocus() {
        List<Command> builtIns = FocusCommand.BuiltInCommands;

        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, c => c.Cmd == "focus");
    }

    [Fact]
    public void Clone_CopiesEndpoint_AndIsIndependent() {
        FocusCommand original = new() {
            Cmd = "focus",
            Enabled = true,
            Value = "Preview",
            By = "automationid",
            X = 400,
            Y = 250,
            PointSpecified = true,
        };

        FocusCommand clone = (FocusCommand)original.Clone(null!);

        Assert.NotSame(original, clone);
        Assert.Equal("Preview", clone.Value);
        Assert.Equal("automationid", clone.By);
        Assert.Equal(400, clone.X);
        Assert.Equal(250, clone.Y);
        Assert.True(clone.PointSpecified);

        clone.Value = "changed";
        Assert.Equal("Preview", original.Value);
    }

    [Fact]
    public void Execute_WhenAgentDisabled_ReturnsFalse_WritesFailureJson() {
        // Disabled path never actuates (no real foreground/focus in tests); it must fail closed.
        AgentTestSupport.EnsureTelemetry();
        AgentRuntime.Settings = null; // agent commands disabled
        try {
            CapturingReply reply = new();
            FocusCommand cmd = new() { Cmd = "focus", Enabled = true, Reply = reply, Handle = 0x1234 };

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
    public void ForegroundFailure_MapsToForegroundCategory() {
        // #91/#270: the focus tool is the producer of the foreground category. A window that won't
        // activate is not a bug (internal) and not a retry-the-selector case; it is OS-policy foreground.
        FocusCommand cmd = new() { Cmd = "focus" };

        CommandResult result = cmd.ForegroundFailure();

        Assert.False(result.Success);
        Assert.Equal("foreground-not-set", result.ErrorCode);
        Assert.Equal("foreground", result.ErrorCategory);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void FocusFailure_MapsToFocusCategory_AndCarriesTheWindow() {
        // #91/#270: a foreground window whose focus never landed is the focus category, and the resolved
        // window rides along as lastObservation so the failure is debuggable.
        FocusCommand cmd = new() { Cmd = "focus" };
        WindowInfo window = new() { Handle = 0x99, Title = "WinPrint" };

        CommandResult result = cmd.FocusFailure(window);

        Assert.False(result.Success);
        Assert.Equal("focus-not-confirmed", result.ErrorCode);
        Assert.Equal("focus", result.ErrorCategory);
        Assert.NotNull(result.Data);
        Assert.Equal(0x99, result.Data!["handle"]!.GetValue<long>());
    }
}
