// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// The MCP HTTP transport (#215): owns the <see cref="HttpListener"/> lifecycle for the localhost
/// JSON-RPC floor (POST a JSON-RPC request to <c>/mcp</c>). Extracted from the old monolithic
/// <c>AgentServer</c>; the production instance is wired by the <see cref="AgentServer"/> facade,
/// and tests construct their own instance with an injected dispatch delegate (replacing the old
/// <c>HttpDispatchOverride</c> static seam).
///
/// SECURITY: the listener binds to <see cref="AppSettings.McpBindAddress"/> (127.0.0.1 by default).
/// A loopback bind is canonicalized before it reaches <see cref="HttpListener"/> (#152, see
/// <see cref="TryGetLoopbackPrefixHost"/>) so obfuscated loopback spellings can't slip a wildcard
/// binding past validation; a non-loopback bind is a deliberate off-box exposure and is allowed only
/// when <see cref="AppSettings.McpAuthToken"/> is set (#143); otherwise <see cref="Start"/> refuses
/// to start with a loud error. Every inbound request is validated by the pure
/// <see cref="GateHttpRequest"/> (Host/Origin/token/path) BEFORE its body is read or dispatched.
///
/// LIFECYCLE: <see cref="Stop"/> closes the listener, JOINS the accept thread (bounded), and DRAINS
/// the in-flight worker pool (bounded); so a Settings-dialog Stop/Start can never overlap old
/// workers with a new listener (#215; the old <c>StopHttp</c> just closed and nulled).
/// </summary>
public sealed class McpHttpTransport {
    /// <summary>
    /// Largest HTTP request body the /mcp endpoint accepts, in bytes (#151). Real JSON-RPC requests
    /// are a few KB; 1 MB leaves generous headroom while making a memory-exhaustion POST impossible
    /// (the old unbounded read buffered the whole body, then JsonNode.Parse built a second copy).
    /// Oversized requests are refused with HTTP 413.
    /// </summary>
    public const long MaxHttpBodyBytes = 1024 * 1024;

    /// <summary>
    /// Upper bound on HTTP requests served concurrently (#151). Each accepted request runs on its own
    /// worker task (#113); without a bound, a request flood spawns unbounded tasks each holding a
    /// buffered body. Legitimate agent traffic is a handful of in-flight calls; past this cap the
    /// server answers 503 instead of queueing.
    /// </summary>
    public const int MaxConcurrentHttpRequests = 16;

    /// <summary>
    /// Upper bound <see cref="Stop"/> waits for the accept thread to exit and, separately, for the
    /// in-flight workers to drain. A worker stuck in a pathologically slow dispatch past this bound
    /// is logged and abandoned (it is a background task holding no listener resources) rather than
    /// wedging the operator's Stop.
    /// </summary>
    public const int StopDrainTimeoutMs = 5_000;

    private readonly Func<AppSettings?> _settings;
    private readonly Func<JsonObject, JsonObject?> _dispatch;
    private readonly SemaphoreSlim _workerSlots = new(MaxConcurrentHttpRequests, MaxConcurrentHttpRequests);
    private readonly object _gate = new();
    private HttpListener? _listener;
    private Thread? _acceptThread;

