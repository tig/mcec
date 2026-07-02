//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Net.Sockets;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #150: SocketServer must not leak a dead connection when the
/// receive path hits a terminal error. The command port binds unauthenticated, so an error
/// that leaves a client dead-but-tracked (holding a socket handle, a _clientList entry, and a
/// connected-count slot) is a resource-exhaustion vector under repeated occurrences.
///
/// Two paths were leaking:
///   1. OnDataReceived's SocketException catch closed only on ConnectionReset; every other
///      error code (including the ConnectionReset-adjacent errors the telnet-reply Socket.Send
///      can throw mid-parse) merely logged.
///   2. The receive-callback precondition returned when the socket was not Connected, without
///      closing.
/// Both must CloseSocket exactly once; removed from TrackedClients, ConnectedClientCount
/// decremented once, socket handle closed; and must preserve #147's idempotent close (no
/// double-decrement when close runs twice).
///
/// No live listener is used; tests drive the internal receive seams directly.
/// </summary>
public class SocketServerReceiveErrorTests : IDisposable {
    private readonly SocketServer _server = new();

    public void Dispose() {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Socket NewSocket() =>
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [Fact]
    public void HandleReceiveError_NonConnectionReset_ClosesClient_NoLeak() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        Assert.Equal(1, _server.ConnectedClientCount);

        // A non-reset error (e.g. the telnet-reply Send throwing NetworkDown mid-parse) is
        // still terminal: the connection can never be received from again, so it must close.
        _server.HandleReceiveError(context, new SocketException((int)SocketError.NetworkDown));

        Assert.DoesNotContain(socket, _server.TrackedClients.Values);
        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.True(socket.SafeHandle.IsClosed, "dead client's socket handle was not closed");
    }

    [Fact]
    public void HandleReceiveError_ConnectionReset_ClosesClient_Unchanged() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);

        _server.HandleReceiveError(context, new SocketException((int)SocketError.ConnectionReset));

        Assert.DoesNotContain(socket, _server.TrackedClients.Values);
        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.True(socket.SafeHandle.IsClosed);
    }

    [Fact]
    public void HandleReceiveError_EmitsErrorOccurred_WithTypedSocketError() {
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        ServiceError? received = null;
        _server.ErrorOccurred += error => received = error;

        _server.HandleReceiveError(context, new SocketException((int)SocketError.NetworkDown));

        Assert.NotNull(received);
        // #211: the error carries the TYPED SocketError (the old stringly notification
        // flattened it into the message text).
        Assert.Equal(SocketError.NetworkDown, received!.SocketError);
        Assert.StartsWith("OnDataReceived:", received.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleReceiveError_RunTwice_DoesNotDoubleDecrement() {
        using Socket socketA = NewSocket();
        using Socket socketB = NewSocket();
        var contextA = _server.RegisterClient(socketA);
        _ = _server.RegisterClient(socketB);
        Assert.Equal(2, _server.ConnectedClientCount);

        // Same terminal error can be observed twice (e.g. EndReceive then a stale callback);
        // the guarded decrement (#147) must not drop the count below the real client tally.
        _server.HandleReceiveError(contextA, new SocketException((int)SocketError.NetworkDown));
        _server.HandleReceiveError(contextA, new SocketException((int)SocketError.NetworkDown));

        Assert.Equal(1, _server.ConnectedClientCount);
        Assert.Contains(socketB, _server.TrackedClients.Values);
    }

    [Fact]
    public void EnsureClientConnectedOrClose_NotConnected_ClosesClient_NoLeak() {
        // A freshly created, never-connected socket reports Connected == false, standing in for
        // a client whose connection silently dropped between callbacks.
        using Socket socket = NewSocket();
        var context = _server.RegisterClient(socket);
        Assert.False(socket.Connected);
        Assert.Equal(1, _server.ConnectedClientCount);

        bool proceed = _server.EnsureClientConnectedOrClose(context);

        Assert.False(proceed);
        Assert.DoesNotContain(socket, _server.TrackedClients.Values);
        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.True(socket.SafeHandle.IsClosed, "dead-but-tracked client was not closed");
    }

    [Fact]
    public void EnsureClientConnectedOrClose_RunTwice_DoesNotDoubleDecrement() {
        using Socket socketA = NewSocket();
        using Socket socketB = NewSocket();
        var contextA = _server.RegisterClient(socketA);
        _ = _server.RegisterClient(socketB);

        _ = _server.EnsureClientConnectedOrClose(contextA);
        _ = _server.EnsureClientConnectedOrClose(contextA);

        Assert.Equal(1, _server.ConnectedClientCount);
        Assert.Contains(socketB, _server.TrackedClients.Values);
    }
}
