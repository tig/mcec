// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The result of a UIA tree snapshot (<see cref="UiaService.DumpTree"/>): the (possibly null) root
/// node, the total <see cref="NodeCount"/> captured, and whether the node cap clipped the walk
/// (<see cref="Truncated"/>). Carrying the count and truncation flag lets <c>query</c> surface a
/// <c>tree-truncated</c> warning instead of returning a silently clipped tree. <see cref="Failure"/>
/// classifies an attach-level fault (#261) so <c>query</c> can report <c>stale-element</c>/<c>elevation</c>
/// instead of an empty tree that reads as a healthy-but-blank observation.
/// </summary>
public sealed record UiaTreeResult(UiaElementInfo? Root, int NodeCount, bool Truncated,
    UiaFailureKind Failure = UiaFailureKind.None);
