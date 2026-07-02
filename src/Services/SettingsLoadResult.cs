// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>
/// Result of <see cref="SettingsStore.Load"/> (#216). <see cref="Settings"/> is ALWAYS usable;
/// on any failure it holds defaults; and <see cref="Outcome"/>/<see cref="Error"/> carry what
/// happened so the host (GUI or headless) owns the dialog/log/telemetry decisions instead of the
/// data layer.
/// </summary>
public sealed class SettingsLoadResult {
    /// <summary>The loaded settings, or defaults if the file was missing/unreadable/corrupt. Never null.</summary>
    public required AppSettings Settings { get; init; }

    /// <summary>What happened during the load attempt.</summary>
    public required SettingsLoadOutcome Outcome { get; init; }

    /// <summary>The exception behind a non-<see cref="SettingsLoadOutcome.Loaded"/> outcome, if any.
    /// For <see cref="SettingsLoadOutcome.CreatedDefault"/> this is the write failure (if the default
    /// file could not be created).</summary>
    public Exception? Error { get; init; }

    /// <summary>Human-readable failure detail suitable for a message to the operator. For
    /// <see cref="SettingsLoadOutcome.ParseError"/> this is the innermost exception message
    /// (XmlSerializer wraps the XmlException in an InvalidOperationException).</summary>
    public string? ErrorDetail { get; init; }
}
