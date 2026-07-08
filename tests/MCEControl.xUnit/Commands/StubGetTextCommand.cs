// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>Test double for <see cref="GetTextCommand"/> that stubs window capture and OCR.</summary>
internal sealed class StubGetTextCommand : GetTextCommand {
    public Bitmap? StubWindowBitmap { get; init; }
    public RegionOcrResult? StubOcrResult { get; init; }
    public bool OcrCalled { get; private set; }

    protected override WindowInfo? TryResolveWindow() =>
        new() { Handle = 0x123, Title = "Stub", ProcessName = "stub" };

    protected override Bitmap CaptureWindowBitmap(WindowInfo win, out bool usedFallback) {
        usedFallback = false;
        return (Bitmap)StubWindowBitmap!.Clone();
    }

    protected override RegionOcrResult Recognize(Bitmap bitmap) {
        OcrCalled = true;
        return StubOcrResult!;
    }
}