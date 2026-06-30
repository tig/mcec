// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Mutable walk state threaded through <see cref="UiaService"/>'s tree dump: the node cap
/// (<see cref="MaxNodes"/>) and the running <see cref="Count"/>/<see cref="Truncated"/> flag.
/// </summary>
internal sealed class UiaTreeBudget {
    public int MaxNodes { get; init; } = int.MaxValue;
    public int Count { get; set; }
    public bool Truncated { get; set; }
}
