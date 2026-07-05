// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Guard-path tests for <see cref="FocusService"/> (#91/#270). The live foreground/focus behavior needs
/// real windows (like <see cref="UiaService"/>'s live paths, it is not unit-tested); these pin the
/// zero-handle guards so a bad handle can never sail into a P/Invoke and returns the honest negative that
/// makes a command emit the <c>foreground</c>/<c>focus</c> category instead of a false success.
/// </summary>
public class FocusServiceTests {
    [Fact]
    public void BringToForeground_ZeroHandle_ReturnsFalse() {
        Assert.False(FocusService.BringToForeground(IntPtr.Zero));
    }

    [Fact]
    public void IsFocusInWindow_ZeroHandle_ReturnsFalse() {
        Assert.False(FocusService.IsFocusInWindow(IntPtr.Zero));
    }

    [Fact]
    public void FocusedWindow_ZeroHandle_ReturnsZero() {
        Assert.Equal(IntPtr.Zero, FocusService.FocusedWindow(IntPtr.Zero));
    }

    [Fact]
    public void RootOf_ZeroHandle_ReturnsZero() {
        Assert.Equal(IntPtr.Zero, FocusService.RootOf(IntPtr.Zero));
    }

    [Fact]
    public void ConfirmFocusInWindow_ZeroHandle_ReturnsFalse() {
        // A zero handle can never hold focus; the bounded confirm poll must give up and report false.
        Assert.False(FocusService.ConfirmFocusInWindow(IntPtr.Zero));
    }
}
