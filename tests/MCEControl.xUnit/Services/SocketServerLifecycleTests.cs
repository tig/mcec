//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #202: SocketServer conflated Stop() with Dispose().
///   - Stop() must be resettable: a stopped server can Start() again and accept clients.
///   - Dispose() must be terminal and guarded: idempotent, and Start() after Dispose() throws.
///   - Stop() must be quiet: the pending EndAccept's ObjectDisposedException is expected
///     shutdown, not an Error notification.
///   - A per-client send to a dead peer must not throw out of Send into the calling command
///     handler; the dead client is closed and removed from tracking (complements #150).
/// Listener tests bind loopback on an ephemeral port (0) only, per the house pattern.
/// </summary>
public class SocketServerLifecycleTests : IDisposable {
    private readonly SocketServer _server = new();

    public SocketServerLifecycleTests() {
        // Reaching ServiceStatus.Connected starts ServiceBase's connected-time stopwatch, and
        // SetStatus(Stopped) then dereferences TelemetryService.Instance.TelemetryClient! —
        // null until telemetry is initialized (see AgentTestSupport).
        AgentTestSupport.EnsureTelemetry();
    }

    public void Dispose() {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Socket NewSocket() =>
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    /// <summary>Polls (like the other loopback suites) instead of sleeping a fixed time, so the
    /// test is fast when the server is fast and only slow when something is actually wrong.</summary>
    private static void WaitUntil(Func<bool> condition, string because, int timeoutMs = 10000) {
        var sw = Stopwatch.StartNew();
        while (!condition()) {
            Assert.True(sw.ElapsedMilliseconds < timeoutMs, $"Timed out waiting for {because}");
            Thread.Sleep(10);
        }
    }

    // ---- Stop() is resettable (#202) ----

    [Fact]
    public void Start_AfterStop_AcceptsNewClient() {
        _server.Start(0, "loopback");
        int firstPort = _server.ListeningPort;
        Assert.NotEqual(0, firstPort);

        using (TcpClient first = new()) {
            first.Connect(IPAddress.Loopback, firstPort);
            WaitUntil(() => _server.ConnectedClientCount == 1, "the first client to be tracked");
        }

        _server.Stop();
        Assert.Equal(ServiceStatus.Stopped, _server.CurrentStatus);
        Assert.Empty(_server.TrackedClients);
        Assert.Equal(0, _server.ConnectedClientCount);
        Assert.Equal(0, _server.ListeningPort);

        // A stopped server is NOT disposed: it must Start again and accept a client.
        _server.Start(0, "loopback");
        int secondPort = _server.ListeningPort;
        Assert.NotEqual(0, secondPort);

        using TcpClient second = new();
        second.Connect(IPAddress.Loopback, secondPort);
        WaitUntil(() => _server.ConnectedClientCount == 1, "a client to be tracked after restart");
        Assert.Equal(ServiceStatus.Connected, _server.CurrentStatus);
    }

    // ---- Stop() is quiet (#202) ----

    [Fact]
    public void Stop_WithPendingAccept_EmitsNoErrorNotification() {
        var errors = new List<string>();
        _server.ErrorOccurred += error => {
            lock (errors) {
                errors.Add(error.Message);
            }
        };

        _server.Start(0, "loopback");
        Assert.Equal(ServiceStatus.Waiting, _server.CurrentStatus);

        _server.Stop();
        Assert.Equal(ServiceStatus.Stopped, _server.CurrentStatus);

        // Closing the listener completes the pending BeginAccept on a ThreadPool thread —
        // the buggy path fired its spurious Error there, asynchronously — so give the
        // callback time to run before asserting silence.
        Thread.Sleep(500);
        lock (errors) {
            Assert.Empty(errors);
        }
    }

    // ---- Dispose() is terminal and guarded (#202) ----

    [Fact]
    public void Dispose_IsIdempotent() {
        var server = new SocketServer();
        server.Start(0, "loopback");

        server.Dispose();
        var ex = Record.Exception(server.Dispose);

        Assert.Null(ex);
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException() {
        var server = new SocketServer();
        server.Dispose();

        Assert.Throws<ObjectDisposedException>(() => server.Start(0, "loopback"));
    }

    // ---- Per-client Send is guarded (#202) ----

    [Fact]
    public void SendToClient_DeadPeer_DoesNotThrow_AndRemovesClientFromTracking() {
        // A never-connected socket makes Socket.Send throw a SocketException
        // deterministically — the same shape as a peer that vanished between the
        // broadcast loop's TryGetValue and the Send.
        using Socket dead = NewSocket();
        var context = _server.RegisterClient(dead);
        // #211: write failures surface via the typed ErrorOccurred event (the old
        // WriteFailed notification existed only to be logged).
        var writeFailures = new List<string>();
        _server.ErrorOccurred += error => {
            if (error.Message.Contains("Write failed", StringComparison.Ordinal)) {
                lock (writeFailures) {
                    writeFailures.Add(error.Message);
                }
            }
        };

        var ex = Record.Exception(() => _server.SendToClient("hello", context));

        Assert.Null(ex);
        Assert.DoesNotContain(dead, _server.TrackedClients.Values);
        Assert.Equal(0, _server.ConnectedClientCount);
        lock (writeFailures) {
            Assert.NotEmpty(writeFailures);
        }
    }

    [Fact]
    public void SendToClient_SocketClosedUnderneath_DoesNotThrow_AndRemovesClientFromTracking() {
        // The receive path (#150) can close a client's socket between the broadcast loop's
        // tracking lookup and the send — Socket.Send then throws ObjectDisposedException.
        Socket dead = NewSocket();
        var context = _server.RegisterClient(dead);
        dead.Close();

        var ex = Record.Exception(() => _server.SendToClient("hello", context));

        Assert.Null(ex);
        Assert.Empty(_server.TrackedClients);
        Assert.Equal(0, _server.ConnectedClientCount);
    }
}
