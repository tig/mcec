//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the typed service events (#211): the events that replaced the 4-arg stringly
/// NotificationCallback are now observable without MainWindow. Verifies SocketServer raises
/// StatusChanged with the right lifecycle sequence/detail and CommandReceived with the right
/// Reply context and command payload. Listener tests bind loopback on an ephemeral port (0)
/// only, per the house pattern.
/// </summary>
public class SocketServerTypedEventsTests : IDisposable {
    private readonly SocketServer _server = new();

    public SocketServerTypedEventsTests() {
        // Reaching ServiceStatus.Connected starts ServiceBase's connected-time stopwatch, and
        // SetStatus(Stopped) then dereferences TelemetryService.Instance.TelemetryClient!;
        // null until telemetry is initialized (see AgentTestSupport).
        AgentTestSupport.EnsureTelemetry();
    }

    public void Dispose() {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void WaitUntil(Func<bool> condition, string because, int timeoutMs = 10000) {
        var sw = Stopwatch.StartNew();
        while (!condition()) {
            Assert.True(sw.ElapsedMilliseconds < timeoutMs, $"Timed out waiting for {because}");
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void StatusChanged_LifecycleSequence_CarriesStatusAndDetail() {
        var changes = new List<(ServiceStatus Status, string Detail)>();
        _server.StatusChanged += (status, detail) => {
            lock (changes) {
                changes.Add((status, detail));
            }
        };

        _server.Start(0, "loopback");
        lock (changes) {
            // Started (with the bind endpoint as detail) then Waiting.
            ServiceStatus[] expected = [ServiceStatus.Started, ServiceStatus.Waiting];
            ServiceStatus[] actual = [.. changes.Select(c => c.Status)];
            Assert.Equal(expected, actual);
            Assert.StartsWith("127.0.0.1:", changes[0].Detail, StringComparison.Ordinal);
        }

        using (TcpClient client = new()) {
            client.Connect(IPAddress.Loopback, _server.ListeningPort);
            WaitUntil(() => {
                lock (changes) {
                    return changes.Any(c => c.Status == ServiceStatus.Connected);
                }
            }, "the Connected status change");
        }

        _server.Stop();
        lock (changes) {
            Assert.Equal(ServiceStatus.Stopped, changes[^1].Status);
        }
    }

    [Fact]
    public void CommandReceived_CarriesServerReplyContextAndCommand() {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var context = _server.RegisterClient(socket);
        var received = new List<(Reply Reply, string Command)>();
        _server.CommandReceived += (reply, command) => received.Add((reply, command));

        byte[] data = "mute\nvolup\n"u8.ToArray();
        Array.Copy(data, context.DataBuffer, data.Length);
        Assert.True(_server.ProcessReceivedData(context, data.Length));

        Assert.Equal(2, received.Count);
        string[] commands = [.. received.Select(r => r.Command)];
        Assert.Equal(["mute", "volup"], commands);
        // The Reply is the per-connection ServerReplyContext, so command output goes back to
        // the right client; the old code smuggled this through an unchecked downcast.
        Assert.All(received, r => Assert.Same(context, r.Reply));
    }

    [Fact]
    public void ErrorOccurred_StartOnPortInUse_CarriesTypedSocketError() {
        // Occupy a port, then start the server on it: Start's SocketException path must
        // surface as a typed ServiceError (and the server must report Stopped).
        using Socket blocker = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        blocker.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        blocker.Listen(1);
        int port = ((IPEndPoint)blocker.LocalEndPoint!).Port;

        var errors = new List<ServiceError>();
        _server.ErrorOccurred += errors.Add;

        _server.Start(port, "loopback");

        Assert.Equal(ServiceStatus.Stopped, _server.CurrentStatus);
        ServiceError error = Assert.Single(errors);
        Assert.Equal(SocketError.AddressAlreadyInUse, error.SocketError);
        Assert.NotNull(error.HResult);
    }
}
