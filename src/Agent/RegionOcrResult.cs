// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>Outcome of <see cref="RegionOcr"/> recognizing text in a bitmap region (#331).</summary>
/// <param name="Text">Joined recognized text (lines separated by newlines).</param>
/// <param name="LineCount">Number of recognized lines.</param>
/// <param name="WordCount">Number of recognized words across all lines.</param>
/// <param name="Language">BCP-47 language tag of the OCR engine used, when available.</param>
public record RegionOcrResult(string Text, int LineCount, int WordCount, string? Language);