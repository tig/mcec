// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Which transport a JSON-RPC request arrived over. It matters for one security decision: the raw
/// <c>send_command</c> pass-through is reachable without the agent-surface opt-in only over the LOCAL
/// stdio transport (the operator launched <c>mcec.exe --mcp</c>), never over the network-facing HTTP
/// floor (#153). Every other tool is gated identically on both transports.
/// </summary>
public enum AgentTransport {
    /// <summary>Local stdio (<c>mcec.exe --mcp</c>); the process was launched by its client; no CSRF surface.</summary>
    Stdio,

    /// <summary>The localhost HTTP floor (<c>POST /mcp</c>); network-reachable, so CSRF/DNS-rebinding applies.</summary>
    Http,
}
