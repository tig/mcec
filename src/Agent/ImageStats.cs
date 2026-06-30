// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Blank/near-uniform content statistics for a captured frame. <see cref="DominantFraction"/> is the
/// share of sampled pixels occupying the single most common (quantized) color; a real app window is
/// busy and scores low, while a failed <c>PrintWindow</c> grab is a flat fill and scores ~1.0.
/// <see cref="IsBlank"/> is the thresholded verdict; <see cref="DominantIsDark"/> distinguishes the
/// classic all-black failure from a legitimately empty (e.g. white) surface.
/// </summary>
public readonly record struct ImageStats(bool IsBlank, double DominantFraction, bool DominantIsDark);
