// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The outcome of a command for the on-screen command overlay (#119). The overlay renders this as
/// colour/emphasis (e.g. a failed action stands out) in addition to the terse text.
/// </summary>
public enum CommandOutcome {
    /// <summary>Dispatched but not yet resolved (e.g. an <c>invoke</c> that opened a modal dialog).</summary>
    Pending,

    /// <summary>The command achieved its goal.</summary>
    Ok,

    /// <summary>The command failed.</summary>
    Failed,

    /// <summary>An informational note with no success/failure semantics.</summary>
    Info,
}
