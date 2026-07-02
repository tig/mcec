// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the #215 transport-lifecycle fix: <see cref="McpHttpTransport.Stop"/> must JOIN the
/// accept thread and DRAIN the in-flight worker pool (both bounded) before returning, so a
/// Settings-dialog Stop/Start can never overlap old workers (still executing tool calls) with a new
/// listener. The old close-and-null Stop returned immediately, leaving workers running.
/// </summary>
[Collection("AgentSerial")]
public class McpHttpTransportStopTests {
    /// <summary>Asks the OS for a free loopback TCP port by binding to port 0 and reading the assignment.</summary>
    private static int FindFreeLoopbackPort() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        try {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally {
            listener.Stop();
        }
    }

    private static Task<HttpResponseMessage> PostPingAsync(HttpClient client, string url, int id) {
        StringContent content = new($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"ping\"}}", Encoding.UTF8, "application/json");
        return client.PostAsync(url, content);
    }

    [Fact]
    public async Task Stop_WaitsForInFlightWorker_NoOverlapAfterReturn() {
        // A request is parked inside dispatch (the seam) when Stop is called. Stop must block until
        // that worker finishes; and once Stop returns, the worker must be provably done. This is the
        // "no overlap" contract: post-Stop there is no old worker left to interleave with a restart.
        int port = FindFreeLoopbackPort();
        AppSettings settings = new() { McpBindAddress = "127.0.0.1", McpHttpPort = port };
        string url = $"http://127.0.0.1:{port}/mcp";
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        bool dispatchExited = false;
        McpHttpTransport transport = new(() => settings, req => {
            entered.Set();
            release.Wait(10000);
            Volatile.Write(ref dispatchExited, true);
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = req["id"]?.DeepClone(), ["result"] = new JsonObject() };
        });
        transport.Start();
        try {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            Task<HttpResponseMessage> parked = PostPingAsync(client, url, 1);
            Assert.True(entered.Wait(10000), "request never reached dispatch");

            Task stop = Task.Run(transport.Stop);

            // Stop closes the listener before draining; wait (bounded) for that so we know Stop is
            // actually underway; under load Task.Run scheduling alone can take hundreds of ms.
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (transport.IsListening && sw.ElapsedMilliseconds < 10000) {
                await Task.Delay(10);
            }
            Assert.False(transport.IsListening, "Stop never closed the listener");

            // While the worker is parked (holding its slot), Stop must NOT complete; it is
            // draining, not abandoning. Deterministic: the drain cannot finish until release fires.
            await Task.Delay(200);
            Assert.False(stop.IsCompleted, "Stop returned while a worker was still in flight");

            release.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(10));

            // The ordering proof: by the time Stop returned, the worker had exited dispatch.
            Assert.True(Volatile.Read(ref dispatchExited), "Stop returned before the in-flight worker finished");

            // The parked request's client task ends one way or another (a response or an aborted
            // connection from the closed listener); it must not hang.
            try {
                (await parked.WaitAsync(TimeSpan.FromSeconds(10))).Dispose();
            }
            catch (HttpRequestException) {
                // An aborted connection is acceptable; the listener was closed under the request.
            }
        }
        finally {
            release.Set();
            transport.Stop();
        }
    }

    [Fact]
    public async Task StopThenStart_SameInstance_ServesRequestsFromAQuiescedPool() {
        // The Settings-dialog scenario: Stop then Start on the same transport. After the drain, the
        // restarted listener must serve requests normally (all worker slots back, no stale listener).
        int port = FindFreeLoopbackPort();
        AppSettings settings = new() { McpBindAddress = "127.0.0.1", McpHttpPort = port };
        string url = $"http://127.0.0.1:{port}/mcp";
        McpHttpTransport transport = new(() => settings,
            req => new JsonObject { ["jsonrpc"] = "2.0", ["id"] = req["id"]?.DeepClone(), ["result"] = new JsonObject() });
        try {
            transport.Start();
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            using (HttpResponseMessage first = await PostPingAsync(client, url, 1)) {
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            }

            transport.Stop();
            Assert.False(transport.IsListening);

            transport.Start();
            Assert.True(transport.IsListening);
            using (HttpResponseMessage second = await PostPingAsync(client, url, 2)) {
                Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            }
        }
        finally {
            transport.Stop();
        }
    }

    [Fact]
    public void Stop_WhenNeverStarted_IsANoOp() {
        McpHttpTransport transport = new(() => null, _ => null);
        transport.Stop(); // must not throw or block
        Assert.False(transport.IsListening);
    }
}
