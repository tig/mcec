// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The outcome of a <see cref="UiaService.Find"/> element lookup (#261). The old bare
/// <c>UiaElementInfo?</c> conflated four distinct outcomes into null; not found, ambiguous selector,
/// window gone, and access denied all looked identical to a caller; so an agent was told
/// <c>no-target</c> (broaden the selector!) for failures whose correct recovery is the opposite.
/// </summary>
/// <param name="Element">The single matching element, or null when none/ambiguous/failed.</param>
/// <param name="MatchCount">How many elements matched the selector on the deciding attempt; &gt; 1
/// means the selector was ambiguous and the lookup refused to guess.</param>
/// <param name="Failure">How the lookup failed, when it failed exceptionally.</param>
public sealed record UiaFindOutcome(UiaElementInfo? Element, int MatchCount, UiaFailureKind Failure) {
    /// <summary>A clean miss: nothing matched, no fault.</summary>
    public static readonly UiaFindOutcome NotFound = new(null, 0, UiaFailureKind.None);

    /// <summary>True when the selector matched more than one element and the lookup refused to guess.</summary>
    public bool Ambiguous => MatchCount > 1;
}
