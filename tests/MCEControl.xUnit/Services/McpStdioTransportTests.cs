// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the #215 stdio-loop bounds: the pending-task list is pruned each iteration (the old
/// loop kept one completed Task per request for the process lifetime) and in-flight dispatches are
/// capped at <see cref="McpStdioTransport.MaxConcurrentStdioRequests"/> by backpressure (the reader
/// stops consuming until a slot frees), mirroring the HTTP fan-out bound (#151).
/// </summary>
public class McpStdioTransportTests {
    static McpStdioTransportTests() {
        // These tests deliberately PARK a full cap's worth (16) of dispatches on blocking waits.
        // On a 2-core hosted CI runner the thread pool starts near the core count and injects
        // roughly one thread per second, so reaching 16 simultaneously-blocked workers takes
        // ~14s; losing the race against the tests' 15s timeouts (the post-merge CI flake on
        // #243). Guarantee the pool can field the whole cap (plus the overflow request and the
        // loop's own work) without waiting on thread injection.
        ThreadPool.GetMinThreads(out int workers, out int iocp);
        int needed = McpStdioTransport.MaxConcurrentStdioRequests + 8;
        if (workers < needed) {
            ThreadPool.SetMinThreads(needed, iocp);
        }
    }

    private static string Request(int id) => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"x\"}}";

    private static JsonObject Ok(JsonObject req) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = req["id"]?.DeepClone(), ["result"] = new JsonObject() };

    [Fact]
    public async Task Run_PrunesCompletedTasks_PendingListDoesNotGrowWithoutBound() {
        // Complete many requests, then park one more. When the loop accepts the parked request it
        // prunes the finished tasks first, so the pending list holds ~1 entry, not N+1. Read the
        // count only while the loop is quiescent (blocked in ReadLine), where the list is stable.
        const int completed = 200;
        using CountdownEvent responded = new(completed);
        using ManualResetEventSlim parkedEntered = new(false);
        using ManualResetEventSlim release = new(false);
        McpStdioTransport transport = new(req => {
            long id = req["id"]!.GetValue<long>();
            if (id == long.MaxValue) {
                parkedEntered.Set();
                release.Wait(15000);
            }
            else {
                responded.Signal();
            }
            return Ok(req);
        });
        FeedReader reader = new();
        StringWriter writer = new();
        Task loop = Task.Run(() => transport.Run(reader, writer));
        try {
            for (int i = 0; i < completed; i++) {
                reader.Feed(Request(i));
            }
            Assert.True(responded.Wait(15000), "the quick requests never all dispatched");

            reader.Feed($"{{\"jsonrpc\":\"2.0\",\"id\":{long.MaxValue},\"method\":\"x\"}}");
            Assert.True(parkedEntered.Wait(15000), "the parked request never dispatched");

            // The loop is now blocked in ReadLine; the completed tasks must have been pruned when the
            // parked request was accepted. Tasks signal `responded` before they finish their write, so
            // up to a cap's worth may still be completing; the invariant prune+cap guarantee is
            // pending <= cap + 1 (the old unpruned list would hold all `completed` + 1 here).
            Assert.True(transport.PendingCountForTests <= McpStdioTransport.MaxConcurrentStdioRequests + 1,
                $"pending list holds {transport.PendingCountForTests} tasks after {completed} completed requests; not pruned");
        }
        finally {
            release.Set();
            reader.Eof();
            await loop.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    [Fact]
    public async Task Run_CapsInFlightDispatches_ByBackpressure() {
        // Park every dispatch, then feed one request more than the cap. Exactly MaxConcurrent
        // dispatches may start; the overflow request must NOT dispatch until a slot frees; the
        // reader is parked on the semaphore, which is the lossless stdio equivalent of HTTP's 503.
        int cap = McpStdioTransport.MaxConcurrentStdioRequests;
        using CountdownEvent capReached = new(cap);
        using ManualResetEventSlim release = new(false);
        int inFlight = 0, peak = 0;
        McpStdioTransport transport = new(req => {
            // Intentional (both lines): the captured counters are what the test observes across threads.
            // ReSharper disable once AccessToModifiedClosure
            int now = Interlocked.Increment(ref inFlight);
            int seen;
            // ReSharper disable once AccessToModifiedClosure
            while (now > (seen = Volatile.Read(ref peak))) {
                Interlocked.CompareExchange(ref peak, now, seen);
            }
            // Only the first `cap` requests count down; the overflow request dispatches after the
            // event already reached zero (signaling then would throw).
            if (req["id"]!.GetValue<long>() < cap) {
                capReached.Signal();
            }
            release.Wait(15000);
            Interlocked.Decrement(ref inFlight);
            return Ok(req);
        });
        FeedReader reader = new();
        StringWriter writer = new();
        Task loop = Task.Run(() => transport.Run(reader, writer));
        try {
            for (int i = 0; i < cap + 1; i++) {
                reader.Feed(Request(i));
            }
            Assert.True(capReached.Wait(15000), "the cap's worth of requests never all dispatched");

            // Give the loop a chance to (incorrectly) dispatch the overflow request; the in-flight
            // count must hold at the cap.
            await Task.Delay(250);
            Assert.Equal(cap, Volatile.Read(ref inFlight));

            release.Set();
            reader.Eof();
            await loop.WaitAsync(TimeSpan.FromSeconds(15));

            // Everyone (including the overflow request) eventually ran, and concurrency never
            // exceeded the cap.
            Assert.True(Volatile.Read(ref peak) <= cap, $"in-flight peak {peak} exceeded the cap {cap}");
            Assert.Contains($"\"id\":{cap}", writer.ToString(), StringComparison.Ordinal);
        }
        finally {
            release.Set();
            reader.Eof(); // idempotent enough for cleanup: unblocks a loop leaked by a failed assert
        }
    }

    [Fact]
    public void Run_MalformedLine_YieldsParseError_AndLoopKeepsServing() {
        StringReader reader = new("this is not json\n" + Request(7) + "\n");
        StringWriter writer = new();

        new McpStdioTransport(Ok).Run(reader, writer);

        string output = writer.ToString();
        Assert.Contains("-32700", output, StringComparison.Ordinal);
        Assert.Contains("\"id\":7", output, StringComparison.Ordinal);
    }
}
