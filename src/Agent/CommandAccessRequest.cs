// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;

namespace MCEControl;

/// <summary>
/// One <c>request-command-access</c> ask, shaped for the operator's consent prompt (#307): the
/// resolved command-table names the agent wants enabled, a display line per command (name plus what
/// it does, so the operator judges the capability rather than the name), and the agent's stated
/// reason. The reason is UNTRUSTED text straight from the agent; the dialog renders it visibly
/// quoted and attributed so it can never impersonate MCEC's own chrome.
/// </summary>
public sealed record CommandAccessRequest {
    /// <summary>The resolved (lower-case) command-table names being requested; already filtered to disabled, known commands.</summary>
    public required IReadOnlyList<string> Commands { get; init; }

    /// <summary>One line per requested command: the name and a short description of what enabling it permits.</summary>
    public required IReadOnlyList<string> DisplayLines { get; init; }

    /// <summary>The agent's stated reason (sanitized: length-capped, newlines collapsed). Untrusted.</summary>
    public required string Reason { get; init; }
}
