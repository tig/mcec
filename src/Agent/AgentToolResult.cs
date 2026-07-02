// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The single result envelope every MCEC 3.0 agent tool returns, defined by
/// <c>docs/design/agent-tool-result-contract.md</c> and <c>agent-tool-result.schema.json</c> (#101).
/// One shape, one error vocabulary, so an agent can branch on success/failure uniformly across
/// <c>capture</c>/<c>query</c>/<c>find</c>/<c>wait-for</c>/<c>invoke</c>/<c>send_command</c> and the
/// session-lifecycle tools.
///
/// A result is <b>either</b> success (<c>ok:true</c>, <see cref="Result"/> present, no
/// <see cref="Error"/>) <b>or</b> failure (<c>ok:false</c>, <see cref="Error"/> present, no
/// <see cref="Result"/>) — never both. The factory methods enforce that invariant structurally.
///
/// <para>Since #206 the envelope is built from the <see cref="CommandResult"/> OBJECT the command
/// returned (see <see cref="FromCommandResult"/>) — no serialize → re-parse round-trip, and no
/// free-text "categorization": every agent command emits the structured code/category itself
/// (enforced by <see cref="AgentCommand"/>'s sealed template). <see cref="SessionId"/> stays null
/// until the session store lands (Phase 2/3).</para>
/// </summary>
public sealed class AgentToolResult {
    private AgentToolResult(bool ok, JsonObject? result, AgentError? error, string? sessionId, IReadOnlyList<AgentWarning> warnings) {
        Ok = ok;
        Result = result;
        Error = error;
        SessionId = sessionId;
        Warnings = warnings;
    }

    /// <summary>Owning session id, or null for a stateless one-shot call.</summary>
    public string? SessionId { get; }

    /// <summary>True when the tool achieved its goal — the field an agent branches on first.</summary>
    public bool Ok { get; }

    /// <summary>Tool-specific success payload (present when <see cref="Ok"/>); null on failure.</summary>
    public JsonObject? Result { get; }

    /// <summary>Non-fatal conditions surfaced alongside the result. May be present on success or failure.</summary>
    public IReadOnlyList<AgentWarning> Warnings { get; }

    /// <summary>The failure descriptor (present only when <see cref="Ok"/> is false).</summary>
    public AgentError? Error { get; }

    /// <summary>Builds a success envelope.</summary>
    public static AgentToolResult Success(JsonObject? result, string? sessionId = null, IReadOnlyList<AgentWarning>? warnings = null) =>
        new(true, result, null, sessionId, warnings ?? []);

    /// <summary>Builds a failure envelope from an <see cref="AgentError"/>.</summary>
    public static AgentToolResult Failure(AgentError error, string? sessionId = null, IReadOnlyList<AgentWarning>? warnings = null) =>
        new(false, null, error ?? throw new ArgumentNullException(nameof(error)), sessionId, warnings ?? []);

    /// <summary>Builds a failure envelope from its parts.</summary>
    public static AgentToolResult Failure(string code, AgentErrorCategory category, string detail, string? sessionId = null) =>
        Failure(new AgentError(code, category, detail), sessionId);

