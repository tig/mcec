// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The operator's answer to a <c>request-command-access</c> consent prompt (#307). Fail-safe by
/// construction: everything that is not an explicit Allow (Esc, the close box, the timeout, an
/// emergency stop engaging while the prompt is up) maps to <see cref="Denied"/> or
/// <see cref="TimedOut"/>, and <see cref="Denied"/> is the enum default so an unset decision can
/// never read as a grant.
/// </summary>
public enum CommandAccessDecision {
    /// <summary>The operator explicitly denied the request (final for this instance; the deny is sticky).</summary>
    Denied,

    /// <summary>The operator never answered; the prompt closed itself. NOT sticky; the agent may ask again.</summary>
    TimedOut,

    /// <summary>Enable exactly the requested commands.</summary>
    AllowRequested,

    /// <summary>Enable the requested commands AND auto-approve this instance's future requests.</summary>
    AllowAny,
}
