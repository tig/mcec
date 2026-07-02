// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Test-only <see cref="AgentCommand"/> whose body returns whatever <see cref="Producer"/> supplies,
/// so tests can drive the sealed Execute template (#208/#206); gate, normalization, and result
/// emission; without touching the desktop.
/// </summary>
internal sealed class StubResultAgentCommand : AgentCommand {
    public Func<CommandResult> Producer { get; set; } = null!;

    protected override CommandResult ExecuteCore() => Producer();
}
