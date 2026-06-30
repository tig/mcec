// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The outcome of a finished GIF recording: the encoded animation plus the metadata an agent needs
/// (frame count, dimensions, duration). Returned by <see cref="GifRecorder.Stop"/> and surfaced in
/// the <c>record</c> command's structured result.
/// </summary>
public sealed class RecordingResult {
    /// <summary>The assembled animated GIF89a bytes (empty when <see cref="Error"/> is set).</summary>
    public byte[] Gif { get; init; } = [];

    /// <summary>Number of frames captured.</summary>
    public int Frames { get; init; }

    /// <summary>Width of the animation in pixels (largest frame).</summary>
    public int Width { get; init; }

    /// <summary>Height of the animation in pixels (largest frame).</summary>
    public int Height { get; init; }

    /// <summary>The effective (clamped) frames-per-second the recording targeted.</summary>
    public int Fps { get; init; }

    /// <summary>Wall-clock duration of the capture loop, in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Descriptor of the recorded target (window metadata or region), for the result/audit.</summary>
    public JsonNode? Target { get; init; }

    /// <summary>Non-null when the recording failed (e.g. the target window disappeared mid-record).</summary>
    public string? Error { get; init; }
}
