// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Outcome of <see cref="CommandInvoker.Enqueue"/> (#195): whether the decoded command tree actually
/// entered the execute queue. The agent's <c>send_command</c> reports a failure for anything other
/// than <see cref="Enqueued"/> instead of pretending success for a command that will never run (the
/// legacy TCP/serial path still only logs; its clients have no error channel).
/// </summary>
public enum CommandEnqueueResult {
    /// <summary>The whole command tree was enqueued and will execute.</summary>
    Enqueued,

    /// <summary>The command string matched nothing in the loaded command table.</summary>
    UnknownCommand,

    /// <summary>
    /// The tree was dropped whole: over the #154 bounds (embedded-expansion / queue depth) or the
    /// invoker is shut down.
    /// </summary>
    Dropped,
}
