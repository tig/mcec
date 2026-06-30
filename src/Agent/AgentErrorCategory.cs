// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The <b>closed</b> failure taxonomy for every MCEC 3.0 agent tool result, owned by the contract in
/// <c>docs/design/agent-tool-result-contract.md</c> (#101). An agent may branch exhaustively on this
/// set; new failure modes are mapped onto an existing member rather than added ad hoc. The wire form
/// (the kebab-case string that goes into <c>error.category</c>) is produced by
/// <see cref="AgentError.CategoryWire"/>.
/// </summary>
public enum AgentErrorCategory {
    /// <summary>A wait/poll (e.g. <c>wait-for</c>) expired before its condition held.</summary>
    Timeout,

    /// <summary>A selector matched more than one candidate and the tool refused to guess.</summary>
    AmbiguousSelector,

    /// <summary>A previously resolved element/handle is no longer valid (window closed, tree re-rendered).</summary>
    StaleElement,

    /// <summary>A selector matched nothing (no window/element).</summary>
    NoTarget,

    /// <summary>A screenshot was produced but detected as black/blank.</summary>
    CaptureBlank,

    /// <summary>An action required input focus that could not be set.</summary>
    Focus,

    /// <summary>The target runs at a higher integrity level (UAC) than MCEC and cannot be driven.</summary>
    Elevation,

    /// <summary>An action required the target to be foreground and it could not be brought forward.</summary>
    Foreground,

    /// <summary>An unexpected MCEC-side fault (bug, unhandled exception) or a policy refusal the agent
    /// cannot recover from on its own.</summary>
    Internal,
}
