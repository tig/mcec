//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #148 (SocketServer side): the per-command receive buffer must be
/// bounded. The server binds 0.0.0.0 unauthenticated, so an attacker streaming bytes with no
/// CR/LF/NUL delimiter must not be able to grow the accumulator until OutOfMemoryException
/// (which surfaces on a ThreadPool callback and kills the process). Since #212 the server
/// delegates delimiter/cap handling to <see cref="CommandAccumulator"/>, so overflow follows
/// its single policy: the oversized partial command is dropped, an error is notified once, and
/// input is discarded until the next delimiter — the connection stays open and memory stays
/// bounded. (Pre-#212 the server had a divergent second copy of the policy that closed the
/// connection instead.)
/// No live listener is used — tests drive the internal receive seams directly.
/// </summary>
public class SocketServerReceiveCapTests : IDisposable {
    private readonly SocketServer _server = new();

    public void Dispose() {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Socket NewSocket() =>
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [Fact]
    public void ParseReceivedData_DelimiterlessFlood_NeverExceedsCap_AndDropsBuffer() {
        var accumulator = new CommandAccumulator();
        var chunk = new byte[1024];
        Array.Fill(chunk, (byte)'a');

        int overflows = 0;
        // 8 KB of delimiter-less data: parsing must refuse to buffer past the cap.
        for (int i = 0; i < 8; i++) {
            SocketServer.ParseReceivedData(chunk, chunk.Length, accumulator, _ => { }, _ => { }, () => overflows++);
            Assert.True(accumulator.Length <= CommandAccumulator.MaxCommandLength,
                $"accumulator grew to {accumulator.Length} chars after chunk {i + 1}");
        }

        Assert.Equal(1, overflows); // notified exactly once per oversized run (no log flooding)
        Assert.Equal(0, accumulator.Length); // oversized partial command is dropped, not kept
    }

    [Fact]
    public void ParseReceivedData_CommandOfExactlyMaxLength_StillParses() {
        var accumulator = new CommandAccumulator();
        var commands = new List<string>();
        var data = new byte[CommandAccumulator.MaxCommandLength + 1];
        Array.Fill(data, (byte)'a');
        data[^1] = (byte)'\n';

        SocketServer.ParseReceivedData(data, data.Length, accumulator, _ => { }, commands.Add);

        Assert.Single(commands);
        Assert.Equal(CommandAccumulator.MaxCommandLength, commands[0].Length);
    }

    [Fact]
    public void ParseReceivedData_EscapedIacAppendSite_NeverExceedsCap() {
        // The escaped-IAC path (IAC IAC -> literal 0xFF) is an append site too and must
        // honor the same cap: fill to one char under the cap, then send IAC IAC. A
        // multi-char append there (e.g. the decimal string "255") would blow past the cap.
        var accumulator = new CommandAccumulator();
        var data = new byte[CommandAccumulator.MaxCommandLength + 1];
        Array.Fill(data, (byte)'a');
        data[^2] = 255; // IAC
        data[^1] = 255; // IAC — escaped literal 0xFF

        SocketServer.ParseReceivedData(data, data.Length, accumulator, _ => { }, _ => { });

        Assert.True(accumulator.Length <= CommandAccumulator.MaxCommandLength,
            $"accumulator grew to {accumulator.Length} chars via the escaped-IAC append site");
    }

    [Fact]
    public void ProcessReceivedData_DelimiterlessFlood_DiscardsUntilDelimiter_AndKeepsClientConnected() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        var errors = new List<string>();
        var commands = new List<string>();
        _server.ErrorOccurred += error => errors.Add(error.Message);
        _server.CommandReceived += (reply, command) => commands.Add(command);
        Array.Fill(context.DataBuffer, (byte)'a');

        // 8 KB of delimiter-less data. #212: ONE overflow policy — the accumulator's
        // discard-until-delimiter recovery. The connection is NOT closed (pre-#212 the
        // server had a divergent copy of the cap logic that closed it).
        for (int i = 0; i < 8; i++) {
            Assert.True(_server.ProcessReceivedData(context, context.DataBuffer.Length),
                $"receive was stopped after chunk {i + 1}");
            Assert.True(context.Accumulator.Length <= CommandAccumulator.MaxCommandLength,
                $"accumulator grew to {context.Accumulator.Length} chars after chunk {i + 1}");
        }

        Assert.Contains(socket, _server.TrackedClients.Values);
        Assert.False(socket.SafeHandle.IsClosed, "client's socket must stay open under the #212 policy");
        Assert.Single(errors, msg => msg.Contains("maximum length", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(commands); // the oversized run (and its tail) never surfaces as a command

        // Recovery: a delimiter ends the discard and the next command parses normally.
        byte[] recovery = Encoding.ASCII.GetBytes("\nmute\n");
        Array.Copy(recovery, context.DataBuffer, recovery.Length);
        Assert.True(_server.ProcessReceivedData(context, recovery.Length));
        Assert.Equal(new[] { "mute" }, commands);
    }

    [Fact]
    public void ProcessReceivedData_ValidCommand_EmitsCommand_AndKeepsClientConnected() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        var commands = new List<string>();
        _server.CommandReceived += (reply, command) => commands.Add(command);
        byte[] data = Encoding.ASCII.GetBytes("mute\n");
        Array.Copy(data, context.DataBuffer, data.Length);

        bool keepReceiving = _server.ProcessReceivedData(context, data.Length);

        Assert.True(keepReceiving);
        Assert.Equal(new[] { "mute" }, commands);
        Assert.Contains(socket, _server.TrackedClients.Values);
    }
}
