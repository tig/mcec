// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The server-side runtime state for one agent automation session (#86). Where the legacy
/// <see cref="CommandResult"/> made every tool call stateless, a session gives the runtime an identity
/// (<see cref="SessionId"/>), a per-session artifact directory, and a memory of the most recent
/// target/observation/action/error so a multi-step task is one durable, debuggable record.
///
/// <para>Phase 2 (#86) introduces a single <b>ambient</b> session owned by
/// <see cref="AgentRuntime.Session"/>; explicit <c>session/start|status|end</c> lifecycle tools and
/// per-call session routing are Phase 3. The state recorded here is what feeds <c>sessionId</c> on
/// every result, <c>error.lastObservation</c> on failures, and (later) #87's evidence bundle.</para>
///
/// All mutators are guarded by an internal lock because an <c>invoke</c> can finish on a background
/// worker thread after the dispatching call has already returned.
/// </summary>
public sealed class AgentSession {
    private readonly object _gate = new();
    private readonly string _artifactRoot;
    private string? _artifactDir;

    private JsonObject? _activeTarget;
    private JsonObject? _lastObservation;
    private string? _lastAction;
    private JsonObject? _lastError;
    private DateTime? _emergencyStopAtUtc;
    private string? _emergencyStopSource;

    private AgentSession(string sessionId, DateTime startedAtUtc, string artifactRoot) {
        SessionId = sessionId;
        StartedAtUtc = startedAtUtc;
        _artifactRoot = artifactRoot;
    }

    /// <summary>
    /// Creates a session with a fresh 12-hex-char id (matching the evidence harness'
    /// <c>New-McecSession</c> format) and a reserved; not yet created; artifact directory path under
    /// <paramref name="artifactRoot"/>.
    /// </summary>
    public static AgentSession Create(string artifactRoot) =>
        new(Guid.NewGuid().ToString("N")[..12], DateTime.UtcNow, artifactRoot);

    /// <summary>Stable identifier carried on every result that ran inside this session.</summary>
    public string SessionId { get; }

    /// <summary>When the session was created (UTC).</summary>
    public DateTime StartedAtUtc { get; }

    /// <summary>The window the session is currently operating on, or null before the first target resolves.</summary>
    public JsonObject? ActiveTarget {
        get { lock (_gate) { return _activeTarget?.DeepClone() as JsonObject; } }
    }

    /// <summary>The most recent good observation (a query/capture/find payload), or null.</summary>
    public JsonObject? LastObservation {
        get { lock (_gate) { return _lastObservation?.DeepClone() as JsonObject; } }
    }

    /// <summary>The name of the most recently dispatched tool, or null.</summary>
    public string? LastAction {
        get { lock (_gate) { return _lastAction; } }
    }

    /// <summary>The most recent error envelope (its <c>error</c> object), or null.</summary>
    public JsonObject? LastError {
        get { lock (_gate) { return _lastError?.DeepClone() as JsonObject; } }
    }

    /// <summary>The reserved per-session artifact directory path (created on demand by <see cref="EnsureArtifactDir"/>).</summary>
    public string ArtifactDir {
        get {
            lock (_gate) {
                return _artifactDir ??= Path.Combine(
                    _artifactRoot,
                    $"{StartedAtUtc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)}-{SessionId}");
            }
        }
    }

    /// <summary>Records a successful observation and, when present, the target window it concerns.</summary>
    public void RecordObservation(JsonObject? observation, JsonObject? target = null) {
        lock (_gate) {
            if (observation is not null) {
                _lastObservation = observation.DeepClone() as JsonObject;
            }
            if (target is not null) {
                _activeTarget = target.DeepClone() as JsonObject;
            }
        }
    }

    /// <summary>Records the name of the tool just dispatched.</summary>
    public void RecordAction(string action) {
        lock (_gate) {
            _lastAction = action;
        }
    }

    /// <summary>Records the most recent failure's error object.</summary>
    public void RecordError(JsonObject error) {
        lock (_gate) {
            _lastError = error.DeepClone() as JsonObject;
        }
    }

    /// <summary>
    /// Stamps the operator emergency stop (#135) into the session; who/what triggered it and when; so a
    /// run's evidence bundle shows that a human halted it. Only the first stop of a latched span is kept.
    /// </summary>
    public void RecordEmergencyStop(string source, DateTime atUtc) {
        lock (_gate) {
            _emergencyStopAtUtc ??= atUtc;
            _emergencyStopSource ??= source;
        }
    }

    /// <summary>Clears the recorded emergency stop when the operator re-arms.</summary>
    public void ClearEmergencyStop() {
        lock (_gate) {
            _emergencyStopAtUtc = null;
            _emergencyStopSource = null;
        }
    }

    /// <summary>
    /// Records the outcome of a tool call: a successful <b>observation</b> (query/capture/find/wait-for)
    /// updates <see cref="LastObservation"/> and, when the payload names a window, <see cref="ActiveTarget"/>;
    /// a failure updates <see cref="LastError"/>. Actuation tools (invoke/send_command) don't record an
    /// observation. Centralizing the decision keeps every observation tool; wait-for included; consistent.
    /// </summary>
    public void RecordToolOutcome(string toolName, AgentToolResult env) {
        if (env.Ok) {
            if (IsObservationTool(toolName)) {
                RecordObservation(env.Result, env.Result?["window"] as JsonObject);
            }
        }
        else if (env.Error is not null) {
            RecordError(env.Error.ToJsonObject());
        }
    }

    private static bool IsObservationTool(string toolName) =>
        toolName is "query" or "capture" or "find" or "wait-for";

    /// <summary>Creates the per-session artifact directory if it does not yet exist and returns its path.</summary>
    public string EnsureArtifactDir() {
        string dir = ArtifactDir;
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>A debug/replay snapshot of the session's current state (the basis for <c>session/status</c>, Phase 3).</summary>
    public JsonObject ToStatusJson() {
        lock (_gate) {
            JsonObject obj = new() {
                ["sessionId"] = SessionId,
                ["startedAt"] = StartedAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                ["artifactDir"] = ArtifactDir,
            };
            if (_activeTarget is not null) {
                obj["activeTarget"] = _activeTarget.DeepClone();
            }
            if (_lastAction is not null) {
                obj["lastAction"] = _lastAction;
            }
            if (_lastObservation is not null) {
                obj["lastObservation"] = _lastObservation.DeepClone();
            }
            if (_lastError is not null) {
                obj["lastError"] = _lastError.DeepClone();
            }
            if (_emergencyStopAtUtc is not null) {
                obj["emergencyStop"] = new JsonObject {
                    ["at"] = _emergencyStopAtUtc.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    ["source"] = _emergencyStopSource,
                };
            }
            return obj;
        }
    }
}
