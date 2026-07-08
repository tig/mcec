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
/// failure-summary artifacts). See <c>docs/agent_control.md</c> / <c>agent-tool-result.schema.json</c>.
/// </summary>
public sealed class AgentError(string code, AgentErrorCategory category, string detail, JsonObject? lastObservation = null, JsonObject? partialResult = null) {
    /// <summary>Stable, fine-grained machine code (kebab-case); narrows <see cref="Category"/>.</summary>
    private string Code { get; } = code;

    /// <summary>Coarse failure class from the closed taxonomy.</summary>
    private AgentErrorCategory Category { get; } = category;

    /// <summary>Human-readable explanation of what failed and, where possible, how to recover.</summary>
    private string Detail { get; } = detail;

    /// <summary>The last good observation captured before the failure, or null when none exists.</summary>
    private JsonObject? LastObservation { get; } = lastObservation;

    /// <summary>
    /// The failing call's OWN partial payload, when the command deliberately kept one; e.g. a blank
    /// <c>capture</c> still carries the (suspect) PNG it grabbed so the evidence is not lost (#206).
    /// Distinct from <see cref="LastObservation"/>, which is the last GOOD state from a prior call.
    /// </summary>
    private JsonObject? PartialResult { get; } = partialResult;

    /// <summary>The kebab-case wire string for <see cref="Category"/> that goes into <c>error.category</c>.</summary>
    public string CategoryWire => Category switch {
        AgentErrorCategory.Timeout => "timeout",
        AgentErrorCategory.AmbiguousSelector => "ambiguous-selector",
        AgentErrorCategory.StaleElement => "stale-element",
        AgentErrorCategory.NoTarget => "no-target",
        AgentErrorCategory.InvalidArgument => "invalid-argument",
        AgentErrorCategory.CaptureBlank => "capture-blank",
        AgentErrorCategory.OcrBlank => "ocr-blank",
        AgentErrorCategory.OcrNoText => "ocr-no-text",
        AgentErrorCategory.Focus => "focus",
        AgentErrorCategory.Elevation => "elevation",
        AgentErrorCategory.Foreground => "foreground",
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
        if (PartialResult is not null) {
            obj["partialResult"] = PartialResult.DeepClone();
        }
        return obj;
    }
}