    /// <param name="settings">Accessor for the live settings (bind address, port, auth token).</param>
    /// <param name="dispatch">JSON-RPC dispatch for one request object; the production wiring tags
    /// <see cref="AgentTransport.Http"/> so the transport-sensitive <c>send_command</c> gate (#153)
    /// sees the network transport.</param>
    public McpHttpTransport(Func<AppSettings?> settings, Func<JsonObject, JsonObject?> dispatch) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
    }

    /// <summary>
    /// Whether the transport is currently listening. Read-only status for the GUI's status strip (#211).
    /// </summary>
    public bool IsListening {
        get {
            lock (_gate) {
                return _listener is not null;
            }
        }
    }

    public void Start() {
        AppSettings? settings = _settings();
        if (settings is null) {
            return;
        }
        lock (_gate) {
            if (_listener is not null) {
                return;
            }
            // SECURITY (#152 + #143): two composed rules decide the bind.
            //
            // A LOOPBACK bind is the safe default and needs no auth, but the raw settings string is
            // canonicalized first (#152); built into the prefix as a normalized loopback literal rather
            // than passed through verbatim; so obfuscated spellings (e.g. "0x7f.0.0.1", "2130706433",
            // "::ffff:127.0.0.1") that http.sys would otherwise treat as wildcard hostname registrations
            // can't slip a non-loopback bind past validation.
            //
            // A NON-LOOPBACK bind is a deliberate off-box exposure and is only allowed with a bearer token
            // (#143): the Host/Origin gate defeats browser CSRF/DNS-rebinding but the Host header is
            // attacker-controlled and is not a network control, so an unauthenticated off-box bind would
            // hand UI automation + screen capture to the network. Without a token we refuse to start.
            string prefix;
            if (TryGetLoopbackPrefixHost(settings.McpBindAddress, out string prefixHost)) {
                prefix = $"http://{prefixHost}:{settings.McpHttpPort}/";
            }
            else if (!string.IsNullOrEmpty(settings.McpAuthToken)) {
                // Deliberate, authenticated off-box bind (#143). The raw operator-chosen address is used
                // as-is; canonicalization only applies to the loopback path.
                prefix = $"http://{settings.McpBindAddress}:{settings.McpHttpPort}/";
            }
            else {
                Logger.Instance.Log4.Error(
                    $"AgentServer: REFUSING to start the MCP HTTP transport: McpBindAddress '{settings.McpBindAddress}' " +
                    "is not a loopback address and no McpAuthToken is set. An unauthenticated non-loopback bind would " +
                    "expose UI automation and screen capture to the network. Either set <McpBindAddress> to " +
                    "\"localhost\", \"127.0.0.1\", \"::1\", or another literal loopback IP (127.x.y.z), or set " +
                    "<McpAuthToken> to deliberately expose the door off-box. Wildcards (\"+\", \"*\") and " +
                    "all-interfaces addresses (\"0.0.0.0\", \"::\") are never loopback and still require a token.");
                return;
            }
            HttpListener listener = new();
            listener.Prefixes.Add(prefix);
            try {
                listener.Start();
            }
            catch (HttpListenerException e) {
                Logger.Instance.Log4.Error($"AgentServer: could not start HTTP listener on {prefix}: {e.Message}");
                return;
            }
            _listener = listener;
            Logger.Instance.Log4.Info($"AgentServer: MCP HTTP transport listening on {prefix}mcp");
            _acceptThread = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "MCEC-AgentHttp" };
            _acceptThread.Start();
        }
    }

    /// <summary>
    /// Stops the transport: closes the listener, joins the accept thread (bounded by
    /// <see cref="StopDrainTimeoutMs"/>), and drains the in-flight worker pool (same bound); so by
    /// the time Stop returns no old worker is still running and a subsequent <see cref="Start"/>
    /// begins from a quiesced pool. The old close-and-null Stop let a Settings-dialog Stop/Start
    /// overlap stale workers (still executing tool calls) with the new listener (#215).
    /// </summary>
    public void Stop() {
        HttpListener listener;
        Thread? acceptThread;
        lock (_gate) {
            if (_listener is null) {
                return;
            }
            Logger.Instance.Log4.Info("AgentServer: stopping HTTP transport.");
            listener = _listener;
            acceptThread = _acceptThread;
            _listener = null;
            _acceptThread = null;
        }
        listener.Close();
        if (acceptThread is not null && !acceptThread.Join(StopDrainTimeoutMs)) {
            Logger.Instance.Log4.Warn(
                $"AgentServer: HTTP accept thread did not exit within {StopDrainTimeoutMs}ms after Stop.");
        }
        DrainWorkers();
    }

    /// <summary>
    /// Waits (bounded) for every in-flight request worker to release its slot by acquiring the full
    /// semaphore count, then hands the slots back. The accept loop is already dead when this runs, so
    /// no new worker can start mid-drain; a worker that outlives the bound (a hung tool call) is
    /// logged and abandoned rather than wedging Stop forever.
    /// </summary>
    private void DrainWorkers() {
        Stopwatch sw = Stopwatch.StartNew();
        int acquired = 0;
        try {
            while (acquired < MaxConcurrentHttpRequests) {
                int remainingMs = StopDrainTimeoutMs - (int)sw.ElapsedMilliseconds;
                if (remainingMs <= 0 || !_workerSlots.Wait(remainingMs)) {
                    Logger.Instance.Log4.Warn(
                        $"AgentServer: {MaxConcurrentHttpRequests - acquired} HTTP worker(s) still in flight " +
                        $"{StopDrainTimeoutMs}ms after Stop; abandoning the wait.");
                    break;
                }
                acquired++;
            }
        }
        finally {
            if (acquired > 0) {
                _workerSlots.Release(acquired);
            }
        }
    }

    private void AcceptLoop(HttpListener listener) {
        while (listener.IsListening) {
            HttpListenerContext context;
            try {
                context = listener.GetContext();
            }
            catch (HttpListenerException) {
                break; // listener stopped
            }
            catch (InvalidOperationException) {
                break;
            }
            // Serve each request on a worker so a slow call (a long wait-for, a deep query) doesn't block
            // accepting or serving other requests (#113). Each context owns its own response stream, so no
            // cross-request correlation is needed. The fan-out is bounded (#151): past
            // MaxConcurrentHttpRequests in-flight requests the server answers 503 instead of spawning
            // unbounded tasks. (WriteHttp here is safe on the accept thread: http.sys buffers the small
            // response in the kernel, so a slow client can't stall the accept loop.)
            if (!_workerSlots.Wait(0)) {
                try {
                    WriteHttp(context, 503, new JsonObject { ["error"] = $"Server busy: more than {MaxConcurrentHttpRequests} requests in flight" });
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Error($"AgentServer: failed to write HTTP 503: {e.Message}");
                }
                continue;
            }
            _ = Task.Run(() => {
                try {
                    HandleHttp(context);
                }
                finally {
                    _workerSlots.Release();
                }
            });
        }
    }

    private void HandleHttp(HttpListenerContext context) {
        try {
            // SECURITY (#143): a localhost HTTP service is reachable by any web page the operator visits
            // (browser CSRF) and by a rebinding attacker (DNS rebinding). Validate Host/Origin/token and
            // the path BEFORE reading the body or dispatching, so a failed request never actuates a tool.
            HttpListenerRequest request = context.Request;
            AppSettings? settings = _settings();
            HttpGateDecision decision = GateHttpRequest(
                request.HttpMethod,
                request.Url?.AbsolutePath,
                request.UserHostName,
                request.Headers["Origin"],
                request.Headers["Authorization"],
                settings?.McpHttpPort ?? 0,
                settings?.McpAuthToken);
            if (decision != HttpGateDecision.Allow) {
                RejectHttp(context, decision, request);
                return;
            }
            // #151: refuse an oversized body from the header alone; never buffer it. ContentLength64
            // is -1 for chunked transfer, which passes here and is caught by the bounded read below.
            if (context.Request.ContentLength64 > MaxHttpBodyBytes) {
                WriteHttp(context, 413, new JsonObject { ["error"] = $"Request body exceeds {MaxHttpBodyBytes} bytes" });
                return;
            }
            // Bounded read, so a chunked (or lying) client without an honest Content-Length still
            // cannot make the server buffer more than the cap.
            if (!TryReadBoundedBody(context.Request.InputStream, context.Request.ContentEncoding, out string body)) {
                WriteHttp(context, 413, new JsonObject { ["error"] = $"Request body exceeds {MaxHttpBodyBytes} bytes" });
                return;
            }
            JsonObject response;
            try {
                JsonNode? node = JsonNode.Parse(body);
                response = node is JsonObject req
                    ? _dispatch(req) ?? new JsonObject { ["jsonrpc"] = "2.0" }
                    : JsonRpcError(-32600, "Invalid Request");
            }
            catch (JsonException e) {
                response = JsonRpcError(-32700, $"Parse error: {e.Message}");
            }
            WriteHttp(context, 200, response);
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"AgentServer: HTTP handler error: {e}");
            try {
                WriteHttp(context, 500, new JsonObject { ["error"] = e.Message });
            }
            catch (Exception inner) {
                Logger.Instance.Log4.Error($"AgentServer: failed to write HTTP error: {inner.Message}");
            }
        }
    }

    // -------------------------------------------------------------------------------------------
    // Pure request gate + bind validation (unit-tested without a socket)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Validates <paramref name="address"/> as a loopback bind address AND canonicalizes it into the
    /// exact host string <see cref="Start"/> puts in the <see cref="HttpListener"/> prefix (#152).
    /// Returns true only for the literal hostname <c>localhost</c> or a literal IP that
    /// <see cref="IPAddress.IsLoopback"/> confirms is loopback; on success <paramref name="prefixHost"/>
    /// is the CANONICAL form; <c>localhost</c>, a dotted IPv4 literal (<c>127.0.0.1</c>), or a bracketed
    /// IPv6 literal (<c>[::1]</c>).
    /// <para>
    /// Canonicalizing is load-bearing, not cosmetic. <see cref="IPAddress.TryParse"/> also blesses
    /// obfuscated loopback spellings; <c>0x7f.0.0.1</c>, <c>127.1</c>, <c>2130706433</c>,
    /// <c>127.00.00.01</c>, <c>::ffff:127.0.0.1</c>; as loopback, but <c>http.sys</c> parses those RAW
    /// strings differently: some register as hostname/wildcard bindings that, under an elevated MCEC,
    /// bind non-loopback, so a LAN attacker with a matching <c>Host</c> header would reach the
    /// unauthenticated (#143) endpoint. Building the prefix from <c>ip.ToString()</c> instead collapses
    /// every accepted form to a literal <c>http.sys</c> binds to loopback (an IPv4-mapped IPv6 loopback
    /// is folded to its IPv4 literal). Everything else is rejected: the HttpListener wildcards <c>+</c>
    /// and <c>*</c>, the all-interfaces addresses <c>0.0.0.0</c> and <c>::</c>, any non-loopback IP, and
    /// any hostname other than <c>localhost</c>. Hostnames are deliberately NOT resolved via DNS; a
    /// name could resolve to a non-loopback interface, so only the one literal name is trusted.
    /// </para>
    /// </summary>
    internal static bool TryGetLoopbackPrefixHost(string? address, out string prefixHost) {
        prefixHost = string.Empty;
        if (string.IsNullOrWhiteSpace(address)) {
            return false;
        }
        string candidate = address.Trim();
        if (string.Equals(candidate, "localhost", StringComparison.OrdinalIgnoreCase)) {
            prefixHost = "localhost";
            return true;
        }
        // An IPv6 literal may be written bracketed ([::1]), as it appears inside a URL/prefix.
        if (candidate.Length >= 2 && candidate[0] == '[' && candidate[^1] == ']') {
            candidate = candidate[1..^1];
        }
        if (!IPAddress.TryParse(candidate, out IPAddress? ip) || !IPAddress.IsLoopback(ip)) {
            return false;
        }
        // Fold an IPv4-mapped IPv6 loopback (::ffff:127.0.0.1) to its IPv4 literal so the prefix
        // http.sys sees is an unambiguous 127.x.y.z, not a form it may treat as a hostname registration.
        if (ip.IsIPv4MappedToIPv6) {
            ip = ip.MapToIPv4();
        }
        prefixHost = ip.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString();
        return true;
    }

    /// <summary>
    /// Whether <paramref name="address"/> is a bind address <see cref="Start"/> will accept (#152):
    /// the thin predicate over <see cref="TryGetLoopbackPrefixHost"/>. See that method for the accepted
    /// set and why an accepted address is canonicalized before it ever reaches <see cref="HttpListener"/>.
    /// </summary>
    internal static bool IsLoopbackBindAddress(string? address) => TryGetLoopbackPrefixHost(address, out _);

    /// <summary>
    /// Validates an inbound MCP/HTTP request against the localhost front-door policy (#143):
    /// POST only, path exactly <c>/mcp</c>, a loopback <c>Host</c> (defeats DNS rebinding), an
    /// absent-or-loopback <c>Origin</c> (defeats browser CSRF), and; when a token is configured;
    /// a matching <c>Authorization: Bearer</c> header. Pure so it can be unit tested without a socket.
    /// </summary>
    internal static HttpGateDecision GateHttpRequest(
            string httpMethod,
            string? absolutePath,
            string? hostHeader,
            string? originHeader,
            string? authorizationHeader,
            int port,
            string? requiredToken) {
        if (!string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase)) {
            return HttpGateDecision.RejectMethod;
        }
        if (!string.Equals(absolutePath, "/mcp", StringComparison.Ordinal)) {
            return HttpGateDecision.RejectPath;
        }
        if (!IsLoopbackAuthority(hostHeader, port)) {
            return HttpGateDecision.RejectHost;
        }
        if (!IsAllowedOrigin(originHeader)) {
            return HttpGateDecision.RejectOrigin;
        }
        if (!string.IsNullOrEmpty(requiredToken) && !BearerTokenMatches(authorizationHeader, requiredToken)) {
            return HttpGateDecision.RejectAuth;
        }
        return HttpGateDecision.Allow;
    }

    /// <summary>True if <paramref name="hostHeader"/> names a loopback host and (if a port is present) that port.</summary>
    private static bool IsLoopbackAuthority(string? hostHeader, int port) {
        if (string.IsNullOrWhiteSpace(hostHeader)) {
            return false;
        }
        string host = hostHeader.Trim();
        string hostName;
        string? portPart = null;
        if (host.StartsWith('[')) {
            // IPv6 literal: [::1] or [::1]:port
            int end = host.IndexOf(']');
            if (end < 0) {
                return false;
            }
            hostName = host.Substring(1, end - 1);
            if (end + 1 < host.Length && host[end + 1] == ':') {
                portPart = host[(end + 2)..];
            }
        }
        else {
            int colon = host.IndexOf(':');
            if (colon >= 0) {
                hostName = host[..colon];
                portPart = host[(colon + 1)..];
            }
            else {
                hostName = host;
            }
        }
        if (portPart is not null && (!int.TryParse(portPart, out int p) || p != port)) {
            return false;
        }
        return IsLoopbackName(hostName);
    }

    /// <summary>True if the Origin header is absent (typical non-browser MCP client) or names a loopback host.</summary>
    private static bool IsAllowedOrigin(string? originHeader) {
        if (string.IsNullOrEmpty(originHeader)) {
            return true;
        }
        // A literal "null" origin (sandboxed iframe / file://) or any un-parseable value is not loopback.
        return Uri.TryCreate(originHeader, UriKind.Absolute, out Uri? uri) && IsLoopbackName(uri.Host);
    }

    private static bool IsLoopbackName(string hostName) =>
        string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(hostName, out IPAddress? ip) && IPAddress.IsLoopback(ip));

    /// <summary>
    /// True when binding the HTTP listener to <paramref name="bindAddress"/> would expose it off-box
    /// (anything that is not a loopback address), so a bearer token must be required (#143). An empty or
    /// unparseable bind address is treated as loopback (the default is 127.0.0.1 and the listener would
    /// fail to start on garbage anyway).
    /// </summary>
    internal static bool BindRequiresAuthToken(string? bindAddress) {
        if (string.IsNullOrWhiteSpace(bindAddress)) {
            return false;
        }
        string trimmed = bindAddress.Trim();
        if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        if (!IPAddress.TryParse(trimmed, out IPAddress? ip)) {
            return false;
        }
        return !IPAddress.IsLoopback(ip);
    }

    /// <summary>Constant-time check of an <c>Authorization: Bearer &lt;token&gt;</c> header against the expected token.</summary>
    private static bool BearerTokenMatches(string? authorizationHeader, string expectedToken) {
        if (string.IsNullOrEmpty(authorizationHeader)) {
            return false;
        }
        const string scheme = "Bearer ";
        if (!authorizationHeader.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        string provided = authorizationHeader[scheme.Length..].Trim();
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(expectedToken);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void RejectHttp(HttpListenerContext context, HttpGateDecision decision, HttpListenerRequest request) {
        (int status, string message) = decision switch {
            HttpGateDecision.RejectMethod => (405, "Only POST /mcp is supported"),
            HttpGateDecision.RejectPath => (404, "Not found: only POST /mcp is supported"),
            HttpGateDecision.RejectHost => (403, "Forbidden: Host must be a loopback authority"),
            HttpGateDecision.RejectOrigin => (403, "Forbidden: cross-origin requests are not allowed"),
            HttpGateDecision.RejectAuth => (401, "Unauthorized: a valid bearer token is required"),
            _ => (403, "Forbidden"),
        };
        // Audit rejected drive-by/rebinding attempts so an operator can see them.
        Logger.Instance.Log4.Warn(
            $"AGENT-AUDIT: rejected HTTP request ({decision}) method={request.HttpMethod} " +
            $"path={request.Url?.AbsolutePath} host={request.UserHostName} origin={request.Headers["Origin"]} " +
            $"remote={request.RemoteEndPoint}");
        WriteHttp(context, status, new JsonObject { ["error"] = message });
    }

    /// <summary>
    /// Reads <paramref name="input"/> to its end into <paramref name="body"/>, refusing to buffer more
    /// than <see cref="MaxHttpBodyBytes"/> (#151). Returns false; with <paramref name="body"/> empty
    /// and the stream abandoned; the moment the cap is crossed, so a chunked or lying client cannot
    /// bypass the Content-Length check and exhaust memory.
    /// </summary>
    internal static bool TryReadBoundedBody(Stream input, Encoding encoding, out string body) {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[64 * 1024];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0) {
            if (buffer.Length + read > MaxHttpBodyBytes) {
                body = "";
                return false;
            }
            buffer.Write(chunk, 0, read);
        }
        // Unlike the StreamReader this replaced, GetString does not strip a leading BOM; a
        // BOM-prefixed body now fails JSON parsing with a normal JSON-RPC parse error.
        body = encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        return true;
    }

    /// <summary>
    /// A transport-level (pre-dispatch) JSON-RPC error response: a body that never parsed to a request
    /// object has no <c>id</c>, so the id is always null here. Tool/method faults inside a parsed
    /// request are the dispatcher's job, not the transport's.
    /// </summary>
    private static JsonObject JsonRpcError(int code, string message) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = null,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private static void WriteHttp(HttpListenerContext context, int status, JsonObject payload) {
        byte[] bytes = Encoding.UTF8.GetBytes(payload.ToJsonString(AgentJson.Options));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }
}
