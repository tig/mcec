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
    /// Sealed gate + audit + dispatch template. Order (identical to the pre-#208 hand-written
    /// bodies): the <see cref="Command.Execute"/> per-command <c>Enabled</c> check and telemetry,
    /// then the <see cref="AgentRuntime.AgentCommandsEnabled"/> opt-in gate (fail-closed with the
    /// same structured <see cref="CommandResult"/> the commands have always emitted), then the
    /// <see cref="AgentRuntime.Audit"/> line (when <see cref="AuditDetails"/> supplies one), then
    /// <see cref="ExecuteCore"/>.
    /// </summary>
    public sealed override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }

        string? auditDetails = AuditDetails();
        if (auditDetails is not null) {
            AgentRuntime.Audit(Cmd, auditDetails);
        }

        return ExecuteCore();
    }

    /// <summary>
    /// The <c>AGENT-AUDIT:</c> detail logged immediately after the gate passes, or <c>null</c> when
    /// the command audits later itself (e.g. <c>capture</c>/<c>record</c>/<c>launch</c> audit after
    /// branch-specific validation, with branch-specific detail).
    /// </summary>
    protected virtual string? AuditDetails() => null;

    /// <summary>
    /// The command body. Runs only when the per-command <c>Enabled</c> flag AND the
    /// <see cref="AgentRuntime.AgentCommandsEnabled"/> opt-in are both set.
    /// </summary>
    protected abstract bool ExecuteCore();
}
