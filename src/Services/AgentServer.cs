// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// MCEC 3.0's agent front door: a self-contained Model Context Protocol (MCP) server, hand-rolled as
/// JSON-RPC 2.0 over two transports; stdio (for an MCP client that launches <c>mcec.exe --mcp</c>)
/// and a localhost HTTP/JSON floor (POST a JSON-RPC request to <c>/mcp</c>). No external SDK or
/// Python/Node runtime: the same self-contained native binary, with MCP/HTTP as just one more
/// transport over the existing command core.
///
/// Since #215 this is a THIN STATIC FACADE that wires the production instances of the split layers
/// and re-exposes the few members the hosts compile against:
/// <list type="bullet">
/// <item><see cref="McpHttpTransport"/>; the HTTP listener lifecycle, the pure #143 Host/Origin/
/// bearer gate, and the #152 loopback-bind canonicalization.</item>
/// <item><see cref="McpStdioTransport"/>; the stdio read/dispatch/write loop (pruned + capped).</item>
/// <item><see cref="JsonRpcDispatcher"/>; the JSON-RPC 2.0 protocol layer shared by both.</item>
/// <item><see cref="AgentToolExecutor"/>; gate → catalog → build → dispatch → #101 envelope.</item>
/// </list>
///
/// SECURITY: the gated agent tools (the <see cref="ToolCatalog"/> set; capture, query, displays,
/// find, wait-for, invoke, drag, click, record, launch) only run when
/// <see cref="AgentRuntime.AgentCommandsEnabled"/> is true; otherwise a tool call is reported as an
/// error (enforced in <see cref="AgentToolExecutor"/>). Every tool call is loudly audit-logged.
/// </summary>
public static class AgentServer {
    public const string ProtocolVersion = JsonRpcDispatcher.ProtocolVersion;

    // Re-exposed so existing call sites (UiaService doc contract, Program.Main's reap,
    // CommandInvokerDispatcherTests' timing math) keep one canonical constant name.
    public const int InvokeModalGraceMs = AgentToolExecutor.InvokeModalGraceMs;
    public const int SendCommandCompletionTimeoutMs = AgentToolExecutor.SendCommandCompletionTimeoutMs;
    public const int SessionReapAgeHours = AgentToolExecutor.SessionReapAgeHours;

    /// <summary>
    /// The production executor: tool gating/dispatch bound to the ambient <see cref="AgentRuntime"/>
    /// (settings, invoker, session) via accessors, so live settings changes are always observed.
    /// </summary>
    private static readonly AgentToolExecutor Executor = new(
        () => AgentRuntime.Settings,
        () => AgentRuntime.Invoker,
        () => AgentRuntime.Session);

    /// <summary>The production protocol layer, serving the embedded <see cref="Instructions"/>.</summary>
    private static readonly JsonRpcDispatcher Dispatcher = new(Executor, () => Instructions);

    /// <summary>
    /// The production HTTP transport instance (#215). Tests construct their own transport with an
    /// injected dispatch delegate instead of the old <c>HttpDispatchOverride</c> static seam.
    /// </summary>
    private static readonly McpHttpTransport Http = new(
        () => AgentRuntime.Settings,
        req => Dispatch(req, AgentTransport.Http));

    /// <summary>
    /// Whether the MCP HTTP transport is currently listening. Read-only status for the GUI's
    /// status strip (#211).
    /// </summary>
    internal static bool IsHttpListening => Http.IsListening;

    /// <summary>
    /// Starts the localhost MCP/HTTP front door. SECURITY: refused from the installed (Program Files)
    /// copy; the operator-owned install never serves agents (see
    /// <see cref="Program.IsProgramFilesInstall"/>). Provisioned sessions and manual copies run from
    /// writable locations and are unaffected.
    /// </summary>
    public static void StartHttp() {
        if (Program.IsProgramFilesInstall) {
            Logger.Instance.Log4.Error(
                $"AgentServer: MCP/HTTP server refused from the installed location. {Program.InstalledAgentServingGuidance}");
            return;
        }
        Http.Start();
    }

    public static void StopHttp() => Http.Stop();

    /// <summary>
    /// Runs the newline-delimited JSON-RPC loop over the given streams (stdin/stdout for <c>--mcp</c>).
    /// Returns when the input stream reaches EOF. The loop itself; worker fan-out, pending-task
    /// pruning, and the concurrency cap; lives in <see cref="McpStdioTransport"/> (#215).
    /// </summary>
    public static void RunStdio(Stream input, Stream output) =>
        new McpStdioTransport(req => Dispatch(req, AgentTransport.Stdio)).Run(input, output);

    /// <summary>
    /// Dispatches a single JSON-RPC request object through the production
    /// <see cref="JsonRpcDispatcher"/>; null for a notification. <paramref name="transport"/> defaults
    /// to <see cref="AgentTransport.Stdio"/>; the local, opt-in-preserving path; the HTTP floor passes
    /// <see cref="AgentTransport.Http"/> so <c>send_command</c> honors the network gate (#153).
    /// </summary>
    public static JsonObject? Dispatch(JsonObject request, AgentTransport transport = AgentTransport.Stdio) =>
        Dispatcher.Dispatch(request, transport);

    /// <summary>See <see cref="AgentToolExecutor.SerializesOnInputLock"/> (the #113 contract).</summary>
    public static bool SerializesOnInputLock(string tool) => AgentToolExecutor.SerializesOnInputLock(tool);

    /// <summary>See <see cref="AgentToolExecutor.BuildCommand"/> (#201/#205). Kept for tests (InternalsVisibleTo).</summary>
    internal static Command? BuildCommand(string name, JsonObject args) => AgentToolExecutor.BuildCommand(name, args);

    /// <summary>See <see cref="AgentToolExecutor.RunAgentCommand"/>. Kept for tests (InternalsVisibleTo).</summary>
    internal static JsonObject RunAgentCommand(string name, JsonObject args) => Executor.RunAgentCommand(name, args);

    /// <summary>
    /// Built-in guidance handed to an agent at connect time (the MCP client shows this to the model). It
    /// is authored in <c>src/Agent/AgentInstructions.md</c> (the single source of truth) and embedded
    /// into the exe at build time; this loads it once, collapsing each blank-line-separated paragraph to
    /// a single line (the historical connect-time format).
    /// </summary>
    public static string Instructions => LazyInstructions.Value;

    private static readonly Lazy<string> LazyInstructions = new(LoadInstructions);

    private static string LoadInstructions() {
        using Stream? stream = typeof(AgentServer).Assembly.GetManifestResourceStream("MCEControl.AgentInstructions.md");
        if (stream is null) {
            throw new InvalidOperationException(
                "Embedded resource 'MCEControl.AgentInstructions.md' not found; check the <EmbeddedResource> item in MCEControl.csproj.");
        }
        using StreamReader reader = new(stream);
        string raw = reader.ReadToEnd().Replace("\r\n", "\n");
        // Authored wrapped for readability; the model receives one line per blank-line-separated paragraph.
        string[] paragraphs = raw.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < paragraphs.Length; i++) {
            string[] lines = paragraphs[i].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < lines.Length; j++) {
                lines[j] = lines[j].Trim();
            }
            paragraphs[i] = string.Join(" ", lines);
        }
        return string.Join("\n", paragraphs);
    }
}
