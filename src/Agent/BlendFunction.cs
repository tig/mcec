// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// The Win32 <c>BLENDFUNCTION</c> passed to <c>UpdateLayeredWindow</c> so the command overlay (#119) is
/// composited with the per-pixel alpha of its source bitmap (translucent item backgrounds, opaque text)
/// rather than a single window-wide opacity.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BlendFunction {
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}
