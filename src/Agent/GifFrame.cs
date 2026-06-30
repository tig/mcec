// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// A standalone GIF frame parsed by <see cref="GifEncoder"/>: its dimensions, color table, and raw
/// LZW image-data block (the min-code-size byte + data sub-blocks + terminator), ready to be
/// re-emitted as one frame of an animation.
/// </summary>
internal sealed class GifFrame {
    public int Width { get; init; }
    public int Height { get; init; }
    public byte[] ColorTable { get; init; } = [];
    public int ColorTableSizeBits { get; init; }
    public byte[] LzwData { get; init; } = [];
}
