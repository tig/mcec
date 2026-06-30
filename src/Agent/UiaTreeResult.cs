// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The result of a UIA tree snapshot (<see cref="UiaService.DumpTree"/>): the (possibly null) root
/// node, the total <see cref="NodeCount"/> captured, and whether the node cap clipped the walk
/// (<see cref="Truncated"/>). Carrying the count and truncation flag lets <c>query</c> surface a
/// <c>tree-truncated</c> warning instead of returning a silently clipped tree.
/// </summary>
public sealed record UiaTreeResult(UiaElementInfo? Root, int NodeCount, bool Truncated);
