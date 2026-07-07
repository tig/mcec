// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Threading;

namespace MCEControl;

/// <summary>
/// The in-memory consent state behind the <c>request-command-access</c> meta-tool (#307): whether a
/// prompt is currently on screen (single-flight), the operator's standing "allow any later requests"
/// grant, and the sticky per-command denies. All of it is process-lifetime only, on purpose:
/// a grant is never written to <c>mcec.commands</c>/<c>mcec.settings</c>, so a leaked provisioned
/// directory never carries widened permissions and a respawned instance resets to its provisioned
/// defaults and must ask again.
///
/// <para>SECURITY: consent must be OUT-OF-BAND from the agent. The prompt is MCEC's own dialog on the
/// operator's desktop, reached through <see cref="Prompter"/> (registered by the GUI host and the
/// headless operator UI); it is deliberately NOT MCP elicitation, which would route the question
/// through the agent's own client, i.e. through the party being constrained. With no prompter
/// registered (no interactive operator surface) the tool fails closed with
/// <c>consent-unavailable</c>.</para>
/// </summary>
public static class AgentConsent {
    /// <summary>
    /// How long the consent prompt waits for the operator before closing itself as a deny
    /// (<see cref="CommandAccessDecision.TimedOut"/>). The prompt must not outlive its tool call:
    /// a late "Allow" landing after the agent already received consent-timeout would be a silent
    /// grant nobody is waiting on.
    /// </summary>
    public const int PromptTimeoutMs = 120_000;

    /// <summary>Upper bound on commands per request, so one prompt stays operator-readable.</summary>
    public const int MaxCommandsPerRequest = 8;

    /// <summary>Upper bound on the agent's stated reason; anything longer is truncated before display.</summary>
    public const int MaxReasonLength = 300;

    private static readonly Lock _gate = new();
    private static bool _pending;
    private static bool _anyCommandGrant;
    private static readonly HashSet<string> _denied = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _granted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The operator-prompt channel, registered by whichever host has an operator surface
    /// (GUI: <c>MainWindow</c>; headless: <see cref="HeadlessOperatorUi"/>). Shows the modal
    /// <see cref="CommandAccessConsentDialog"/> on the operator's UI thread and blocks the calling
    /// (MCP worker) thread for the decision. Returns null when the prompt cannot be shown (no
    /// interactive desktop, host torn down), which the executor reports as <c>consent-unavailable</c>;
    /// fail closed, never fail open. Null when no host registered one (tests, early startup).
    /// </summary>
    public static Func<CommandAccessRequest, CommandAccessDecision?>? Prompter { get; set; }

    /// <summary>
    /// True while a consent prompt is on screen. <see cref="AgentToolExecutor"/> refuses every
    /// actuation-capable tool call while this is set (<c>consent-pending</c>), which; together with
    /// the <see cref="AgentRuntime.InputGate"/> hold around the prompt and the dialog registering
    /// itself as a never-a-target window; is what keeps the agent from answering its own prompt.
    /// </summary>
    public static bool IsPending {
        get {
            lock (_gate) {
                return _pending;
            }
        }
    }

    /// <summary>True once the operator chose "allow these and any later requests" (this process only).</summary>
    public static bool AnyCommandGrantActive {
        get {
            lock (_gate) {
                return _anyCommandGrant;
            }
        }
    }

    /// <summary>
    /// Whether the operator already denied <paramref name="commandName"/> this process. A deny is
    /// STICKY: re-asking returns <c>consent-denied</c> without a prompt, so an agent cannot nag the
    /// operator into approval (consent fatigue is how consent systems actually fail).
    /// </summary>
    public static bool IsDenied(string commandName) {
        lock (_gate) {
            return _denied.Contains(commandName);
        }
    }

    /// <summary>
    /// Records a table key the operator's consent enabled, so persistence paths can shield it:
    /// <see cref="CommandInvoker.Save"/> serializes a consent-granted command as DISABLED, keeping
    /// the dialog's "nothing is written to any config file" promise even when the operator later
    /// saves the Commands window (#308 review). Process-lifetime, like the grant itself.
    /// </summary>
    internal static void RecordGrantedKey(string commandKey) {
        lock (_gate) {
            _ = _granted.Add(commandKey);
        }
    }

    /// <summary>
    /// Whether <paramref name="commandKey"/> was enabled by operator consent this process. A true
    /// here means the enable is in-memory-only by contract and must never be persisted as enabled.
    /// </summary>
    public static bool WasGrantedByConsent(string? commandKey) {
        if (string.IsNullOrEmpty(commandKey)) {
            return false;
        }
        lock (_gate) {
            return _granted.Contains(commandKey);
        }
    }

    /// <summary>
    /// Claims the single prompt slot. False means another consent prompt is already on screen
    /// (the caller reports <c>consent-pending</c>); one dialog at a time, so a burst of requests can
    /// never stack prompts. Pair with <see cref="EndPrompt"/> in a finally.
    /// </summary>
    internal static bool TryBeginPrompt() {
        lock (_gate) {
            if (_pending) {
                return false;
            }
            _pending = true;
            return true;
        }
    }

    /// <summary>Releases the single prompt slot claimed by <see cref="TryBeginPrompt"/>.</summary>
    internal static void EndPrompt() {
        lock (_gate) {
            _pending = false;
        }
    }

    /// <summary>
    /// Records the operator's decision: <see cref="CommandAccessDecision.AllowAny"/> arms the
    /// standing grant; <see cref="CommandAccessDecision.Denied"/> makes each requested command
    /// sticky-denied. A timeout records nothing; it is the operator not answering, not the operator
    /// deciding, so the agent may ask again later.
    /// </summary>
    internal static void RecordDecision(CommandAccessDecision decision, IEnumerable<string> commands) {
        lock (_gate) {
            switch (decision) {
                case CommandAccessDecision.AllowAny:
                    _anyCommandGrant = true;
                    break;
                case CommandAccessDecision.Denied:
                    foreach (string name in commands) {
                        _ = _denied.Add(name);
                    }
                    break;
                case CommandAccessDecision.TimedOut:
                case CommandAccessDecision.AllowRequested:
                default:
                    break;
            }
        }
    }

    /// <summary>Drops all consent state (pending flag, standing grant, sticky denies, granted keys). Tests only.</summary>
    internal static void ResetForTests() {
        lock (_gate) {
            _pending = false;
            _anyCommandGrant = false;
            _denied.Clear();
            _granted.Clear();
        }
    }
}
