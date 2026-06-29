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
