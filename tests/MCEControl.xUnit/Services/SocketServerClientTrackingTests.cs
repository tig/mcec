using System;
using System.Net.Sockets;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #147: SocketServer must not reuse the connected-client
/// tally as the client-list key. Keys must be unique for the lifetime of the server,
/// otherwise under connect/disconnect churn a new client's socket is never tracked
/// (handle leak) and closing one client closes another, still-active client.
/// </summary>
public class SocketServerClientTrackingTests : IDisposable
{
    private readonly SocketServer _server = new();

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Socket NewSocket() =>
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [Fact]
    public void RegisterClient_AssignsUniqueClientNumbers_AcrossChurn()
    {
        // A connects, B connects, A disconnects, C connects.
        var contextA = _server.RegisterClient(NewSocket());
        var contextB = _server.RegisterClient(NewSocket());
        _server.CloseSocket(contextA);
        var contextC = _server.RegisterClient(NewSocket());

        // With the buggy _clientCount-as-key scheme, C gets B's client number.
        Assert.NotEqual(contextB.ClientNumber, contextC.ClientNumber);
    }

    [Fact]
    public void RegisterClient_AfterChurn_TracksNewClientSocket()
    {
        var contextA = _server.RegisterClient(NewSocket());
        _ = _server.RegisterClient(NewSocket());
        _server.CloseSocket(contextA);

        using var socketC = NewSocket();
        _ = _server.RegisterClient(socketC);

        // With the bug, GetOrAdd hits B's existing key and silently drops
        // socketC; it is never tracked and thus never closed (handle leak).
        Assert.Contains(socketC, _server.TrackedClients.Values);
    }

    [Fact]
    public void CloseSocket_AfterChurn_DoesNotCloseOtherActiveClient()
    {
        var contextA = _server.RegisterClient(NewSocket());
        using var socketB = NewSocket();
        _ = _server.RegisterClient(socketB);
        _server.CloseSocket(contextA);
        var contextC = _server.RegisterClient(NewSocket());

        // Disconnect C. With the bug, C shares B's key, so this removes
        // and forcibly closes B's still-active socket instead.
        _server.CloseSocket(contextC);

        Assert.Contains(socketB, _server.TrackedClients.Values);
        Assert.False(socketB.SafeHandle.IsClosed);
    }

    [Fact]
    public void Dispose_WithConnectedClients_ResetsConnectedClientCount()
    {
        _ = _server.RegisterClient(NewSocket());
        _ = _server.RegisterClient(NewSocket());
        Assert.Equal(2, _server.ConnectedClientCount);

        _server.Stop();

        // Stop force-closes every tracked socket; the connected tally must
        // drain with them or a later Start reports a stale count.
        Assert.Empty(_server.TrackedClients);
        Assert.Equal(0, _server.ConnectedClientCount);
    }

    [Fact]
    public void ConnectedClientCount_TracksOnlyCurrentlyConnectedClients()
    {
        var contextA = _server.RegisterClient(NewSocket());
        var contextB = _server.RegisterClient(NewSocket());
        _server.CloseSocket(contextA);
        var contextC = _server.RegisterClient(NewSocket());

        Assert.Equal(2, _server.ConnectedClientCount);
        Assert.Equal(2, _server.TrackedClients.Count);

        _server.CloseSocket(contextB);
        _server.CloseSocket(contextC);

        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.Empty(_server.TrackedClients);
    }
}
