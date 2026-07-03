// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>
/// A snapshot of one provisioned session directory under <see cref="SessionProvisioner.SessionsRoot"/>,
/// as enumerated by <see cref="SessionProvisioner.ListSessions"/> (#259). This is the operator-facing
/// view (the Settings dialog's Agent tab lists these); contrast <see cref="ProvisionedSession"/>, the
/// agent-facing handoff <see cref="SessionProvisioner.Provision"/> returns.
/// </summary>
public sealed class ProvisionedSessionInfo {
    /// <summary>The session id (the directory name; a 12-hex token for sessions Provision created).</summary>
    public required string SessionId { get; init; }

    /// <summary>Full path of the session directory.</summary>
    public required string Directory { get; init; }

    /// <summary>When the session directory was created (UTC); the same stamp the age-reaper keys on.</summary>
    public required DateTime CreatedUtc { get; init; }

    /// <summary>Total size of the directory's files in bytes (best-effort; 0 if it could not be summed).</summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// True when the session's <c>mcec.exe</c> is locked (the session is running); such a directory
    /// cannot be deleted until the process exits, matching the reaper's skip behavior.
    /// </summary>
    public required bool IsRunning { get; init; }
}
