// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The operator consent prompt for <c>request-command-access</c> (#307): an agent asked to use
/// disabled command(s), and this dialog is the deliberate, out-of-band operator action that decides.
/// Three-way: allow exactly the requested commands, allow those plus any later requests (this
/// instance only), or deny (sticky). Defaults are fail-safe: Deny is the default button, and Enter,
/// Esc, the close box, the timeout, and an emergency stop engaging all mean deny/timeout; only an
/// explicit click of an Allow button grants anything.
///
/// <para>SECURITY: the agent being asked about can synthesize input and drive UIA on this very
/// desktop, so the dialog must be unreachable by everything but physical input. Three layers:
/// (1) the executor holds <see cref="AgentRuntime.InputGate"/> for the prompt's whole lifetime, so
/// queued/synthesized input cannot land; (2) the executor refuses every actuation-capable tool call
/// with <c>consent-pending</c> while the prompt is up (notably <c>invoke</c>, which is UIA-pattern
/// actuation that never takes the gate); (3) this window registers itself with
/// <see cref="WindowResolver"/> as never-a-target (the overlay's #119 mechanism), so in-process
/// window resolution can never hand the agent its own consent prompt. Residual risk: a SECOND MCEC
/// instance driven by the same agent shares none of these; the audit log and overlay narration are
/// the mitigations there.</para>
///
/// <para>The timeout closes the dialog as <see cref="CommandAccessDecision.TimedOut"/> so the prompt
/// can never outlive its tool call; a late "Allow" after the agent already received consent-timeout
/// would be a silent grant.</para>
/// </summary>
internal sealed class CommandAccessConsentDialog : Form {
    /// <summary>The operator's decision; <see cref="CommandAccessDecision.Denied"/> unless an Allow button was clicked or the timeout fired.</summary>
    public CommandAccessDecision Decision { get; private set; } = CommandAccessDecision.Denied;

    private readonly System.Windows.Forms.Timer _timeout;
    private readonly Action<bool> _onEmergencyStop;

    // The handle currently registered as ignored (never-a-target), tracked so a WinForms handle
    // recreation never leaves a stale HWND in the resolver's ignore set (see CommandOverlayWindow).
    private long _registeredHandle;

    public CommandAccessConsentDialog(CommandAccessRequest request, int timeoutMs = AgentConsent.PromptTimeoutMs) {
        ArgumentNullException.ThrowIfNull(request);

        Text = "MCE Controller - Agent requests command access";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        // No owner: the GUI host's MainWindow may be hidden in the tray and headless has no window at
        // all; TopMost keeps the prompt above whatever the agent was driving.
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Label message = new() {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(4, 4, 4, 12),
            Text = BuildBody(request, timeoutMs / 1000),
            // The reason inside the body is untrusted agent text; a Label renders it inert (no
            // links, no mnemonics), so it cannot smuggle an accelerator or spoof a control.
            UseMnemonic = false,
        };

        Button allowRequested = new() {
            Text = "Allow &these commands",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };
        allowRequested.Click += (_, _) => Decision = CommandAccessDecision.AllowRequested;
        Button allowAny = new() {
            Text = "Allow these + &any later requests",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };
        allowAny.Click += (_, _) => Decision = CommandAccessDecision.AllowAny;
        Button deny = new() {
            Text = "&Deny",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
        };
        deny.Click += (_, _) => Decision = CommandAccessDecision.Denied;

        FlowLayoutPanel buttons = new() {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };
        // RightToLeft flow: first added lands right-most. Deny (the safe choice) sits right, first in
        // tab order, and is both AcceptButton and CancelButton below.
        buttons.Controls.Add(deny);
        buttons.Controls.Add(allowAny);
        buttons.Controls.Add(allowRequested);

        TableLayoutPanel layout = new() {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };
        layout.Controls.Add(message, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        AcceptButton = deny;
        CancelButton = deny;

        _timeout = new System.Windows.Forms.Timer { Interval = timeoutMs };
        _timeout.Tick += (_, _) => {
            Decision = CommandAccessDecision.TimedOut;
            Close();
        };
        _timeout.Start();

        // The operator's panic hotkey wins over an open consent prompt: engaging the stop dismisses
        // this dialog as a deny (Decision stays Denied). Fires on the hook/pool thread; marshal.
        _onEmergencyStop = stopped => {
            if (!stopped) {
                return;
            }
            try {
                if (IsHandleCreated && !IsDisposed) {
                    BeginInvoke(Close);
                }
            }
            catch (InvalidOperationException) {
                // Handle torn down between the check and the post; the dialog is already closing.
            }
        };
        EmergencyStop.StateChanged += _onEmergencyStop;
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        // Never-a-target (#119 mechanism): without this, `invoke`/`click` by name could resolve this
        // very dialog and press its own Allow button (invoke is UIA actuation and does not take the
        // input gate). Defensively drop any previously-registered handle first (handle recreation).
        if (_registeredHandle != 0) {
            WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
        }
        _registeredHandle = Handle.ToInt64();
        WindowResolver.RegisterIgnoredWindow(_registeredHandle);
    }

    protected override void OnHandleDestroyed(EventArgs e) {
        if (_registeredHandle != 0) {
            WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
            _registeredHandle = 0;
        }
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _timeout.Dispose();
            EmergencyStop.StateChanged -= _onEmergencyStop;
            // OnHandleDestroyed normally clears the registration; unregister defensively in case the
            // window is disposed without a handle-destroyed notification.
            if (_registeredHandle != 0) {
                WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
                _registeredHandle = 0;
            }
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Renders the prompt body. Static and internal so tests can verify the text (the command list,
    /// the quoted-as-untrusted reason framing, the in-memory/this-instance-only scope, the sticky
    /// deny, and the timeout) without showing a window.
    /// </summary>
    internal static string BuildBody(CommandAccessRequest request, int timeoutSeconds) {
        StringBuilder sb = new();
        _ = sb.AppendLine("An agent connected to this MCEC instance is asking to use "
            + (request.Commands.Count == 1 ? "a command that is" : "commands that are")
            + " currently disabled:");
        _ = sb.AppendLine();
        foreach (string line in request.DisplayLines) {
            _ = sb.AppendLine($"    {line}");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("The agent says (unverified):");
        _ = sb.AppendLine($"    \"{request.Reason}\"");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Allowing enables the command(s) in THIS instance only, in memory, until it "
            + "exits; nothing is written to any config file. \"Allow these + any later requests\" also "
            + "auto-approves this instance's future requests; every grant is still audit-logged and "
            + "narrated on the overlay.");
        _ = sb.AppendLine();
        _ = sb.Append($"Deny is final for this instance (the agent cannot ask again for these commands). "
            + $"Doing nothing denies the request in {timeoutSeconds} seconds.");
        return sb.ToString();
    }
}
