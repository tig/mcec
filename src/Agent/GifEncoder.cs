// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MCEControl;

/// <summary>
/// Assembles a sequence of frames into a single animated GIF89a for the MCEC 3.0 agent "record"
/// feature, with NO extra dependency. .NET's GDI+ GIF codec can quantize + LZW-compress one frame
/// (<see cref="Image.Save(Stream, ImageFormat)"/>) but cannot author an animation (frame delays /
/// looping). So we let GDI+ encode each frame to a standalone GIF, then stitch those together:
/// a GIF89a header, a Netscape looping extension, and; per frame; a Graphic Control Extension
/// carrying the inter-frame delay plus the frame's image data, with each frame's color table written
/// as a Local Color Table so per-frame palettes survive.
/// </summary>
public static class GifEncoder {
    /// <summary>
    /// Encodes a single bitmap to standalone GIF bytes via GDI+ (quantizes to ≤256 colors and
    /// LZW-compresses). This is the per-frame input to <see cref="Assemble"/>.
    /// </summary>
    public static byte[] EncodeFrame(Bitmap frame) {
        ArgumentNullException.ThrowIfNull(frame);
        using MemoryStream ms = new();
        frame.Save(ms, ImageFormat.Gif);
        return ms.ToArray();
    }

    /// <summary>
    /// Stitches the given standalone GIF frames (each produced by <see cref="EncodeFrame"/>) into one
    /// animated GIF89a.
    /// </summary>
    /// <param name="gifFrames">Per-frame standalone GIF byte buffers, in order.</param>
    /// <param name="delayMs">Delay between frames, in milliseconds (rounded to GIF centiseconds).</param>
    /// <param name="loop">When true, the animation loops forever; otherwise it plays once.</param>
    /// <returns>The animated GIF89a as a byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when there are no frames or a frame cannot be parsed.</exception>
    public static byte[] Assemble(IReadOnlyList<byte[]> gifFrames, int delayMs, bool loop) {
        ArgumentNullException.ThrowIfNull(gifFrames);
        if (gifFrames.Count == 0) {
            throw new ArgumentException("At least one frame is required.", nameof(gifFrames));
        }

        // GIF frame delay is expressed in centiseconds. Clamp to a minimum of 2 (20 ms): a 0 delay is
        // interpreted by many viewers as "as fast as possible", which is not what an fps implies.
        int delayCs = Math.Max(2, (int)Math.Round(delayMs / 10.0));

        List<GifFrame> frames = [];
        int width = 0;
        int height = 0;
        foreach (byte[] raw in gifFrames) {
            GifFrame f = ParseFrame(raw);
            frames.Add(f);
            width = Math.Max(width, f.Width);
            height = Math.Max(height, f.Height);
        }

        using MemoryStream output = new();

        // --- Header + Logical Screen Descriptor (no Global Color Table; every frame carries its own
        //     Local Color Table). ---
        output.Write("GIF89a"u8);
        WriteUInt16(output, (ushort)width);
        WriteUInt16(output, (ushort)height);
        output.WriteByte(0x00); // packed: no GCT
        output.WriteByte(0x00); // background color index (unused without a GCT)
        output.WriteByte(0x00); // pixel aspect ratio

        // --- Netscape Application Extension: loop count (0 = forever). Omit it for play-once. ---
        if (loop) {
            output.WriteByte(0x21); // extension introducer
            output.WriteByte(0xFF); // application extension label
            output.WriteByte(0x0B); // block size (11)
            output.Write("NETSCAPE2.0"u8);
            output.WriteByte(0x03); // sub-block size
            output.WriteByte(0x01); // sub-block id
            WriteUInt16(output, 0); // loop count: 0 = infinite
            output.WriteByte(0x00); // block terminator
        }

        // --- Per-frame: Graphic Control Extension (delay) + Image Descriptor + Local Color Table +
        //     LZW image data. ---
        foreach (GifFrame f in frames) {
            output.WriteByte(0x21); // extension introducer
            output.WriteByte(0xF9); // graphic control label
            output.WriteByte(0x04); // block size (4)
            output.WriteByte(0x04); // packed: disposal method 1 (do not dispose), no transparency
            WriteUInt16(output, (ushort)delayCs);
            output.WriteByte(0x00); // transparent color index (unused)
            output.WriteByte(0x00); // block terminator

            output.WriteByte(0x2C); // image separator
            WriteUInt16(output, 0); // image left
            WriteUInt16(output, 0); // image top
            WriteUInt16(output, (ushort)f.Width);
            WriteUInt16(output, (ushort)f.Height);
            // packed: Local Color Table flag (0x80) | LCT size bits (N where table = 2^(N+1)).
            output.WriteByte((byte)(0x80 | (f.ColorTableSizeBits & 0x07)));
            output.Write(f.ColorTable, 0, f.ColorTable.Length);
            output.Write(f.LzwData, 0, f.LzwData.Length); // min-code-size + sub-blocks + terminator
        }

        output.WriteByte(0x3B); // trailer
        return output.ToArray();
    }

