using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

public class TelnetProtocolTests
{
    // NOTE: ÿ is used instead of \xFF throughout — C#'s \x escape greedily
    // consumes up to four hex digits, so "\xFFb" would be the single char U+0FFB.

    [Fact]
    public void EscapeIac_DoublesIacCharacter()
    {
        Assert.Equal("ÿÿ", TelnetProtocol.EscapeIac("ÿ"));
    }

    [Fact]
    public void EscapeIac_DoublesEveryIacCharacter()
    {
        Assert.Equal("aÿÿbÿÿc", TelnetProtocol.EscapeIac("aÿbÿc"));
    }

    [Fact]
    public void EscapeIac_LeavesNormalTextUnchanged()
    {
        Assert.Equal("hello world\r\n", TelnetProtocol.EscapeIac("hello world\r\n"));
    }

    [Fact]
    public void EscapeIac_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", TelnetProtocol.EscapeIac(""));
    }

    [Fact]
    public void EscapeIac_DoesNotMatchTheOldBuggyLiteral()
    {
        // The pre-#203 code replaced the four-char string NUL+'x'+'F'+'F' — make sure
        // that sequence is now left alone and only the real IAC char is escaped.
        Assert.Equal("\0xFF", TelnetProtocol.EscapeIac("\0xFF"));
    }

    [Fact]
    public void EscapeIac_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TelnetProtocol.EscapeIac(null!));
    }
}
