// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Tests the #119 observation-exclusion registry: a window MCEC owns (the command overlay) registers its
/// handle so an agent can never resolve/target it by handle, foreground, or enumeration.
/// </summary>
public class WindowResolverIgnoreTests {
    [Fact]
    public void Register_MakesHandleIgnored_AndUnregisterClearsIt() {
        const long h = 0x4242;
        Assert.False(WindowResolver.IsIgnoredWindow(h));

        WindowResolver.RegisterIgnoredWindow(h);
        try {
            Assert.True(WindowResolver.IsIgnoredWindow(h));
        }
        finally {
            WindowResolver.UnregisterIgnoredWindow(h);
        }

        Assert.False(WindowResolver.IsIgnoredWindow(h));
    }

    [Fact]
    public void ResolveByHandle_ReturnsNull_ForAnIgnoredWindow() {
        const long h = 0x4243;
        WindowResolver.RegisterIgnoredWindow(h);
        try {
            // Even though a handle was given, an ignored window must never resolve as a target.
            Assert.Null(WindowResolver.Resolve(h, null, null, null, foreground: false));
        }
        finally {
            WindowResolver.UnregisterIgnoredWindow(h);
        }
    }
}
