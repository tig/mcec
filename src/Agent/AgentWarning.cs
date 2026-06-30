// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// A non-fatal condition surfaced alongside an <see cref="AgentToolResult"/>: the call still succeeded
/// (or failed for a different reason), but the agent should know something was adjusted, degraded, or
/// assumed (e.g. <c>minimized-window</c>, <c>tree-truncated</c>, <c>region-clamped</c>). Same stability
/// rules as error codes — kebab-case, branchable, tolerate unknowns. See
/// <c>docs/design/agent-tool-result-contract.md</c> (#101).
/// </summary>
public sealed class AgentWarning {
    public AgentWarning(string code, string detail) {
        Code = code;
        Detail = detail;
    }

    /// <summary>Stable, kebab-case machine code for the warning condition.</summary>
    public string Code { get; }

    /// <summary>Human-readable explanation of the warning.</summary>
    public string Detail { get; }

    public JsonObject ToJsonObject() => new() {
        ["code"] = Code,
        ["detail"] = Detail,
    };
}
