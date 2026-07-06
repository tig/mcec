// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using MCEControl.Dialogs;
using Xunit;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Tests the copyable crash-report text (#295). The dialog itself is WinForms UI, but the detail it puts
/// on the clipboard is a pure string, so that is what we pin: it must carry enough to file a good bug
/// report (type, message, stack trace, and the full inner-exception chain), not just the top message.
/// </summary>
public class ExceptionDialogTests {
    private static Exception Thrown(Exception toThrow) {
        try {
            throw toThrow;
        }
        catch (Exception e) {
            return e; // now carries a real stack trace
        }
    }

    [Fact]
    public void BuildDetails_IncludesType_Message_AndStackTrace() {
        Exception ex = Thrown(new InvalidOperationException("the widget exploded"));

        string details = ExceptionDialog.BuildDetails(ex);

        Assert.Contains("InvalidOperationException", details);
        Assert.Contains("the widget exploded", details);
        // A real stack trace is present: the frame where it was thrown (ExceptionDialogTests.Thrown).
        Assert.Contains("ExceptionDialogTests.Thrown", details);
    }

    [Fact]
    public void BuildDetails_IncludesTheInnerExceptionChain() {
        Exception ex = Thrown(new Exception("outer", new InvalidOperationException("the real cause")));

        string details = ExceptionDialog.BuildDetails(ex);

        Assert.Contains("outer", details);
        Assert.Contains("the real cause", details);
    }

    [Fact]
    public void BuildDetails_HasNoLiteralBackslashN() {
        // The bug that started #295: the old MessageBox used a verbatim interpolated string, so "\n"
        // showed up as text. The report must use real newlines, never the two-character sequence.
        Exception ex = Thrown(new Exception("boom"));

        string details = ExceptionDialog.BuildDetails(ex);

        Assert.DoesNotContain(@"\n", details);
        Assert.Contains("\n", details); // real line breaks (version/OS header)
    }

    [Fact]
    public void BuildDetails_NullException_Throws() {
        Assert.Throws<ArgumentNullException>(() => ExceptionDialog.BuildDetails(null!));
    }
}
