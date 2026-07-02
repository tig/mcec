//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Regression tests for issue #212: SocketClient's lifecycle is one async run per Start().
///   - Stop() while receiving must not throw: the pre-#212 Stop() nulled fields the read
///     loop was dereferencing on another thread, NREing on every "Act as client" toggle.
///   - Double-Start() must supersede (not orphan) the prior run and its TcpClient.
///   - The client must auto-reconnect after the server drops, waiting the configured delay.
///   - Received bytes are read in buffered async chunks — no 100 ms-per-command cap, no
///     one-ReadByte-per-syscall — and decoded as UTF-8 with a stateful Decoder.
/// Loopback only: a local TcpListener on an ephemeral port plays the remote server, and
/// waits poll instead of sleeping fixed times (the #202 house pattern).
/// </summary>
public class SocketClientLifecycleTests {
    public SocketClientLifecycleTests() {
        // SetStatus dereferences TelemetryService.Instance.TelemetryClient — null until
        // telemetry is initialized (see AgentTestSupport).
        AgentTestSupport.EnsureTelemetry();
    }

    /// <summary>Polls (like the other loopback suites) instead of sleeping a fixed time, so the
    /// test is fast when the client is fast and only slow when something is actually wrong.</summary>
    private static void WaitUntil(Func<bool> condition, string because, int timeoutMs = 10000) {
        var sw = Stopwatch.StartNew();
        while (!condition()) {
            Assert.True(sw.ElapsedMilliseconds < timeoutMs, $"Timed out waiting for {because}");
            Thread.Sleep(10);
        }
    }

    private static TcpListener NewListener(out int port) {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static SocketClient NewClient(int port, int delayTime, out List<string> received) {
        var settings = new AppSettings {
            ClientHost = "127.0.0.1",
            ClientPort = port,
            ClientDelayTime = delayTime,
        };
        var client = new SocketClient(settings);
        var list = new List<string>();
        client.CommandReceived += (reply, command) => {
            lock (list) {
                list.Add(command);
            }
        };
        received = list;
        return client;
    }

    private static TcpClient Accept(TcpListener listener, string because) {
        WaitUntil(listener.Pending, because);
        return listener.AcceptTcpClient();
    }

    private static int CountReceived(List<string> received) {
        lock (received) {
            return received.Count;
        }
    }

    // ---- Connect and receive ----

    [Fact]
    public void Client_ConnectsAndReceivesCommand() {
        using TcpListener listener = NewListener(out int port);
        using SocketClient client = NewClient(port, 0, out List<string> received);

        client.Start();
        using TcpClient server = Accept(listener, "the client to connect");
        WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected, "the client to report Connected");

        byte[] data = Encoding.UTF8.GetBytes("mute\n");
        server.GetStream().Write(data, 0, data.Length);

        WaitUntil(() => CountReceived(received) == 1, "the command to arrive");
        lock (received) {
            Assert.Equal(new[] { "mute" }, received);
        }

        client.Stop();
        Assert.Equal(ServiceStatus.Stopped, client.CurrentStatus);
    }

    [Fact]
    public void Receive_Utf8CharSplitAcrossReads_DecodesCorrectly() {
        // #212: receive decodes UTF-8 with a stateful Decoder — the old loop cast each
        // byte to char, so a multi-byte character (here 'é' = 0xC3 0xA9) was mangled,
        // and splitting it across two reads must not corrupt it either.
        using TcpListener listener = NewListener(out int port);
        using SocketClient client = NewClient(port, 0, out List<string> received);

        client.Start();
        using TcpClient server = Accept(listener, "the client to connect");
        WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected, "the client to report Connected");

        byte[] data = Encoding.UTF8.GetBytes("café\n"); // 'é' is two bytes
        NetworkStream stream = server.GetStream();
        stream.Write(data, 0, 4); // "caf" + the FIRST byte of 'é'
        stream.Flush();
        Thread.Sleep(50); // let the client drain the partial read before the rest arrives
        stream.Write(data, 4, data.Length - 4);

