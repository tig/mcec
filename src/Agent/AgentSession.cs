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
/// <para>Phase 2 (#86) introduced a single <b>ambient</b> session; Phase 3 adds explicit
/// <c>session-start</c>/<c>session-status</c>/<c>session-end</c> lifecycle tools and per-call routing, so
/// there can now be several addressable sessions alongside the implicit default (see
/// <see cref="AgentRuntime.StartSession"/>/<see cref="AgentRuntime.TryResolveSession"/>). The state
/// recorded here is what feeds <c>sessionId</c> on every result, <c>error.lastObservation</c> on
/// failures, and (later) #87's evidence bundle.</para>
///
/// All mutators are guarded by an internal lock because an <c>invoke</c> can finish on a background
/// worker thread after the dispatching call has already returned.
/// </summary>
public sealed class AgentSession {
    private readonly Lock _gate = new();
    private readonly string _artifactRoot;

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
    /// <c>New-McecSession</c> format) and a reserved (not yet created) artifact directory path under
    /// <paramref name="artifactRoot"/>.
    /// </summary>
    public static AgentSession Create(string artifactRoot) =>
        new(Guid.NewGuid().ToString("N")[..12], DateTime.UtcNow, artifactRoot);

    /// <summary>Stable identifier carried on every result that ran inside this session.</summary>
    public string SessionId { get; }

    /// <summary>When the session was created (UTC).</summary>
    private DateTime StartedAtUtc { get; }

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
                return field ??= Path.Combine(
                    _artifactRoot,
                    $"{StartedAtUtc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)}-{SessionId}");
            }
        }
    }

    /// <summary>
    /// Records a successful observation and, when present, the target window it concerns.
    ///
    /// <para>PAYLOAD BOMB DEFUSED (#215): a <c>capture</c> observation carries the full base64 PNG.
    /// The old code deep-cloned it into session state per capture and re-cloned it per read, and any
    /// LATER failure then embedded megabytes of stale screenshot into <c>error.lastObservation</c>.
    /// Now an image-bearing observation is compacted BEFORE storage: the PNG bytes are written to a
    /// file under the per-session artifact directory and the session remembers only a summary
    /// (window descriptor, dimensions, blankCheck verdict, byte count) plus the artifact path; so
    /// <c>error.lastObservation</c> never carries raw base64. Non-image observations (query trees,
    /// find results) are stored as before.</para>
    /// </summary>
    public void RecordObservation(JsonObject? observation, JsonObject? target = null) {
        // Compact (and write the artifact) OUTSIDE the lock; file IO must never hold up other
        // recorders/readers.
        JsonObject? compact = observation is null ? null : CompactObservation(observation);
        lock (_gate) {
            if (compact is not null) {
                _lastObservation = compact;
            }
            if (target is not null) {
                _activeTarget = target.DeepClone() as JsonObject;
            }
        }
    }

    /// <summary>
    /// Returns the session-state form of <paramref name="observation"/>: a clone for ordinary
    /// payloads, or; when it carries inline image bytes (<c>base64</c>, i.e. a capture); a compact
    /// summary with the bytes swapped for an artifact file path.
    /// </summary>
    private JsonObject? CompactObservation(JsonObject observation) {
        if (observation["base64"] is not JsonValue b64 || !b64.TryGetValue(out string? base64) || string.IsNullOrEmpty(base64)) {
            return observation.DeepClone() as JsonObject;
        }

        JsonObject summary = new() { ["kind"] = "capture-summary" };
        // The compact fields the contract names: window descriptor, dimensions, blankCheck verdict,
        // byte count; plus the small metadata capture already reports (encoding, optional file/handle).
        foreach (string key in (string[])["window", "handle", "width", "height", "encoding", "bytes", "blankCheck", "file"]) {
            if (observation[key] is { } node) {
                summary[key] = node.DeepClone();
            }
        }
        string extension = observation["encoding"] is JsonValue ev && ev.TryGetValue(out string? enc) && !string.IsNullOrEmpty(enc)
            ? enc.ToLowerInvariant()
            : "png";
        if (TryWriteArtifact(base64, extension) is { } artifact) {
            summary["artifact"] = artifact;
        }
        else {
            summary["artifactError"] = "The image bytes could not be written to the session artifact directory; only this summary was retained.";
        }
        return summary;
    }

    private int _artifactCounter;

    /// <summary>
    /// Writes an observation's image bytes to a fresh file in the per-session artifact directory
    /// (<see cref="EnsureArtifactDir"/>) and returns its path, or null on any decode/IO failure;
    /// recording an observation must never throw.
    /// </summary>
    private string? TryWriteArtifact(string base64, string extension) {
        try {
            byte[] bytes = Convert.FromBase64String(base64);
            string dir = EnsureArtifactDir();
            string name = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Interlocked.Increment(ref _artifactCounter)}.{extension}");
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException) {
            Logger.Instance.Log4.Warn($"AgentSession: could not write observation artifact: {e.Message}");
            return null;
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
    /// observation. Centralizing the decision keeps every observation tool (wait-for included) consistent.
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

    /// <summary>The observation set is the <see cref="ToolDescriptor.IsObservation"/> flag in the catalog (#205).</summary>
    private static bool IsObservationTool(string toolName) =>
        ToolCatalog.TryGet(toolName, out ToolDescriptor descriptor) && descriptor.IsObservation;

    /// <summary>Creates the per-session artifact directory if it does not yet exist and returns its path.</summary>
    public string EnsureArtifactDir() {
        string dir = ArtifactDir;
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>A debug/replay snapshot of the session's current state (the payload of the <c>session-status</c> tool, #86 Phase 3).</summary>
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
