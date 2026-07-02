// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Base class for every MCEC 3.0 agent command (<c>capture</c>, <c>query</c>, <c>find</c>/<c>wait-for</c>,
/// <c>invoke</c>, <c>click</c>, <c>drag</c>, <c>record</c>, <c>displays</c>, <c>launch</c>). Its sealed
/// <see cref="Execute"/> template method makes the <see cref="AgentRuntime.AgentCommandsEnabled"/>
/// security gate STRUCTURAL: a subclass implements <see cref="ExecuteCore"/> and can only ever run
/// after the gate has passed — forgetting the gate is impossible by construction (issue #208).
///
/// RESULTS (#206): <see cref="ExecuteCore"/> returns the command's <see cref="CommandResult"/> as an
/// OBJECT; the template emits it once per transport. Over the legacy TCP/serial pipeline it writes
/// <c>result.ToJson()</c> to <see cref="Command.Reply"/> exactly as before; for the MCP path it hands
/// the object to <see cref="CapturingReply.Result"/> so the server never re-parses its own output.
/// The template also normalizes failures: a failure without a structured code/category is stamped
/// <c>unhandled</c>/<c>internal</c>, so every agent failure envelope is categorical by construction
/// — there is no free-text-only failure for prose-sniffing to interpret.
///
/// SECURITY — why the gate must live here and not (only) in the server: the MCP path checks
/// <c>AgentCommandsEnabled</c> in <c>AgentServer</c> before dispatch, but agent commands are ordinary
/// <see cref="Command"/>s reachable over the legacy TCP/serial pipeline too, and that pipeline has NO
/// server-side agent gate. For those transports this in-command check is the ONLY thing standing
/// between a flipped per-command <c>Enabled</c> flag and full agent observation/actuation. Under the
/// old copy-paste pattern, a new agent command whose author forgot the paste was silently exposed
/// over TCP — nothing caught it. <c>AgentCommandStructuralGateTests</c> asserts every agent tool maps
/// to a subclass of this type.
/// </summary>
public abstract class AgentCommand : Command {
    /// <summary>
    /// Sealed gate + audit + dispatch + emit template. Order (identical to the pre-#208 hand-written
    /// bodies): the <see cref="Command.Execute"/> per-command <c>Enabled</c> check and telemetry,
    /// then the <see cref="AgentRuntime.AgentCommandsEnabled"/> opt-in gate (fail-closed with the
    /// same structured <see cref="CommandResult"/> the commands have always emitted), then the
    /// <see cref="AgentRuntime.Audit"/> line (when <see cref="AuditDetails"/> supplies one), then
    /// <see cref="ExecuteCore"/>, then a single result emission (see the class remarks).
    /// </summary>
    public sealed override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        CommandResult result;
        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            result = CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).",
                "agent-commands-disabled", "internal");
        }
        else {
            string? auditDetails = AuditDetails();
            if (auditDetails is not null) {
                AgentRuntime.Audit(Cmd, auditDetails);
            }
            result = ExecuteCore();
        }

        // Structural guarantee (#206): every agent failure carries the closed taxonomy. A command
        // that slipped a bare-string Fail through still produces a categorical envelope.
        if (!result.Success) {
            result.ErrorCode ??= "unhandled";
            result.ErrorCategory ??= "internal";
        }

        if (Reply is CapturingReply capturing) {
            // In-process (MCP) dispatch: hand the OBJECT over; the server consumes it directly and
            // CapturingReply.Captured serializes lazily if legacy text is ever wanted.
            capturing.Result = result;
        }
        else {
            // Legacy TCP/serial transport: the same JSON line these commands have always written.
            Reply?.WriteLine(result.ToJson());
        }

        return result.Success;
    }

    /// <summary>
    /// The <c>AGENT-AUDIT:</c> detail logged immediately after the gate passes, or <c>null</c> when
    /// the command audits later itself (e.g. <c>capture</c>/<c>record</c>/<c>launch</c> audit after
    /// branch-specific validation, with branch-specific detail).
    /// </summary>
    protected virtual string? AuditDetails() => null;

    /// <summary>
    /// The command body. Runs only when the per-command <c>Enabled</c> flag AND the
    /// <see cref="AgentRuntime.AgentCommandsEnabled"/> opt-in are both set. Returns the command's
    /// structured result — the template owns emitting it (never write it to <see cref="Command.Reply"/>
    /// here). Failures should carry a structured code/category (<see
    /// cref="CommandResult.Fail(string, string, string, string, System.Text.Json.Nodes.JsonObject?)"/>).
    /// </summary>
    protected abstract CommandResult ExecuteCore();
}
