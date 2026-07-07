// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Headless tests for the <c>request-command-access</c> consent prompt body (#307): the operator must
/// see what capability each command grants, the agent's reason framed as untrusted, the in-memory /
/// this-instance-only scope, the sticky-deny consequence, and the auto-deny timeout; verified via the
/// static <see cref="CommandAccessConsentDialog.BuildBody"/> without showing a window.
/// </summary>
public class CommandAccessConsentDialogTests {
    private static CommandAccessRequest SampleRequest() => new() {
        Commands = ["launch", "chars:"],
        DisplayLines = ["launch: Start a program.", "chars: a raw MCEC command (CharsCommand)"],
        Reason = "need to launch notepad to edit the export",
    };

    [Fact]
    public void Body_ListsEveryRequestedCommandWithItsCapability() {
        string body = CommandAccessConsentDialog.BuildBody(SampleRequest(), 120);
        Assert.Contains("launch: Start a program.", body);
        Assert.Contains("chars: a raw MCEC command (CharsCommand)", body);
    }

    [Fact]
    public void Body_FramesTheReasonAsUntrustedQuotedText() {
        string body = CommandAccessConsentDialog.BuildBody(SampleRequest(), 120);
        Assert.Contains("The agent says (unverified):", body);
        Assert.Contains("\"need to launch notepad to edit the export\"", body);
    }

    [Fact]
    public void EmergencyStopDismissal_ReportsTimedOut_NotAnOperatorDeny() {
        // #308 review: the panic hotkey halts the session; it does not answer the consent question.
        // Dismissing as Denied would record a sticky per-command deny the operator never chose.
        using var dialog = new CommandAccessConsentDialog(SampleRequest());
        Assert.Equal(CommandAccessDecision.Denied, dialog.Decision); // the fail-safe default
        dialog.HandleEmergencyStop(stopped: true);
        Assert.Equal(CommandAccessDecision.TimedOut, dialog.Decision);
    }

    [Fact]
    public void Body_StatesScopeStickyDenyAndTimeout() {
        string body = CommandAccessConsentDialog.BuildBody(SampleRequest(), 90);
        // In-memory, this instance only; never persisted.
        Assert.Contains("THIS instance only", body);
        Assert.Contains("nothing is written to any config file", body);
        // The allow-any choice still audits every grant.
        Assert.Contains("audit-logged", body);
        // Deny is final; doing nothing denies at the timeout.
        Assert.Contains("Deny is final for this instance", body);
        Assert.Contains("90 seconds", body);
    }
}
