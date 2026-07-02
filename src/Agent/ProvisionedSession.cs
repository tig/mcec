// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The handoff descriptor returned by <see cref="SessionProvisioner.Provision"/> (#138): everything an
/// authorized agent needs to run a fresh, disposable, isolated MCEC instance; where it lives, how to
/// launch it, how to reach its MCP server, and the id/token to tear it down. The installed MCEC's config
/// is never touched; all enabled state lives only inside <see cref="Directory"/>, so teardown is just
/// deleting that directory.
/// </summary>
public sealed class ProvisionedSession {
    public required string SessionId { get; init; }

    /// <summary>The disposable session directory (contains mcec.exe + deps + co-located agent-ready config).</summary>
    public required string Directory { get; init; }

    /// <summary>Full path to the mcec.exe the agent should launch (inside <see cref="Directory"/>).</summary>
    public required string ExePath { get; init; }

    /// <summary>Whether the co-located config enabled the MCP/HTTP transport for this session.</summary>
    public required bool McpServerEnabled { get; init; }

    /// <summary>The localhost bind address the session's MCP HTTP server uses (when enabled).</summary>
    public required string BindAddress { get; init; }

    /// <summary>The TCP port the session's MCP HTTP server uses (when enabled).</summary>
    public required int Port { get; init; }

    /// <summary>An opaque token identifying this provisioning (returned for symmetry with teardown/audit).</summary>
    public required string Token { get; init; }

    /// <summary>The MCP result payload the agent consumes: path, launch, connect, and teardown details.</summary>
    public JsonObject ToJsonObject() {
        JsonObject obj = new() {
            ["sessionId"] = SessionId,
            ["directory"] = Directory,
            ["exePath"] = ExePath,
            ["workingDir"] = Directory,
            ["token"] = Token,
            ["launch"] = $"Run \"{ExePath}\" --mcp from workingDir \"{Directory}\" (stdio MCP), or launch it and POST JSON-RPC to the HTTP endpoint below.",
            ["mcpServerEnabled"] = McpServerEnabled,
            ["teardown"] = "Call the end-session tool with this sessionId when finished (or just delete the directory); MCEC also reaps stale session dirs on launch.",
        };
        if (McpServerEnabled) {
            obj["bindAddress"] = BindAddress;
            obj["port"] = Port;
            obj["mcpEndpoint"] = $"http://{BindAddress}:{Port}/mcp";
        }
        return obj;
    }
}
