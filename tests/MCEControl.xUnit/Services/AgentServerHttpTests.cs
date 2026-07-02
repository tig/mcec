// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the HTTP transport hardening (#151): the request body is capped (a huge POST must be
/// rejected with 413 instead of being buffered into memory twice), the cap holds even for a chunked
/// body that carries no Content-Length header, and the per-request worker fan-out is bounded (a
/// saturated server answers 503 instead of spawning unbounded tasks). These drive a REAL
/// HttpListener on a free loopback port; the same code path production uses.
/// </summary>
[Collection("AgentSerial")]
public class AgentServerHttpTests {
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

    private static string StartServer() {
        int port = FindFreeLoopbackPort();
        AgentRuntime.Settings = new AppSettings { McpBindAddress = "127.0.0.1", McpHttpPort = port };
        AgentServer.StartHttp();
        return $"http://127.0.0.1:{port}/mcp";
    }

    private static void StopServer() {
        AgentServer.StopHttp();
        AgentRuntime.Settings = null;
    }

    [Fact]
    public async Task Http_NormalRequest_StillWorks_Returns200WithJsonRpcResult() {
        string url = StartServer();
        try {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            StringContent content = new("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}", Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await client.PostAsync(url, content);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            JsonObject body = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsObject();
            Assert.Equal(1, body["id"]!.GetValue<int>());
            Assert.NotNull(body["result"]);
        }
        finally {
            StopServer();
        }
    }

    [Fact]
    public async Task Http_ContentLengthOverCap_Returns413_NotParsedOrDispatched() {
        // A body one byte over the cap, declared honestly via Content-Length. The handler must refuse
        // it from the header alone (413) WITHOUT buffering it. Under the old unbounded code this body
        // would be read whole and fed to JsonNode.Parse, coming back 200 with a -32700 parse error;
        // so a 413 here proves the reject path ran instead of the read-everything path.
        string url = StartServer();
        try {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            byte[] payload = new byte[AgentServer.MaxHttpBodyBytes + 1];
            Array.Fill(payload, (byte)'x');
            ByteArrayContent content = new(payload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using HttpResponseMessage resp = await client.PostAsync(url, content);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
        }
        finally {
            StopServer();
        }
    }

    [Fact]
    public void TryReadBoundedBody_AtTheCap_ReadsWholeBody() {
        byte[] payload = new byte[AgentServer.MaxHttpBodyBytes];
        Array.Fill(payload, (byte)'a');

        bool ok = AgentServer.TryReadBoundedBody(new System.IO.MemoryStream(payload), Encoding.UTF8, out string body);

        Assert.True(ok);
        Assert.Equal(AgentServer.MaxHttpBodyBytes, body.Length);
    }

    [Fact]
    public void TryReadBoundedBody_OneByteOverTheCap_RefusesWithoutBuffering() {
        byte[] payload = new byte[AgentServer.MaxHttpBodyBytes + 1];
        Array.Fill(payload, (byte)'a');

        bool ok = AgentServer.TryReadBoundedBody(new System.IO.MemoryStream(payload), Encoding.UTF8, out string body);

        Assert.False(ok);
        Assert.Equal("", body);
    }

    [Fact]
    public async Task Http_MoreConcurrentRequestsThanWorkerSlots_OverflowGets503_OthersComplete() {
        // #151 fan-out bound: saturate every worker slot with a request parked inside dispatch, then
        // send one more; it must be refused with 503 rather than spawn an unbounded worker. Releasing
        // the parked dispatches lets all the saturating requests finish normally (200). Deterministic:
        // the overflow request is only sent after the CountdownEvent proves all slots are occupied.
        string url = StartServer();
        using System.Threading.CountdownEvent allParked = new(AgentServer.MaxConcurrentHttpRequests);
        using System.Threading.ManualResetEventSlim release = new(false);
        AgentServer.HttpDispatchOverride = req => {
            allParked.Signal();
            release.Wait(30000);
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = req["id"]?.DeepClone(), ["result"] = new JsonObject() };
        };
        try {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
            Task<HttpResponseMessage>[] saturating = new Task<HttpResponseMessage>[AgentServer.MaxConcurrentHttpRequests];
            for (int i = 0; i < saturating.Length; i++) {
                StringContent content = new($"{{\"jsonrpc\":\"2.0\",\"id\":{i},\"method\":\"ping\"}}", Encoding.UTF8, "application/json");
                saturating[i] = client.PostAsync(url, content);
            }
            Assert.True(allParked.Wait(30000), "worker slots never all filled");

            StringContent overflowContent = new("{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"ping\"}", Encoding.UTF8, "application/json");
            using HttpResponseMessage overflow = await client.PostAsync(url, overflowContent);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, overflow.StatusCode);

            release.Set();
            foreach (Task<HttpResponseMessage> t in saturating) {
                using HttpResponseMessage resp = await t;
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            }
        }
        finally {
            release.Set();
            AgentServer.HttpDispatchOverride = null;
            StopServer();
        }
    }

    [Fact]
    public async Task Http_ChunkedBodyOverCap_NoContentLength_Returns413FromBoundedReader() {
        // Chunked transfer carries no Content-Length header (ContentLength64 == -1), so the header
        // check alone can't refuse it; this proves the READER is bounded: it must stop and reject as
        // soon as the cap is crossed, not buffer the whole stream. Old code returned 200 (-32700).
        string url = StartServer();
        try {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            byte[] payload = new byte[(AgentServer.MaxHttpBodyBytes * 2) + 1];
            Array.Fill(payload, (byte)'x');
            using HttpRequestMessage request = new(HttpMethod.Post, url) { Content = new ByteArrayContent(payload) };
            request.Headers.TransferEncodingChunked = true;

            using HttpResponseMessage resp = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
        }
        finally {
            StopServer();
        }
    }
}
