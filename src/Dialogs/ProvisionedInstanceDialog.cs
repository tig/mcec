// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Shows the operator the handoff for an instance they just provisioned from the Agent tab's
/// "Provision new…" button (#296): where the disposable copy lives, how to point an agent at it
/// (the stdio <c>--mcp</c> launch line and, when enabled, the HTTP endpoint + bearer token), and how
/// to tear it down. The whole handoff is one read-only, copyable text block; the operator's next act
/// is pasting this into an MCP client config, so Copy-all is the primary affordance.
///
/// <para>SECURITY: the text includes the session token (#215). That is the point; the operator IS
/// the session owner and needs the credential to hand to their agent; and it is no wider an
/// exposure than <c>provision-session</c> returning the same token to a connected agent.</para>
/// </summary>
internal sealed class ProvisionedInstanceDialog : Form {
    public ProvisionedInstanceDialog(ProvisionedSession session) {
        ArgumentNullException.ThrowIfNull(session);

        Text = $"MCE Controller - Provisioned instance {session.SessionId}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        TextBox handoff = new() {
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = BuildHandoffText(session),
            Size = new Size(640, 320),
            Margin = new Padding(4, 4, 4, 8),
            TabStop = false,
        };

        Button copy = new() {
            Text = "&Copy all",
            AutoSize = true,
        };
        copy.Click += (_, _) => Clipboard.SetText(handoff.Text);
        Button close = new() {
            Text = "Close",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };

        FlowLayoutPanel buttons = new() {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };
        // RightToLeft flow: first added lands right-most; Close sits right, Copy all beside it.
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);

        TableLayoutPanel layout = new() {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };
        layout.Controls.Add(handoff, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        AcceptButton = close;
        CancelButton = close;
    }

    /// <summary>
    /// Renders the copyable handoff: the same facts <see cref="ProvisionedSession.ToJsonObject"/>
    /// hands a connected agent, phrased for the operator who will paste them into an MCP client
    /// config. Static and internal so tests can verify the text without showing a window.
    /// </summary>
    internal static string BuildHandoffText(ProvisionedSession session) {
        StringBuilder sb = new();
        _ = sb.AppendLine("A fresh, disposable MCEC instance is ready. Agent commands are enabled ONLY");
        _ = sb.AppendLine("inside this copy; your installed MCEC is untouched.");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"Directory:  {session.Directory}");
        _ = sb.AppendLine($"Session id: {session.SessionId}");
        _ = sb.AppendLine($"Token:      {session.Token}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Connect over stdio (recommended): configure your MCP client to spawn:");
        _ = sb.AppendLine($"  \"{session.ExePath}\" --mcp");
        _ = sb.AppendLine();
        _ = sb.AppendLine("e.g. Claude Code:");
        _ = sb.AppendLine($"  claude mcp add mcec -- \"{session.ExePath}\" --mcp");
        if (session.McpServerEnabled) {
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Or launch \"{session.ExePath}\" and POST JSON-RPC to its HTTP endpoint:");
            _ = sb.AppendLine($"  http://{session.BindAddress}:{session.Port}/mcp");
            _ = sb.AppendLine($"  with the header: Authorization: Bearer {session.Token}");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("Teardown: have the agent call end-session with the session id and token (after");
        _ = sb.AppendLine("stopping the instance), or delete it from this Agent tab; stale instances are");
        _ = sb.AppendLine("also cleaned up automatically.");
        return sb.ToString();
    }
}
