// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The clamped, effective recording parameters <see cref="RecordCommand"/> hands to
/// <see cref="GifRecorder"/> after applying the operator's <see cref="AppSettings"/> limits.
/// </summary>
internal sealed class RecordLimits {
    public int Fps { get; init; }
    public int MaxFrames { get; init; }
    public int MaxWidth { get; init; }
    public long LoopDurationMs { get; init; }
}
