// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The UI-agnostic seam between MCEC's command engine and whatever is hosting it. In GUI mode
/// <see cref="MainWindow"/> populates <see cref="Settings"/> and <see cref="Invoker"/> after load;
/// in headless MCP mode (<c>--mcp</c>) the bootstrap in <see cref="Program"/> does the same. Agent
/// code reads gating and the command table from here rather than reaching into <c>MainWindow</c>,
/// so the engine works with no window (the "headless first" requirement from the proposal).
///
/// SECURITY: the agent observation commands (capture/query/find/invoke) are gated by
/// <see cref="AgentCommandsEnabled"/> — a SEPARATE opt-in from the existing actuation enable.
/// Enabling "press keys" must not silently enable "screenshot my screen". Every agent action is
/// logged loudly via <see cref="Audit"/>.
/// </summary>
public static class AgentRuntime {
    public static AppSettings? Settings { get; set; }

    public static CommandInvoker? Invoker { get; set; }

    /// <summary>
    /// True only when the user has explicitly opted in to the agent observation/targeting commands.
    /// Defaults to false (disabled) when no settings are loaded.
    /// </summary>
    public static bool AgentCommandsEnabled => Settings?.AgentCommandsEnabled ?? false;

    /// <summary>
    /// Loud, structured audit log for every observation/agent action: what was captured/queried,
    /// when, and with what target. Deliberately logged at Info so it shows in the GUI log view.
    /// </summary>
    public static void Audit(string action, string detail) =>
        Logger.Instance.Log4.Info($"AGENT-AUDIT: {action} — {detail}");
}
