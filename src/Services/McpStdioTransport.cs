// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// The MCP stdio transport (#215): the newline-delimited JSON-RPC read/dispatch/write loop an MCP
/// client drives by launching <c>mcec.exe --mcp</c>. Extracted from the old monolithic
/// <c>AgentServer</c>; the production instance is wired by the <see cref="AgentServer"/> facade, and
/// tests construct their own instance with an injected dispatch delegate.
///
/// Each request line is dispatched on a worker so a slow call (a long <c>wait-for</c>, a deep
/// <c>query</c>) never blocks later requests (#113). JSON-RPC responses carry the request <c>id</c>,
/// so out-of-order completion is valid; writes are serialized so response lines never interleave.
///
/// BOUNDS (#215): the old loop's pending-task list grew unboundedly (one entry per request line for
/// the process lifetime) and had no concurrency cap, unlike HTTP's 503 bound (#151). Now completed
/// tasks are pruned each iteration and in-flight dispatches are capped at
/// <see cref="MaxConcurrentStdioRequests"/>; by BACKPRESSURE, not refusal: stdio has exactly one
/// local client (the operator's MCP client), so the reader simply stops consuming stdin until a slot
/// frees, which is both lossless and the natural pipe semantics.
/// </summary>
public sealed class McpStdioTransport {
    /// <summary>
    /// Upper bound on stdio requests dispatched concurrently. Mirrors
    /// <see cref="McpHttpTransport.MaxConcurrentHttpRequests"/> (#151): legitimate agent traffic is a
    /// handful of in-flight calls; past the cap the reader applies backpressure (stops reading stdin)
    /// instead of spawning unbounded tasks each holding a buffered request line.
    /// </summary>
    public const int MaxConcurrentStdioRequests = 16;

    private readonly Func<JsonObject, JsonObject?> _dispatch;
    private readonly SemaphoreSlim _workerSlots = new(MaxConcurrentStdioRequests, MaxConcurrentStdioRequests);

    /// <param name="dispatch">JSON-RPC dispatch for one request object; the production wiring tags
    /// <see cref="AgentTransport.Stdio"/>; the local, opt-in-preserving path (#153).</param>
    public McpStdioTransport(Func<JsonObject, JsonObject?> dispatch) {
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
    }

    /// <summary>
    /// Runs the newline-delimited JSON-RPC loop over the given streams (stdin/stdout for <c>--mcp</c>).
    /// Returns when the input stream reaches EOF.
    /// </summary>
    public void Run(Stream input, Stream output) {
        using StreamReader reader = new(input, new UTF8Encoding(false));
        using StreamWriter writer = new(output, new UTF8Encoding(false));
        writer.AutoFlush = true;
        Logger.Instance.Log4.Info("AgentServer: MCP stdio transport started.");
        Run(reader, writer);
        Logger.Instance.Log4.Info("AgentServer: MCP stdio transport ended (EOF).");
    }

    /// <summary>
    /// The stdio read/dispatch/write loop, factored over TextReader/TextWriter so it is testable.
    /// Returns when <paramref name="reader"/> reaches EOF, after every in-flight dispatch finished.
    /// </summary>
    public void Run(TextReader reader, TextWriter writer) {
        object writeLock = new();
        List<Task> pending = [];
        while (reader.ReadLine() is { } line) {
            if (line.Length == 0) {
                continue;
            }
            // Prune finished dispatches so the pending list tracks only in-flight work; the old
            // list accumulated one completed Task per request for the whole process lifetime.
            pending.RemoveAll(t => t.IsCompleted);
            // Concurrency cap by backpressure: don't read the next request until a slot frees.
            _workerSlots.Wait();
            string requestLine = line;
            pending.Add(Task.Run(() => {
                try {
                    JsonObject? response = ProcessLine(requestLine, _dispatch);
                    if (response is not null) {
                        lock (writeLock) {
                            writer.WriteLine(response.ToJsonString(AgentJson.Options));
                        }
                    }
                }
                finally {
                    _workerSlots.Release();
                }
            }));
            Volatile.Write(ref _pendingCount, pending.Count);
        }
        try {
            Task.WaitAll([.. pending]);
        }
        catch (AggregateException) {
            // Per-request faults are already turned into JSON-RPC error responses in ProcessLine.
        }
    }

    /// <summary>
    /// The number of pending dispatch tasks currently tracked by a running loop. Test seam
    /// (InternalsVisibleTo): read only while the loop is quiescent (blocked in ReadLine), where the
    /// list is stable; it lets tests prove the prune keeps the list from growing without bound.
    /// </summary>
    internal int PendingCountForTests => Volatile.Read(ref _pendingCount);

    private int _pendingCount;

    private static JsonObject? ProcessLine(string line, Func<JsonObject, JsonObject?> dispatch) {
        try {
            JsonNode? node = JsonNode.Parse(line);
            if (node is not JsonObject request) {
                return JsonRpcError(-32600, "Invalid Request");
            }
            return dispatch(request);
        }
        catch (JsonException e) {
            return JsonRpcError(-32700, $"Parse error: {e.Message}");
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: dispatch error: {e}");
            return JsonRpcError(-32603, $"Internal error: {e.Message}");
        }
    }

    /// <summary>
    /// A transport-level JSON-RPC error response (parse/dispatch fault). The id is null: a line that
    /// never parsed to a request object has no usable id.
    /// </summary>
    private static JsonObject JsonRpcError(int code, string message) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = null,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };
}
