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
/// CR/LF/NUL delimiter must not be able to grow CmdBuilder until OutOfMemoryException (which
/// surfaces on a ThreadPool callback and kills the process). A client past the cap is hostile
/// or broken: the buffer is dropped, an error is logged, and the connection is closed.
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
    public void ParseReceivedData_DelimiterlessFlood_NeverExceedsCap_AndDropsBuilder() {
        var builder = new StringBuilder();
        var chunk = new byte[1024];
        Array.Fill(chunk, (byte)'a');

        bool overflowed = false;
        // 8 KB of delimiter-less data: parsing must refuse to buffer past the cap.
        for (int i = 0; i < 8 && !overflowed; i++) {
            overflowed = !SocketServer.ParseReceivedData(chunk, chunk.Length, builder, _ => { }, _ => { });
            Assert.True(builder.Length <= CommandAccumulator.MaxCommandLength,
                $"CmdBuilder grew to {builder.Length} chars after chunk {i + 1}");
        }

        Assert.True(overflowed, "ParseReceivedData never signaled overflow");
        Assert.Equal(0, builder.Length); // oversized partial command is dropped, not kept
    }

    [Fact]
    public void ParseReceivedData_CommandOfExactlyMaxLength_StillParses() {
        var builder = new StringBuilder();
        var commands = new List<string>();
        var data = new byte[CommandAccumulator.MaxCommandLength + 1];
        Array.Fill(data, (byte)'a');
        data[^1] = (byte)'\n';

        bool ok = SocketServer.ParseReceivedData(data, data.Length, builder, _ => { }, commands.Add);

        Assert.True(ok);
        Assert.Single(commands);
        Assert.Equal(CommandAccumulator.MaxCommandLength, commands[0].Length);
    }

    [Fact]
    public void ParseReceivedData_EscapedIacAppendSite_NeverExceedsCap() {
        // The escaped-IAC path (IAC IAC -> literal 0xFF) is an append site too and must
        // honor the same cap: fill to one char under the cap, then send IAC IAC. A
        // multi-char append there (e.g. the decimal string "255") would blow past the cap.
        var builder = new StringBuilder();
        var data = new byte[CommandAccumulator.MaxCommandLength + 1];
        Array.Fill(data, (byte)'a');
        data[^2] = 255; // IAC
        data[^1] = 255; // IAC — escaped literal 0xFF

        _ = SocketServer.ParseReceivedData(data, data.Length, builder, _ => { }, _ => { });

        Assert.True(builder.Length <= CommandAccumulator.MaxCommandLength,
            $"CmdBuilder grew to {builder.Length} chars via the escaped-IAC append site");
    }

    [Fact]
    public void ProcessReceivedData_DelimiterlessFlood_ClosesOffendingClient_AndLogsError() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        var errors = new List<string>();
        _server.Notifications += (notify, status, reply, msg) => {
            if (notify == ServiceNotification.Error) {
                errors.Add(msg);
            }
        };
        Array.Fill(context.DataBuffer, (byte)'a');

        bool closed = false;
        for (int i = 0; i < 8 && !closed; i++) {
            closed = !_server.ProcessReceivedData(context, context.DataBuffer.Length);
            Assert.True(context.CmdBuilder.Length <= CommandAccumulator.MaxCommandLength,
                $"CmdBuilder grew to {context.CmdBuilder.Length} chars after chunk {i + 1}");
        }

        Assert.True(closed, "flooding client was never closed");
        Assert.DoesNotContain(socket, _server.TrackedClients.Values);
        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.True(socket.SafeHandle.IsClosed, "offending client's socket was not closed");
        Assert.Contains(errors, msg => msg.Contains("maximum length", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessReceivedData_ValidCommand_EmitsCommand_AndKeepsClientConnected() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        var commands = new List<string>();
        _server.Notifications += (notify, status, reply, msg) => {
            if (notify == ServiceNotification.ReceivedData) {
                commands.Add(msg);
            }
        };
        byte[] data = Encoding.ASCII.GetBytes("mute\n");
        Array.Copy(data, context.DataBuffer, data.Length);

        bool keepReceiving = _server.ProcessReceivedData(context, data.Length);

        Assert.True(keepReceiving);
        Assert.Equal(new[] { "mute" }, commands);
        Assert.Contains(socket, _server.TrackedClients.Values);
    }
}
