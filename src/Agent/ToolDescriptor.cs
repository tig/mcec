// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Everything the agent surface knows about one gated agent tool, in one record (#205). Before this,
/// per-tool knowledge was scattered across ~8 hand-synced sites; the <c>tools/list</c> schema builder,
/// the tools/call gate whitelist, the arg-mapping switch, <c>SerializesOnInputLock</c>,
/// <c>AgentSession.IsObservationTool</c>, <c>CommandTersifier.ForAgentTool</c>, and
/// <c>SessionProvisioner</c>'s default/enabled command lists; so adding a tool was shotgun surgery
/// and drift was routine (the tersifier had already lost <c>record</c>/<c>launch</c>). Those sites are
/// now lookups into <see cref="ToolCatalog"/>, which holds one of these per tool.
/// </summary>
public sealed record ToolDescriptor {
    /// <summary>The MCP tool name (also the command name in the loaded command table).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Builds the tool's complete <c>tools/list</c> entry (<c>name</c>/<c>description</c>/
    /// <c>inputSchema</c>). A factory rather than a cached object because a <see cref="JsonObject"/>
    /// can only be parented into one document at a time.
    /// </summary>
    public required Func<JsonObject> BuildSchema { get; init; }

    /// <summary>
    /// Maps the MCP <c>tools/call</c> arguments onto a populated <see cref="Command"/> instance
    /// (the #201 exhaustive mapping; unknown names never reach a descriptor).
    /// </summary>
    public required Func<JsonObject, Command> BuildCommand { get; init; }

    /// <summary>
    /// Creates a bare, correctly-typed <see cref="Command"/> instance for serialization into a
    /// provisioned session's <c>mcec.commands</c> (see <see cref="SessionProvisioner"/>). Separate from
    /// <see cref="BuildCommand"/> so the serialized command carries the type's own property defaults,
    /// not the tool-call argument defaults.
    /// </summary>
    public required Func<Command> CreateCommandInstance { get; init; }

    /// <summary>
    /// Builds the condensed one-line overlay/log label body for a call's arguments (#119). The shared
    /// outcome suffix (<c>…</c> / <c>→ failed</c>) is appended by
    /// <see cref="CommandTersifier.ForAgentTool"/>.
    /// </summary>
    public required Func<JsonObject, string> Tersify { get; init; }

    /// <summary>
    /// Whether the tool serializes on the shared input gate (<see cref="AgentRuntime.InputGate"/>; the
    /// #113 contract) because it synthesizes global physical mouse/keyboard input directly on its MCP
    /// worker. Note <c>send_command</c> is a meta-tool outside the catalog: it serializes INDIRECTLY
    /// via the <see cref="CommandInvoker"/> dispatcher thread (#195) and is special-cased in
    /// <see cref="AgentServer.SerializesOnInputLock"/>.
    /// </summary>
    public bool SerializesOnInput { get; init; }

    /// <summary>
    /// Whether a successful call is an observation whose payload the ambient session records as
    /// <see cref="AgentSession.LastObservation"/> (query/capture/find/wait-for).
    /// </summary>
    public bool IsObservation { get; init; }

    /// <summary>
    /// Whether a provisioned session (#138) enables this command by default; and at all: a tool
    /// without this flag (today only <c>launch</c>) is not provisionable even by explicit request,
    /// matching <see cref="SessionProvisioner"/>'s historical command set.
    /// </summary>
    public bool ProvisionedByDefault { get; init; }

    /// <summary>
    /// Whether the tool is still served while an operator consent prompt is open (#307). Default
    /// FALSE; fail closed: only pure observation may run during the consent freeze, so anything that
    /// could actuate, mutate state, or mint capability (and every meta-tool, which is not in the
    /// catalog at all) is refused with <c>consent-pending</c> and can never reach, or help answer,
    /// the agent's own prompt. A new tool must opt IN to being served, never out of the freeze.
    /// </summary>
    public bool ServedDuringConsent { get; init; }
}