    /// <summary>
    /// Builds the #101 envelope from the <see cref="CommandResult"/> OBJECT an agent command returned
    /// (#206) — the object flows through; nothing is serialized and re-parsed. Success carries
    /// <see cref="CommandResult.Data"/> forward as <see cref="Result"/> (same instance). On failure the
    /// command's structured <see cref="CommandResult.ErrorCode"/>/<see cref="CommandResult.ErrorCategory"/>
    /// become the error (they are mandatory — <see cref="AgentCommand"/> normalizes a missing pair to
    /// <c>unhandled</c>/<c>internal</c>; an unknown category string maps to <c>internal</c> so
    /// <c>error.category</c> always validates against the closed set), any failure <c>Data</c> the
    /// command kept (e.g. a blank capture's suspect PNG) rides in <c>error.partialResult</c>, and
    /// <paramref name="lastObservation"/> (the session's last good state before this call) is attached
    /// to <c>error.lastObservation</c> so the failure is debuggable without rerunning it.
    ///
    /// <para>One success is reclassified as a failure: <see cref="FindCommand"/> returns
    /// <c>Ok{found:false}</c> when a <c>wait-for</c> exhausts its timeout, but an agent branches on
    /// <c>ok</c> — so a wait that never resolved must surface as a <c>timeout</c> failure, not
    /// <c>ok:true</c>. A one-shot <c>find</c> miss stays a success ("a miss is not an error").</para>
    /// </summary>
    public static AgentToolResult FromCommandResult(CommandResult command, string toolName, string? sessionId = null, JsonObject? lastObservation = null) {
        if (command is null) {
            throw new ArgumentNullException(nameof(command));
        }

        List<AgentWarning> warnings = [];
        foreach (CommandWarning w in command.Warnings) {
            warnings.Add(new AgentWarning(w.Code, w.Detail));
        }

        if (command.Success) {
            if (IsWaitForMiss(toolName, command.Data)) {
                return Failure(
                    new AgentError("wait-condition-timeout", AgentErrorCategory.Timeout,
                        "wait-for exhausted its timeout before the element appeared.", lastObservation),
                    sessionId, warnings);
            }
            return Success(command.Data, sessionId, warnings);
        }

        string detail = command.Error ?? "The command failed without a message.";
        string code = string.IsNullOrEmpty(command.ErrorCode) ? "unhandled" : command.ErrorCode;
        AgentErrorCategory category =
            command.ErrorCategory is string wire && TryParseCategory(wire, out AgentErrorCategory parsed)
                ? parsed
                : AgentErrorCategory.Internal;
        return Failure(new AgentError(code, category, detail, lastObservation, command.Data), sessionId, warnings);
    }

    /// <summary>True when a <c>wait-for</c> returned <c>found:false</c> — i.e. it timed out.</summary>
    private static bool IsWaitForMiss(string toolName, JsonObject? data) =>
        string.Equals(toolName, "wait-for", StringComparison.OrdinalIgnoreCase)
        && data?["found"] is JsonValue fv && fv.TryGetValue(out bool found) && !found;

    /// <summary>Maps a wire category string back onto the closed taxonomy; false for an unknown string.</summary>
    private static bool TryParseCategory(string wire, out AgentErrorCategory category) {
        switch (wire) {
            case "timeout": category = AgentErrorCategory.Timeout; return true;
            case "ambiguous-selector": category = AgentErrorCategory.AmbiguousSelector; return true;
            case "stale-element": category = AgentErrorCategory.StaleElement; return true;
            case "no-target": category = AgentErrorCategory.NoTarget; return true;
            case "invalid-argument": category = AgentErrorCategory.InvalidArgument; return true;
            case "capture-blank": category = AgentErrorCategory.CaptureBlank; return true;
            case "focus": category = AgentErrorCategory.Focus; return true;
            case "elevation": category = AgentErrorCategory.Elevation; return true;
            case "foreground": category = AgentErrorCategory.Foreground; return true;
            case "internal": category = AgentErrorCategory.Internal; return true;
            default: category = AgentErrorCategory.Internal; return false;
        }
    }

    /// <summary>Serializes to the camelCase, nulls-omitted envelope object (per <see cref="AgentJson.Options"/>).</summary>
    public JsonObject ToJsonObject() {
        JsonObject obj = new() {
            ["ok"] = Ok,
        };
        if (SessionId is not null) {
            obj["sessionId"] = SessionId;
        }
        if (Ok && Result is not null) {
            obj["result"] = Result.DeepClone();
        }
        if (Warnings.Count > 0) {
            JsonArray warnings = [];
            foreach (AgentWarning w in Warnings) {
                warnings.Add(w.ToJsonObject());
            }
            obj["warnings"] = warnings;
        }
        if (!Ok && Error is not null) {
            obj["error"] = Error.ToJsonObject();
        }
        return obj;
    }

    public string ToJson() => ToJsonObject().ToJsonString(AgentJson.Options);
}
