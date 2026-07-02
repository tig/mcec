// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The headless re-arm affordance for the emergency stop (#135): a modal prompt
/// <see cref="HeadlessOperatorUi"/> shows the moment the operator engages the stop (and again on any
/// chord re-press while stopped). In the GUI host re-arm is a <c>MainWindow</c> menu item; headless
/// there is no menu, so this dialog IS the deliberate operator action the latch waits for.
///
/// <para>SECURITY: safe as a re-arm surface because the latch refuses EVERY agent tool call while it is
/// up, so the only input that can reach its buttons is physical. Defaults are fail-safe: Enter, Esc, and
/// closing the dialog all mean "leave stopped"; only an explicit activation of Re-arm resumes
/// actuation.</para>
/// </summary>
internal sealed class EmergencyStopRearmDialog : Form {
    public EmergencyStopRearmDialog(string reason, string hotkeyDisplay) {
        Text = "MCE Controller - Emergency Stop";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        // No owner window exists headless; TopMost keeps the prompt above whatever the agent was driving.
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Label message = new() {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(4, 4, 4, 12),
            Text =
                $"⛔ Emergency stop engaged ({reason}).\n\n" +
                "Agent actuation is halted; every tool call is refused until you re-arm.\n\n" +
                $"Leave it stopped and press {hotkeyDisplay} again to reopen this prompt, " +
                "or re-arm now to let the agent resume.",
        };

        Button rearm = new() {
            Text = "&Re-arm",
            DialogResult = DialogResult.Yes,
            AutoSize = true,
        };
        Button leaveStopped = new() {
            Text = "&Leave stopped",
            DialogResult = DialogResult.No,
            AutoSize = true,
        };

        FlowLayoutPanel buttons = new() {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };
        // RightToLeft flow: first added lands right-most. Leave stopped (the safe choice) sits right,
        // first in tab order, and is both AcceptButton and CancelButton below.
        buttons.Controls.Add(leaveStopped);
        buttons.Controls.Add(rearm);

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

        AcceptButton = leaveStopped;
        CancelButton = leaveStopped;
    }
}
