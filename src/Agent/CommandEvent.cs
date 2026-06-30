// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// One structured "a command just ran" event, published on the <see cref="CommandEventHub"/> by the
/// execution seam and consumed by the on-screen command overlay (#119). This is the dedicated event
/// hook chosen for the overlay so it gets purpose-built terse text and fields (outcome, session) rather
/// than re-parsing the GUI log view.
/// </summary>
public sealed class CommandEvent {
    public CommandEvent(string command, string terseText, CommandOutcome outcome, string? sessionId = null) {
        Command = command;
        TerseText = terseText;
        Outcome = outcome;
        SessionId = sessionId;
    }

    /// <summary>The command/tool name (e.g. <c>capture</c>, <c>invoke</c>, <c>send_command</c>).</summary>
    public string Command { get; }

    /// <summary>The condensed one-line label the overlay shows (e.g. <c>invoke expand "Help"</c>).</summary>
    public string TerseText { get; }

    /// <summary>Whether the command succeeded, failed, is pending, or is informational.</summary>
    public CommandOutcome Outcome { get; }

    /// <summary>The owning agent session id (#86), or null for a non-session command.</summary>
    public string? SessionId { get; }
}
