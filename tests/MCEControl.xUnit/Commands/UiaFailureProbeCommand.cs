// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Concrete <see cref="WindowTargetingAgentCommand"/> exposing the protected static #261 UIA-failure
/// mappers so <see cref="WindowTargetingAgentCommandTests"/> can pin them without a live UIA tree.
/// </summary>
public sealed class UiaFailureProbeCommand : WindowTargetingAgentCommand {
    protected override string AuditDetails() => "probe";
    protected override CommandResult ExecuteCore(WindowInfo? target) => CommandResult.Ok(Cmd, null);

    public static CommandResult? FindFailure(string cmd, string by, string value, UiaFindOutcome outcome) =>
        UiaFindFailureFor(cmd, by, value, outcome);

    public static CommandResult? Failure(string cmd, UiaFailureKind kind) => UiaFailureFor(cmd, kind);

    public static (int X, int Y) OffsetPoint((int X, int Y) point, (int X, int Y) origin) =>
        OffsetByWindowOrigin(point, origin);
}