        WaitUntil(() => CountReceived(received) == 1, "the command to arrive");
        lock (received) {
            Assert.Equal(new[] { "café" }, received);
        }
    }

    // ---- Throughput: no 100 ms-per-command cap (#212) ----

    [Fact]
    public void Receive_BurstOfCommands_AllArrive_WithNoPerCommandDelay() {
        using TcpListener listener = NewListener(out int port);
        using SocketClient client = NewClient(port, 0, out List<string> received);

        client.Start();
        using TcpClient server = Accept(listener, "the client to connect");
        WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected, "the client to report Connected");

        const int n = 100;
        string[] expected = [.. Enumerable.Range(0, n).Select(i => $"cmd{i}")];
        byte[] burst = Encoding.UTF8.GetBytes(string.Join("\n", expected) + "\n");
        server.GetStream().Write(burst, 0, burst.Length);

        // The pre-#212 loop slept 100 ms per dispatched command (10 commands/sec), so
        // 100 commands took over 10 seconds; the async loop must deliver them promptly.
        WaitUntil(() => CountReceived(received) >= n, $"all {n} commands to arrive", timeoutMs: 5000);
        lock (received) {
            Assert.Equal(expected, received);
        }
    }

    // ---- Stop() while receiving must not throw (#212) ----

    [Fact]
    public void Stop_WhileReceiving_DoesNotThrow_AndRunCompletesCleanly() {
        // The pre-#212 bug was a race (Stop nulled fields the read loop dereferences),
        // so run several iterations of stop-mid-stream.
        for (int iteration = 0; iteration < 5; iteration++) {
            using TcpListener listener = NewListener(out int port);
            using SocketClient client = NewClient(port, 0, out List<string> received);

            client.Start();
            using TcpClient server = Accept(listener, $"the client to connect (iteration {iteration})");
            WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected,
                $"the client to report Connected (iteration {iteration})");

            // Pump commands at the client from another thread while we stop it.
            NetworkStream stream = server.GetStream();
            byte[] chunk = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("volup\n", 50)));
            Task pump = Task.Run(() => {
                try {
                    for (int i = 0; i < 100; i++) {
                        stream.Write(chunk, 0, chunk.Length);
                    }
                }
                catch (IOException) {
                    // client vanished mid-pump — expected when Stop wins the race
                }
                catch (ObjectDisposedException) {
                    // ditto
                }
            });

            WaitUntil(() => CountReceived(received) > 0, $"receiving to begin (iteration {iteration})");

            Task? run = client.RunTask;
            Assert.NotNull(run);

            var ex = Record.Exception(client.Stop);

            Assert.Null(ex);
            WaitUntil(() => run!.IsCompleted, $"the run to complete after Stop (iteration {iteration})");
            Assert.False(run!.IsFaulted, $"run faulted (iteration {iteration}): {run.Exception}");
            WaitUntil(() => pump.IsCompleted, $"the pump to complete (iteration {iteration})");
        }
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow() {
        using SocketClient client = NewClient(1, 0, out _);

        var ex = Record.Exception(client.Stop);

        Assert.Null(ex);
        Assert.Equal(ServiceStatus.Stopped, client.CurrentStatus);
    }

    // ---- Double-Start supersedes; no orphans (#212) ----

    [Fact]
    public void Start_Twice_SupersedesFirstRun_AndSecondConnectionWorks() {
        using TcpListener listener = NewListener(out int port);
        using SocketClient client = NewClient(port, 0, out List<string> received);

        client.Start();
        using TcpClient first = Accept(listener, "the first run to connect");
        WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected, "the first run to report Connected");
        Task? firstRun = client.RunTask;
        Assert.NotNull(firstRun);

        // Second Start supersedes the first run: pre-#212 this orphaned the previous
        // worker and TcpClient (they kept running, untracked, forever).
        client.Start();

        WaitUntil(() => firstRun!.IsCompleted, "the superseded run to complete");
        Assert.False(firstRun!.IsFaulted, $"superseded run faulted: {firstRun.Exception}");

        // The superseded run closed ITS OWN TcpClient: the first accepted socket sees EOF.
        first.ReceiveTimeout = 5000;
        int eof;
        try {
            eof = first.GetStream().Read(new byte[1], 0, 1);
        }
        catch (IOException) {
            eof = 0; // an abortive close (RST) is also "connection gone", not an orphan
        }
        Assert.Equal(0, eof);

        // The second run's connection is live and receives commands.
        using TcpClient second = Accept(listener, "the second run to connect");
        byte[] data = Encoding.UTF8.GetBytes("mute\n");
        second.GetStream().Write(data, 0, data.Length);
        WaitUntil(() => CountReceived(received) == 1, "a command over the second connection");
        lock (received) {
            Assert.Equal(new[] { "mute" }, received);
        }
    }

    // ---- Auto-reconnect after server drop (#212) ----

    [Fact]
    public void Client_ReconnectsAfterServerDrop_WithConfiguredDelay() {
        using TcpListener listener = NewListener(out int port);
        // A short (but nonzero) reconnect delay: nonzero is what enables auto-reconnect.
        using SocketClient client = NewClient(port, 100, out List<string> received);

        client.Start(); // delay: false — first connect is immediate
        using (TcpClient firstConn = Accept(listener, "the initial connection")) {
            WaitUntil(() => client.CurrentStatus == ServiceStatus.Connected, "the client to report Connected");
        } // dispose = the server drops the connection

        // The client must come back on its own after the configured delay...
        using TcpClient secondConn = Accept(listener, "the client to reconnect after the drop");

        // ...and the new connection must actually work.
        byte[] data = Encoding.UTF8.GetBytes("mute\n");
        secondConn.GetStream().Write(data, 0, data.Length);
        WaitUntil(() => CountReceived(received) == 1, "a command over the reconnected connection");
        lock (received) {
            Assert.Equal(new[] { "mute" }, received);
        }

        client.Stop();
    }
}
