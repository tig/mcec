// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Structured result for the MCEC 3.0 agent commands (capture/query/find/invoke). The legacy HTPC
/// command path keeps returning opaque strings; agent commands additionally return one of these,
/// serialized as JSON, so an MCP client (or the HTTP façade) can reason over success/error + data.
///
/// The fields here track the shared result contract in <c>docs/design/agent-tool-result-contract.md</c>
/// (#101): <see cref="Success"/>→<c>ok</c>, <see cref="Data"/>→<c>result</c>, <see cref="Error"/>→
/// <c>error.detail</c>, with <see cref="ErrorCode"/>/<see cref="ErrorCategory"/> carrying the stable
/// taxonomy and <see cref="Warnings"/> the non-fatal conditions. The full envelope rename
/// (<c>ok</c>/<c>result</c>/nested <c>error</c>) lands with the session runtime (#98); these fields are
/// added additively so existing consumers that read <c>success</c>/<c>data</c>/<c>error</c> keep working.
/// </summary>
public sealed class CommandResult {
    public bool Success { get; set; }
    public string? Command { get; set; }
    public string? Error { get; set; }

    /// <summary>Stable, fine-grained error code (kebab-case); narrows <see cref="ErrorCategory"/>.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Coarse failure class from the closed taxonomy in the agent result contract
    /// (timeout, ambiguous-selector, stale-element, no-target, capture-blank, focus, elevation,
    /// foreground, internal).</summary>
    public string? ErrorCategory { get; set; }

    public JsonObject? Data { get; set; }

    /// <summary>Non-fatal conditions surfaced alongside the result; can be present on success or failure.</summary>
    public List<CommandWarning> Warnings { get; } = [];

    public static CommandResult Ok(string command, JsonObject? data = null) =>
        new() { Success = true, Command = command, Data = data };

    public static CommandResult Fail(string command, string error) =>
        new() { Success = false, Command = command, Error = error };

    /// <summary>
    /// Builds a failure result carrying the structured taxonomy (<paramref name="code"/>/
    /// <paramref name="category"/>) and, optionally, partial <paramref name="data"/>; e.g. a blank
    /// capture still returns the (suspect) image so it is not lost. <paramref name="data"/> doubles as
    /// the contract's <c>lastObservation</c>: the last good state carried forward on failure.
    /// </summary>
    public static CommandResult Fail(string command, string error, string code, string category, JsonObject? data = null) =>
        new() { Success = false, Command = command, Error = error, ErrorCode = code, ErrorCategory = category, Data = data };

    /// <summary>Appends a non-fatal warning and returns <c>this</c> for fluent chaining.</summary>
    public CommandResult Warn(string code, string detail) {
        Warnings.Add(new CommandWarning(code, detail));
        return this;
    }

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
        if (ErrorCode is not null) {
            obj["errorCode"] = ErrorCode;
        }
        if (ErrorCategory is not null) {
            obj["errorCategory"] = ErrorCategory;
        }
        if (Data is not null) {
            obj["data"] = Data.DeepClone();
        }
        if (Warnings.Count > 0) {
            JsonArray warnings = [];
            foreach (CommandWarning w in Warnings) {
                warnings.Add(new JsonObject { ["code"] = w.Code, ["detail"] = w.Detail });
            }
            obj["warnings"] = warnings;
        }
        return obj;
    }

    public string ToJson() => ToJsonObject().ToJsonString(AgentJson.Options);
}
