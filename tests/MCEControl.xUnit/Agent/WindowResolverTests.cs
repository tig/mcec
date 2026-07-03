// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

public class WindowResolverTests {
    [Fact]
    public void EnumerateTopLevel_ReturnsNonNullList_AndDoesNotThrow() {
        List<WindowInfo>? windows = null;

        Exception? ex = Record.Exception(() => windows = WindowResolver.EnumerateTopLevel());

        Assert.Null(ex);
        Assert.NotNull(windows);
        // Headless CI may legitimately have zero visible, titled top-level windows.
    }

    [Fact]
    public void Resolve_BogusTitle_ReturnsNull() {
        WindowInfo? result = WindowResolver.Resolve(
            handle: 0,
            title: "no-such-window-" + Guid.NewGuid().ToString("N"),
            processName: null,
            className: null,
            foreground: false);

        Assert.Null(result);
    }

    [Fact]
    public void Matches_AppliesEachFilter_WithResolverSemantics() {
        WindowInfo info = new() {
            Handle = 0x1234,
            Title = "Untitled - Notepad",
            ClassName = "Notepad",
            ProcessName = "notepad",
        };

        // No filter matches anything.
        Assert.True(WindowResolver.Matches(info, null, null, null));
        Assert.True(WindowResolver.Matches(info, "", "", ""));

        // Title is a case-insensitive SUBSTRING; process is EXACT (case-insensitive); class is EXACT (ordinal).
        Assert.True(WindowResolver.Matches(info, "notepad", null, null));
        Assert.True(WindowResolver.Matches(info, null, "NOTEPAD", null));
        Assert.True(WindowResolver.Matches(info, null, null, "Notepad"));
        Assert.False(WindowResolver.Matches(info, null, "notepad.exe", null)); // process is without .exe
        Assert.False(WindowResolver.Matches(info, null, null, "notepad"));     // class is ordinal (case-sensitive)
        Assert.False(WindowResolver.Matches(info, "Calculator", null, null));

        // All given filters must hold (AND).
        Assert.True(WindowResolver.Matches(info, "Notepad", "notepad", "Notepad"));
        Assert.False(WindowResolver.Matches(info, "Notepad", "explorer", null));
    }

    [Fact]
    public void ListTopLevel_BogusFilter_ReturnsEmpty_NotNull() {
        List<WindowInfo> matches = WindowResolver.ListTopLevel(
            title: null, processName: null, className: "MCEC_NoSuchWindowClass_" + Guid.NewGuid().ToString("N"));

        Assert.NotNull(matches);
        Assert.Empty(matches);
    }

    [Fact]
    public void WaitForTopLevel_BogusFilter_ReturnsEmptyAfterTimeout_DoesNotThrow() {
        List<WindowInfo>? matches = null;

        Exception? ex = Record.Exception(() => matches = WindowResolver.WaitForTopLevel(
            title: null, processName: null, className: "MCEC_NoSuchWindowClass_" + Guid.NewGuid().ToString("N"),
            timeoutMs: 30));

        Assert.Null(ex);
        Assert.NotNull(matches);
        Assert.Empty(matches);
    }

    [Fact]
    public void Resolve_Foreground_DoesNotThrow_AndReturnsSaneShape() {
        // Exercises the GetForegroundWindow -> Describe path. Headless: the result may be null;
        // when a foreground window exists it must come back as a WindowInfo with a real handle.
        WindowInfo? info = null;

        Exception? ex = Record.Exception(() =>
            info = WindowResolver.Resolve(null, null, null, null, foreground: true));

        Assert.Null(ex);
        if (info is not null) {
            Assert.NotEqual(0, info.Handle);
            Assert.NotNull(info.Title);
            Assert.NotNull(info.ClassName);
            Assert.NotNull(info.ProcessName);
        }
    }
}
