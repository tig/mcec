// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Outcome of <see cref="SessionProvisioner.ValidateTeardownToken"/> (#215): whether an
/// <c>end-session</c> caller presented the credential <c>provision-session</c> issued for that
/// session. Distinct outcomes so the tool can answer honestly — a malformed id, an already-gone
/// session (idempotent success), and a bad credential each get their own recovery.
/// </summary>
public enum SessionTokenValidation {
    /// <summary>The token matches the session's co-located config; teardown may proceed.</summary>
    Valid,

    /// <summary>The session directory no longer exists — teardown is idempotent and there is
    /// nothing left for the credential to protect.</summary>
    SessionGone,

    /// <summary>The session id is not a well-formed 12-hex token (also the path-traversal defense
    /// — see <see cref="SessionProvisioner.Teardown"/>).</summary>
    InvalidId,

    /// <summary>The token is missing/wrong, or the session's config could not be read to verify it
    /// (fail closed; the age-based reaper still collects such a directory eventually).</summary>
    TokenMismatch,
}
