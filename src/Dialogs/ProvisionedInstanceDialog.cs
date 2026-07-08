// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Shows the operator the handoff for an instance they just provisioned from the Agent tab's
/// "Provision new…" button (#296), in the two steps the operator actually performs (#307):
/// step 1, register the instance as an MCP server (the stdio <c>--mcp</c> launch line and, when
/// enabled, the HTTP endpoint + bearer token); step 2, paste a ready-made briefing prompt to the
/// agent (session identity, token custody, rules of engagement, teardown duty). Each step is its own
/// read-only text block with its own copy button; "Copy prompt" is the primary affordance because it
/// is the last thing the operator does before talking to the agent.
///
/// <para>SECURITY: both blocks include the session token (#215). That is the point; the operator IS
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

        Label setupLabel = new() {
            AutoSize = true,
            Margin = new Padding(4, 4, 4, 2),
            Text = "Step 1 (you): register the instance as an MCP server:",
        };
        TextBox setup = new() {
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = BuildHandoffText(session),
            Size = new Size(680, 150),
            Margin = new Padding(4, 0, 4, 8),
            TabStop = false,
        };

        Label promptLabel = new() {
            AutoSize = true,
            Margin = new Padding(4, 4, 4, 2),
            Text = "Step 2 (you): paste this briefing to your agent as its first message:",
        };
        TextBox prompt = new() {
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = BuildAgentPrompt(session),
            Size = new Size(680, 220),
            Margin = new Padding(4, 0, 4, 8),
            TabStop = false,
        };

        Button copySetup = new() {
            Text = "Copy &setup",
            AutoSize = true,
        };
        copySetup.Click += (_, _) => Clipboard.SetText(setup.Text);
        Button copyPrompt = new() {
            Text = "Copy &prompt",
            AutoSize = true,
        };
        copyPrompt.Click += (_, _) => Clipboard.SetText(prompt.Text);
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
        // RightToLeft flow: first added lands right-most; Close sits right, then Copy prompt (the
        // primary affordance, nearest Close), then Copy setup.
        buttons.Controls.Add(close);
        buttons.Controls.Add(copyPrompt);
        buttons.Controls.Add(copySetup);

        TableLayoutPanel layout = new() {
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };
        layout.Controls.Add(setupLabel, 0, 0);
        layout.Controls.Add(setup, 0, 1);
        layout.Controls.Add(promptLabel, 0, 2);
        layout.Controls.Add(prompt, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);

        AcceptButton = close;
        CancelButton = close;
    }

    /// <summary>
    /// Renders step 1, the MCP-client setup block: the same facts
    /// <see cref="ProvisionedSession.ToJsonObject"/> hands a connected agent, phrased for the
    /// operator who will paste them into an MCP client config. Static and internal so tests can
    /// verify the text without showing a window.
    /// </summary>
    internal static string BuildHandoffText(ProvisionedSession session) {
        StringBuilder sb = new();
        _ = sb.AppendLine("A fresh, disposable MCEC instance is ready. Agent commands are enabled ONLY");
        _ = sb.AppendLine("inside this copy; your installed MCEC is untouched.");
        _ = sb.AppendLine();
        // The identity + credential ALWAYS ride this block too (#308 review): the operator may only
        // ever copy/keep this one, and a stdio-only session would otherwise show the token nowhere
        // outside the briefing.
        _ = sb.AppendLine($"Directory:  {session.Directory}");
        _ = sb.AppendLine($"Provision id: {session.SessionId} (for end-session only — not tool-call sessionId)");
        _ = sb.AppendLine($"Token:      {session.Token}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Connect over stdio (recommended): configure your MCP client to spawn:");
        _ = sb.AppendLine($"  \"{session.ExePath}\" --mcp");
        _ = sb.AppendLine();
        _ = sb.AppendLine("e.g. Claude Code:");
        _ = sb.AppendLine($"  claude mcp add mcec -- \"{session.ExePath}\" --mcp");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Grok Build:");
        _ = sb.AppendLine($"  grok mcp add mcec -- \"{session.ExePath}\" mcp");
        _ = sb.AppendLine("  (or edit ~/.grok/config.toml under [mcp_servers.mcec])");
        _ = sb.AppendLine("  Useful: grok mcp list | grok mcp doctor mcec | /mcps in TUI");
        if (session.McpServerEnabled) {
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Or launch \"{session.ExePath}\" and POST JSON-RPC to its HTTP endpoint:");
            _ = sb.AppendLine($"  http://{session.BindAddress}:{session.Port}/mcp");
            _ = sb.AppendLine($"  with the header: Authorization: Bearer {session.Token}");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("Teardown: the briefing below tells the agent to stop the instance and report");
        _ = sb.AppendLine("done; you can then delete it from this Agent tab (or an agent connected to");
        _ = sb.AppendLine("the installed bootstrap calls end-session with the provision id and token");
        _ = sb.AppendLine("above). Stale instances are also cleaned up automatically.");
        return sb.ToString();
    }

    /// <summary>
    /// Renders step 2, the agent briefing prompt (#307): what the connect-time instructions cannot
    /// know; the session identity, token custody, the operator's rules of engagement (ask via
    /// request-command-access, never edit config files, stop on emergency-stopped, teardown duty);
    /// and a task placeholder. It deliberately does NOT restate the tool playbook: that is the
    /// server's connect-time instructions (AgentInstructions.md), the single source the prompt
    /// points the agent at. Static and internal so tests can verify the text (and that every tool
    /// name and error code it mentions exists on the served surface) without showing a window.
    /// </summary>
    internal static string BuildAgentPrompt(ProvisionedSession session) {
        StringBuilder sb = new();
        _ = sb.AppendLine("You have access to an MCP server named \"mcec\" (MCE Controller). It lets you");
        _ = sb.AppendLine("observe and drive native Windows applications on my PC: enumerate windows, read");
        _ = sb.AppendLine("UI Automation trees, capture screenshots, invoke controls, click, drag, type,");
        _ = sb.AppendLine("and launch apps.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("You are driving a DISPOSABLE, provisioned MCEC instance, not my installed copy:");
        _ = sb.AppendLine($"  Provision id: {session.SessionId} (for end-session only — not tool-call sessionId)");
        _ = sb.AppendLine($"  Directory:  {session.Directory}");
        _ = sb.AppendLine($"  Token:      {session.Token}");
        _ = sb.AppendLine("Keep the token: it is the session credential, required for teardown"
            + (session.McpServerEnabled ? " and as the" : "."));
        if (session.McpServerEnabled) {
            _ = sb.AppendLine($"'Authorization: Bearer <token>' header on any HTTP call to http://{session.BindAddress}:{session.Port}/mcp.");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("Before acting, read the server's connect-time instructions; they define the");
        _ = sb.AppendLine("observe -> target -> act -> verify loop and every tool's contract. Follow them.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Rules of engagement:");
        _ = sb.AppendLine("1. If a tool or command is refused with error.code \"command-disabled\", call the");
        _ = sb.AppendLine("   request-command-access tool with the command name(s) and a one-line reason. I");
        _ = sb.AppendLine("   will approve or deny in a dialog on my screen; the call waits for my answer.");
        _ = sb.AppendLine("   NEVER edit mcec.commands, mcec.settings, or any file in the session directory");
        _ = sb.AppendLine("   to grant yourself access.");
        _ = sb.AppendLine("2. If any call fails with error.code \"emergency-stopped\", I halted you on");
        _ = sb.AppendLine("   purpose. Stop immediately and check in with me; do not retry.");
        // Teardown must work UNATTENDED, so the briefing never routes it through a command that
        // needs a consent round-trip: the raw mcec: built-in is disabled in every provisioned
        // instance (#308 review). Disconnecting ends a stdio instance (the server stops at EOF).
        _ = sb.AppendLine("3. When your task is complete: tell me it is finished so I can delete the");
        _ = sb.AppendLine("   session from Settings > Agent (you cannot disconnect your own MCP client).");
        _ = sb.AppendLine("   If you are also connected to my installed MCEC's bootstrap server, call its");
        _ = sb.AppendLine("   end-session tool with the provision id and token above once this instance");
        _ = sb.AppendLine("   has stopped.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Client setup (for the human operator):");
        _ = sb.AppendLine("  Grok Build:  grok mcp add mcec -- \"<exe path>\" mcp");
        _ = sb.AppendLine("               (or add to ~/.grok/config.toml under [mcp_servers.mcec])");
        _ = sb.AppendLine("  Claude etc.: claude mcp add mcec -- \"<exe path>\" --mcp   or JSON mcpServers");
        _ = sb.AppendLine("  Check status with: grok mcp doctor mcec  |  /mcps  (in Grok TUI)");
        _ = sb.AppendLine();
        _ = sb.Append("My task for you: <describe the task here>");
        return sb.ToString();
    }
}
