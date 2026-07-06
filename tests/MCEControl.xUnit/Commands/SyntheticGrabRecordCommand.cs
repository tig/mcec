// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Text.Json.Nodes;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// A <see cref="RecordCommand"/> whose grabber returns small in-memory bitmaps instead of capturing
/// the desktop, so start/oneshot paths can be exercised in a normal (headless-safe) test run.
/// </summary>
public class SyntheticGrabRecordCommand : RecordCommand {
    protected override Func<Bitmap> BuildGrabber(out JsonNode? target, out string? errorMessage) {
        target = new JsonObject { ["type"] = "synthetic" };
        errorMessage = null;
        return () => new Bitmap(8, 8);
    }
}
