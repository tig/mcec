// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Commands;

public class WindowCommandTests {
    [Fact]
    public void ValidateArguments_RequiresCoordinatesForMove() {
        Assert.Null(WindowCommand.ValidateArguments("move", 10, 20, null, null));
        Assert.Equal("move requires x and y.", WindowCommand.ValidateArguments("move", null, null, null, null));
        Assert.Equal("move requires x and y.", WindowCommand.ValidateArguments("move", 10, null, null, null));
    }

    [Fact]
    public void ValidateArguments_RequiresSizeForResize() {
        Assert.Null(WindowCommand.ValidateArguments("resize", null, null, 640, 480));
        Assert.Equal("resize requires width and height.", WindowCommand.ValidateArguments("resize", null, null, null, null));
        Assert.Equal("resize requires width and height.", WindowCommand.ValidateArguments("resize", null, null, 640, null));
    }

    [Fact]
    public void ValidateArguments_AllowsOtherActionsWithoutExtraArgs() {
        Assert.Null(WindowCommand.ValidateArguments("minimize", null, null, null, null));
        Assert.Null(WindowCommand.ValidateArguments("maximize", null, null, null, null));
        Assert.Null(WindowCommand.ValidateArguments("restore", null, null, null, null));
        Assert.Null(WindowCommand.ValidateArguments("foreground", null, null, null, null));
    }
}
