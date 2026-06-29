// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Structured result for the MCEC 3.0 agent commands (capture/query/find/invoke). The legacy HTPC
/// command path keeps returning opaque strings; agent commands additionally return one of these,
/// serialized as JSON, so an MCP client (or the HTTP façade) can reason over success/error + data.
/// </summary>
public sealed class CommandResult {
    public bool Success { get; set; }
    public string? Command { get; set; }
    public string? Error { get; set; }
    public JsonObject? Data { get; set; }

    public static CommandResult Ok(string command, JsonObject? data = null) =>
        new() { Success = true, Command = command, Data = data };

    public static CommandResult Fail(string command, string error) =>
        new() { Success = false, Command = command, Error = error };

    /// <summary>Serializes this result to a compact, camelCase JSON object.</summary>
    public JsonObject ToJsonObject() {
        JsonObject obj = new() {
            ["success"] = Success,
        };
        if (Command is not null) {
            obj["command"] = Command;
        }
        if (Error is not null) {
            obj["error"] = Error;
        }
        if (Data is not null) {
            obj["data"] = Data.DeepClone();
        }
        return obj;
    }

    public string ToJson() => ToJsonObject().ToJsonString(AgentJson.Options);
}
