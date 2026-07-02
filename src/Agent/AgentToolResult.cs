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
/// <para>Phase 1 (#86) wires this in at the <see cref="AgentServer"/> boundary by translating the
/// legacy <see cref="CommandResult"/> each command still emits (see <see cref="FromLegacy"/>). The
/// per-tool epics (#87–#91) will have the commands emit native categories, warnings, and
/// <c>lastObservation</c> directly. <see cref="SessionId"/> stays null until the session store lands
/// (Phase 2/3).</para>
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
    /// Translates a legacy <see cref="CommandResult"/> JSON object
    /// (<c>{ success, command, error, errorCode?, errorCategory?, data?, warnings? }</c>) — what each agent
    /// command writes to its reply today — into the #101 envelope. Success carries <c>data</c> forward as
    /// <see cref="Result"/>. On failure, the command's own structured <c>errorCategory</c>/<c>errorCode</c>
    /// (e.g. <c>capture-blank</c>) are preserved when present; only a bare free-text error is mapped onto
    /// the closed taxonomy via <see cref="Categorize"/> (the Phase 1 shim). Legacy <c>warnings</c> are
    /// carried through on success or failure. When a failure has no observation of its own,
    /// <paramref name="lastObservation"/> (the session's last good state before this call) is attached to
    /// <c>error.lastObservation</c> so the failure is debuggable without rerunning it.
    ///
    /// <para>One success is reclassified as a failure: <see cref="FindCommand"/> writes
    /// <c>Ok{found:false}</c> when a <c>wait-for</c> exhausts its timeout, but an agent branches on
    /// <c>ok</c> — so a wait that never resolved must surface as a <c>timeout</c> failure, not
    /// <c>ok:true</c>. A one-shot <c>find</c> miss stays a success ("a miss is not an error").</para>
    /// </summary>
    public static AgentToolResult FromLegacy(JsonObject legacy, string toolName, string? sessionId = null, JsonObject? lastObservation = null) {
        IReadOnlyList<AgentWarning> warnings = ReadWarnings(legacy);
        bool success = legacy["success"] is JsonValue sv && sv.TryGetValue(out bool b) && b;
        if (success) {
            JsonObject? data = (legacy["data"] as JsonObject)?.DeepClone() as JsonObject;
            if (IsWaitForMiss(toolName, data)) {
                return Failure(
                    new AgentError("wait-condition-timeout", AgentErrorCategory.Timeout,
                        "wait-for exhausted its timeout before the element appeared.", lastObservation),
                    sessionId, warnings);
            }
            return Success(data, sessionId, warnings);
        }

        string message = LegacyString(legacy, "error") ?? "The command failed without a message.";

        // Prefer the structured taxonomy the command already emitted (e.g. capture-blank / frame-all-black)
        // over re-deriving it from free text. Only fall back to Categorize when the command wrote a bare
        // error string with no code/category (the Phase 1 shim path).
        AgentError error;
        string? code = LegacyString(legacy, "errorCode");
        if (code is not null && LegacyString(legacy, "errorCategory") is string wire && TryParseCategory(wire, out AgentErrorCategory category)) {
            error = new AgentError(code, category, message, lastObservation);
        }
        else {
            error = Categorize(toolName, message);
            if (lastObservation is not null) {
                error = new AgentError(error.Code, error.Category, error.Detail, lastObservation);
            }
        }
        return Failure(error, sessionId, warnings);
    }

    /// <summary>True when a <c>wait-for</c> returned <c>found:false</c> — i.e. it timed out.</summary>
    private static bool IsWaitForMiss(string toolName, JsonObject? data) =>
        string.Equals(toolName, "wait-for", StringComparison.OrdinalIgnoreCase)
        && data?["found"] is JsonValue fv && fv.TryGetValue(out bool found) && !found;

    /// <summary>Reads a string property from a legacy result object, or null if absent/non-string.</summary>
    private static string? LegacyString(JsonObject legacy, string key) =>
        legacy[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

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

    /// <summary>Carries the legacy result's <c>warnings</c> (<c>{ code, detail }</c>) into the envelope.</summary>
    private static IReadOnlyList<AgentWarning> ReadWarnings(JsonObject legacy) {
        if (legacy["warnings"] is not JsonArray arr || arr.Count == 0) {
            return [];
        }
        List<AgentWarning> list = [];
        foreach (JsonNode? n in arr) {
            if (n is JsonObject w && LegacyString(w, "code") is string code && LegacyString(w, "detail") is string detail) {
                list.Add(new AgentWarning(code, detail));
            }
        }
        return list;
    }

    /// <summary>
    /// Maps a legacy free-text command error onto the closed <see cref="AgentErrorCategory"/> taxonomy.
    /// This is the Phase 1 translation shim: the known error strings are pinned by the command-level
    /// tests, so the mapping is stable. Unrecognized messages fall back to <c>internal</c> (not
    /// agent-recoverable; surface to the operator), per the contract's recovery guidance.
    /// </summary>
    public static AgentError Categorize(string toolName, string message) {
        string m = message ?? "";

        if (m.Contains("No matching window", StringComparison.OrdinalIgnoreCase)) {
            return new AgentError("window-not-found", AgentErrorCategory.NoTarget, m);
        }
        if (m.Contains("Invoke failed", StringComparison.OrdinalIgnoreCase)) {
            // The element was not found or its pattern is unsupported; agent recovery is to re-query/
            // re-find a fresh target, which is exactly the no-target recovery path.
            return new AgentError("element-not-found", AgentErrorCategory.NoTarget, m);
        }
        if (m.Contains("Capture failed", StringComparison.OrdinalIgnoreCase)) {
            return new AgentError("capture-exception", AgentErrorCategory.Internal, m);
        }
        if (m.Contains("disabled", StringComparison.OrdinalIgnoreCase)) {
            return new AgentError("agent-commands-disabled", AgentErrorCategory.Internal, m);
        }
        if (m.Contains("produced no output", StringComparison.OrdinalIgnoreCase)) {
            return new AgentError("no-output", AgentErrorCategory.Internal, m);
        }
        return new AgentError("unhandled", AgentErrorCategory.Internal, m);
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
