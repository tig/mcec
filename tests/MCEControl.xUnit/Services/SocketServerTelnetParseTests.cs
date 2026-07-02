//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for the telnet negotiation parser (issue #144): a crafted TCP packet
/// must never cause an out-of-bounds read / unhandled exception that crashes the process.
/// </summary>
public class SocketServerTelnetParseTests {
    private const byte IAC = 255;
    private const byte WILL = 251;
    private const byte WONT = 252;
    private const byte DO = 253;
    private const byte DONT = 254;
    private const byte SGA = 3; // TelnetOptions.SGA

    private static List<byte[]> Parse(byte[] buffer, int count, out List<string> commands) {
        var replies = new List<byte[]>();
        var cmds = new List<string>();
        SocketServer.ParseReceivedData(
            buffer,
            count,
            new StringBuilder(),
            reply => replies.Add(reply),
            cmd => cmds.Add(cmd));
        commands = cmds;
        return replies;
    }

    [Fact]
    public void FullBuffer_EndingInIacDo_DoesNotThrow() {
        // The #144 exploit: a full 1024-byte buffer whose last two bytes are IAC DO.
        // The option byte would be read at index 1024 -> IndexOutOfRangeException.
        var buffer = new byte[1024];
        buffer[1022] = IAC;
        buffer[1023] = DO;

        var ex = Record.Exception(() => Parse(buffer, 1024, out _));

        Assert.Null(ex);
    }

    [Fact]
    public void TruncatedIacVerbOption_ProducesNoReply_AndDoesNotThrow() {
        // IAC DO with no option byte present (verb ends the chunk): must be ignored, no reply,
        // and must not read past the received count.
        var buffer = new byte[8];
        buffer[0] = IAC;
        buffer[1] = DO; // no option byte follows within count

        var replies = Parse(buffer, 2, out _);

        Assert.Empty(replies);
    }

    [Fact]
    public void TrailingLoneIac_DoesNotThrow_AndProducesNoReply() {
        // A chunk ending in a bare IAC (no verb byte follows): must be ignored, no reply, no throw.
        var buffer = new byte[] { (byte)'x', IAC };

        var replies = Parse(buffer, buffer.Length, out _);

        Assert.Empty(replies);
    }

    [Fact]
    public void LiteralIacEscape_DoesNotThrow_AndProducesNoReply() {
        // IAC IAC is the escape for a literal 0xFF; it is appended to the command buffer, not
        // treated as negotiation, so it must emit no telnet reply and must not throw.
        var buffer = new byte[] { IAC, IAC };

        var replies = Parse(buffer, buffer.Length, out _);

        Assert.Empty(replies);
    }

    [Fact]
    public void CompleteDoSga_RepliesWill() {
        // IAC DO SGA -> server replies IAC WILL SGA.
        var buffer = new byte[] { IAC, DO, SGA };

        var replies = Parse(buffer, 3, out _);

        Assert.Equal(3, replies.Count);
        Assert.Equal(IAC, replies[0][0]);
        Assert.Equal(WILL, replies[1][0]);
        Assert.Equal(SGA, replies[2][0]);
    }

    [Fact]
    public void CompleteDoNonSga_RepliesWont() {
        // IAC DO <non-SGA option> -> server replies IAC WONT option.
        const byte someOption = 24; // TERMINAL-TYPE
        var buffer = new byte[] { IAC, DO, someOption };

        var replies = Parse(buffer, 3, out _);

        Assert.Equal(3, replies.Count);
        Assert.Equal(IAC, replies[0][0]);
        Assert.Equal(WONT, replies[1][0]);
        Assert.Equal(someOption, replies[2][0]);
    }

    [Fact]
    public void LiteralIacEscape_AppendsSingleChar255ToCommand() {
        // IAC IAC is the telnet escape for a literal 0xFF data byte: exactly one (char)255
        // must land in the accumulated command — not the decimal string "255"
        // (StringBuilder.Append(byte) formats the number; #148 review follow-up).
        var buffer = new byte[] { (byte)'a', IAC, IAC, (byte)'b', (byte)'\n' };

        Parse(buffer, buffer.Length, out var commands);

        Assert.Single(commands);
        Assert.Equal("aÿb", commands[0]);
    }

    [Fact]
    public void PlainText_TerminatedByNewline_YieldsOneCommand() {
        var buffer = Encoding.ASCII.GetBytes("hello\r");

        Parse(buffer, buffer.Length, out var commands);

        Assert.Single(commands);
        Assert.Equal("hello", commands[0]);
    }

    [Fact]
    public void DontAndWont_AreHandledSymmetrically() {
        // IAC DONT SGA -> reply IAC DO SGA ; IAC WONT <opt> -> reply IAC DONT <opt>
        var buffer = new byte[] { IAC, DONT, SGA, IAC, WONT, 24 };

        var replies = Parse(buffer, buffer.Length, out _);

        Assert.Equal(6, replies.Count);
        Assert.Equal(IAC, replies[0][0]);
        Assert.Equal(DO, replies[1][0]);
        Assert.Equal(SGA, replies[2][0]);
        Assert.Equal(IAC, replies[3][0]);
        Assert.Equal(DONT, replies[4][0]);
        Assert.Equal((byte)24, replies[5][0]);
    }
}
