// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The failure descriptor inside an <see cref="AgentToolResult"/> (present only when <c>ok</c> is
/// false). Carries a fine-grained, open-ended <see cref="Code"/>, a coarse <see cref="Category"/> from
/// the closed <see cref="AgentErrorCategory"/> taxonomy, a human-readable <see cref="Detail"/>, and an
/// optional <see cref="LastObservation"/> (the last good state before the failure, for debugging and
/// #87's failure-summary). See <c>docs/design/agent-tool-result-contract.md</c> (#101).
/// </summary>
public sealed class AgentError {
    public AgentError(string code, AgentErrorCategory category, string detail, JsonObject? lastObservation = null) {
        Code = code;
        Category = category;
        Detail = detail;
        LastObservation = lastObservation;
    }

    /// <summary>Stable, fine-grained machine code (kebab-case); narrows <see cref="Category"/>.</summary>
    public string Code { get; }

    /// <summary>Coarse failure class from the closed taxonomy.</summary>
    public AgentErrorCategory Category { get; }

    /// <summary>Human-readable explanation of what failed and, where possible, how to recover.</summary>
    public string Detail { get; }

    /// <summary>The last good observation captured before the failure, or null when none exists.</summary>
    public JsonObject? LastObservation { get; }

    /// <summary>The kebab-case wire string for <see cref="Category"/> that goes into <c>error.category</c>.</summary>
    public string CategoryWire => Category switch {
        AgentErrorCategory.Timeout => "timeout",
        AgentErrorCategory.AmbiguousSelector => "ambiguous-selector",
        AgentErrorCategory.StaleElement => "stale-element",
        AgentErrorCategory.NoTarget => "no-target",
        AgentErrorCategory.CaptureBlank => "capture-blank",
        AgentErrorCategory.Focus => "focus",
        AgentErrorCategory.Elevation => "elevation",
        AgentErrorCategory.Foreground => "foreground",
        AgentErrorCategory.Internal => "internal",
        _ => "internal",
    };

    public JsonObject ToJsonObject() {
        JsonObject obj = new() {
            ["code"] = Code,
            ["category"] = CategoryWire,
            ["detail"] = Detail,
        };
        if (LastObservation is not null) {
            obj["lastObservation"] = LastObservation.DeepClone();
        }
        return obj;
    }
}
