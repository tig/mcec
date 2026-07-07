// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// A non-fatal condition surfaced alongside a result (e.g. a capture that fell back to an on-screen
/// blit, or a UIA tree clipped to a node cap). <see cref="Code"/> is a stable, branchable string and
/// <see cref="Detail"/> is human-readable. Mirrors the warning shape in the agent result contract
/// (<c>docs/agent_control.md</c> / <c>agent-tool-result.schema.json</c>).
/// </summary>
public sealed record CommandWarning(string Code, string Detail);