    /// <summary>
    /// Parses one standalone GIF (as emitted by GDI+) far enough to extract the first image's
    /// dimensions, color table, and LZW data block (min-code-size byte + data sub-blocks + the 0x00
    /// terminator), so it can be re-emitted as one frame of an animation.
    /// </summary>
    private static GifFrame ParseFrame(byte[] b) {
        if (b.Length < 13 || b[0] != (byte)'G' || b[1] != (byte)'I' || b[2] != (byte)'F') {
            throw new ArgumentException("Not a GIF stream.");
        }

        // Logical Screen Descriptor packed byte at offset 10.
        byte lsdPacked = b[10];
        bool hasGct = (lsdPacked & 0x80) != 0;
        int gctSizeBits = lsdPacked & 0x07;
        int gctEntries = 1 << (gctSizeBits + 1);

        int pos = 13;
        byte[] globalTable = [];
        if (hasGct) {
            int len = 3 * gctEntries;
            globalTable = Slice(b, pos, len);
            pos += len;
        }

        // Walk blocks until the first Image Descriptor (skip any extensions GDI+ might emit).
        while (pos < b.Length) {
            byte sep = b[pos];
            if (sep == 0x21) { // extension: introducer + label + sub-blocks
                pos += 2;
                pos = SkipSubBlocks(b, pos);
            }
            else if (sep == 0x2C) { // image descriptor (10 bytes incl. separator)
                int w = b[pos + 5] | (b[pos + 6] << 8);
                int h = b[pos + 7] | (b[pos + 8] << 8);
                byte imgPacked = b[pos + 9];
                pos += 10;

                bool hasLct = (imgPacked & 0x80) != 0;
                int lctSizeBits = imgPacked & 0x07;
                byte[] colorTable;
                int colorTableSizeBits;
                if (hasLct) {
                    int len = 3 * (1 << (lctSizeBits + 1));
                    colorTable = Slice(b, pos, len);
                    colorTableSizeBits = lctSizeBits;
                    pos += len;
                }
                else {
                    // GDI+ normally writes a Global Color Table and no LCT; reuse the GCT as this
                    // frame's local table in the animation.
                    colorTable = globalTable;
                    colorTableSizeBits = gctSizeBits;
                }

                int lzwStart = pos;
                pos++; // LZW minimum code size byte
                pos = SkipSubBlocks(b, pos); // advances past data sub-blocks and the 0x00 terminator
                byte[] lzw = Slice(b, lzwStart, pos - lzwStart);

                return new GifFrame {
                    Width = w,
                    Height = h,
                    ColorTable = colorTable,
                    ColorTableSizeBits = colorTableSizeBits,
                    LzwData = lzw,
                };
            }
            else {
                // Trailer (0x3B) or anything unexpected before an image: no image found.
                break;
            }
        }

        throw new ArgumentException("GIF frame has no image data.");
    }

    /// <summary>
    /// Advances past a chain of GIF data sub-blocks starting at <paramref name="pos"/>, returning the
    /// index just after the 0x00 block terminator.
    /// </summary>
    private static int SkipSubBlocks(byte[] b, int pos) {
        while (pos < b.Length) {
            int size = b[pos];
            pos++;
            if (size == 0) {
                return pos; // terminator consumed
            }
            pos += size;
        }
        return pos;
    }

    private static byte[] Slice(byte[] src, int start, int length) {
        if (start < 0 || length < 0 || start + length > src.Length) {
            throw new ArgumentException("Malformed GIF: block extends past end of stream.");
        }
        byte[] dst = new byte[length];
        Array.Copy(src, start, dst, 0, length);
        return dst;
    }

    private static void WriteUInt16(Stream s, ushort value) {
        s.WriteByte((byte)(value & 0xFF));
        s.WriteByte((byte)((value >> 8) & 0xFF));
    }
}
