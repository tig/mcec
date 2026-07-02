// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// How a <see cref="SettingsStore.Load"/> attempt turned out. Every outcome still yields usable
/// settings (issue #155: a bad settings file must never put MCEC into a fail-to-start state) —
/// the outcome tells the host what happened so IT can decide what UI (if any) to show (#216).
/// </summary>
public enum SettingsLoadOutcome {
    /// <summary>The settings file existed and loaded cleanly.</summary>
    Loaded,

    /// <summary>No settings file existed; defaults were created and written to disk.
    /// If the write itself failed, <see cref="SettingsLoadResult.Error"/> is set.</summary>
    CreatedDefault,

    /// <summary>The file exists but could not be read (ACLs). Defaults are used for this run;
    /// the unreadable file is left untouched.</summary>
    AccessDenied,

    /// <summary>The file is corrupt or invalid XML (mid-write crash, disk error, hand-edit, or a
    /// prohibited DTD). Defaults are used for this run; the file is deliberately NOT overwritten
    /// so the user can inspect/repair it.</summary>
    ParseError,

    /// <summary>Any other load failure. Defaults are used for this run.</summary>
    UnexpectedError,
}
