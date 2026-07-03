//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MCEControl; 
/// <summary>
/// Implements the TCP/IP server using asynchronous sockets
/// </summary>
public sealed class SocketServer : ServiceBase, IDisposable {
    // An ConcurrentDictionary is used to keep track of worker sockets that are designed
    // to communicate with each connected client. For thread safety.
    private readonly ConcurrentDictionary<int, Socket> _clientList = new();

    // Number of currently connected clients (incremented on connect,
    // decremented on disconnect). Since multiple threads can access this
    // variable, modifying it should be done in a thread safe manner.
    private int _clientCount;

    // Monotonically increasing id used as the _clientList key and ClientNumber.
    // Never decremented; unlike _clientCount, which drops on disconnect; so
    // keys stay unique for the lifetime of the server (#147).
    private int _nextClientId;

    // Test seams (InternalsVisibleTo MCEControl.xUnit)
    internal int ConnectedClientCount => _clientCount;
    internal IReadOnlyDictionary<int, Socket> TrackedClients => _clientList;
    // The port the listener actually bound (tests Start on port 0 so the OS assigns an
    // ephemeral port that a loopback client can then connect to); 0 when not listening.
    internal int ListeningPort => (_mainSocket?.LocalEndPoint as IPEndPoint)?.Port ?? 0;

    // True once Dispose() has run. Dispose is terminal: unlike Stop() (which leaves the
    // object restartable), a disposed SocketServer can never be Start()ed again (#202).
    private bool _disposed;

    #region IDisposable Members
    /// <summary>
    /// Terminal teardown. Guarded and idempotent: the first call closes the listener and every
    /// tracked client; later calls no-op. After Dispose, <see cref="Start"/> throws
    /// <see cref="ObjectDisposedException"/>. Contrast with <see cref="Stop"/>, which closes the
    /// same sockets but leaves the object restartable. Before issue #202 Stop() <i>was</i>
    /// Dispose(true) and the "disposed" object was routinely resurrected by Start(), so
    /// "stopped" and "disposed" were the same state and nothing guarded either.
    /// </summary>
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        Log4.Debug("SocketServer disposing...");
        CloseListenerAndClients();
    }
    #endregion

    // Disposable members
    private Socket? _mainSocket;

    /// <summary>
    /// Closes the listening socket and force-closes every tracked client, draining the
    /// connected tally so a later <see cref="Start"/> does not report a stale count. This is
    /// the shared teardown used by both the resettable <see cref="Stop"/> and the terminal
    /// <see cref="Dispose"/>; it mutates live state but does NOT mark the object disposed (#202).
    /// </summary>
    private void CloseListenerAndClients() {
        foreach (int i in _clientList.Keys) {
            _clientList.TryRemove(i, out Socket? socket);
            if (socket != null) {
                Log4.Debug("Closing Socket #" + i);
                // Keep the connected tally in sync with the force-closed sockets
                // so a later Start does not report a stale count.
                Interlocked.Decrement(ref _clientCount);
                socket.Close();
            }
        }
        _mainSocket?.Close();
        _mainSocket = null;
    }

    //-----------------------------------------------------------
    // Control functions (Start, Stop, etc...)
    //-----------------------------------------------------------
    /// <summary>
    /// Resolves the operator-configured <see cref="AppSettings.SocketServerBindAddress"/> string into
    /// the <see cref="IPAddress"/> the command listener binds to (issue #149). The command server turns
    /// received strings into keyboard/mouse/process actions with NO socket authentication (by design,
    /// trusted-network model), so which interface it listens on is a security control:
    /// <list type="bullet">
    ///   <item><c>"any"</c>/<c>"0.0.0.0"</c>/<c>"*"</c>/empty → <see cref="IPAddress.Any"/> (all
    ///   interfaces; the long-standing default, reachable from every host on the LAN/VPN; kept for
    ///   backward compatibility on upgrade).</item>
    ///   <item><c>"localhost"</c>/<c>"loopback"</c> → <see cref="IPAddress.Loopback"/> (single-machine
    ///   only; the recommended setting when nothing off-box needs to connect).</item>
    ///   <item>a parseable IP (<c>"127.0.0.1"</c>, <c>"::1"</c>, a specific LAN IP) → that address.</item>
    ///   <item>anything else (junk) → <see cref="IPAddress.Loopback"/>, logged loudly; a misconfigured
    ///   bind must fail closed to the safe interface, never silently expose the port on all interfaces.</item>
    /// </list>
    /// Case-insensitive. Mirrors the agent HTTP floor's loopback handling
    /// (<see cref="McpHttpTransport.BindRequiresAuthToken"/>). Internal so it can be unit-tested without
    /// opening a live listener (InternalsVisibleTo).
    /// </summary>
    internal static IPAddress ResolveBindAddress(string? bindAddress) {
        string trimmed = bindAddress?.Trim() ?? string.Empty;
        switch (trimmed.ToLowerInvariant()) {
            case "":
            case "any":
            case "0.0.0.0":
            case "*":
                return IPAddress.Any;
            case "localhost":
            case "loopback":
                return IPAddress.Loopback;
        }
        if (IPAddress.TryParse(trimmed, out IPAddress? ip)) {
            return ip;
        }
        Logger.Instance.Log4.Error(
            $"SocketServer: SocketServerBindAddress '{bindAddress}' is not a valid bind address " +
            "(expected \"0.0.0.0\"/\"any\", \"127.0.0.1\"/\"localhost\", \"::1\", or a specific local IP). " +
            "Falling back to loopback (127.0.0.1) so the unauthenticated command port is not exposed on all interfaces.");
        return IPAddress.Loopback;
    }

    /// <summary>
    /// Emits a loud startup WARNING when the command listener is bound to a non-loopback address
    /// (issue #149). The command server has NO socket authentication, so binding it to all interfaces
    /// (<c>0.0.0.0</c> / <c>::</c>) or any other non-loopback address exposes unauthenticated
    /// keyboard/mouse/process command injection to every host that can reach the port
    /// (LAN/VPN/port-forward). Unlike the MCP HTTP door; which <b>refuses</b> an exposed bind without a
    /// token (<see cref="McpHttpTransport.BindRequiresAuthToken"/>); the socket server keeps its long-standing
    /// all-interfaces default for backward compatibility, so the operator is loudly warned rather than
    /// blocked. <see cref="IPAddress.Any"/> and <see cref="IPAddress.IPv6Any"/> are non-loopback.
    /// Internal + static so it can be unit-tested against a resolved address without opening a listener.
    /// </summary>
    internal static void WarnIfBindAddressExposed(IPAddress bindTo) {
        if (IPAddress.IsLoopback(bindTo)) {
            return;
        }
        Logger.Instance.Log4.Warn(
            $"SocketServer: the command server is bound to {bindTo} (a non-loopback address) and has NO " +
            "authentication; it accepts keyboard/mouse/process commands from ANY host that can reach the " +
            "port (LAN/VPN/port-forward). For single-machine use set SocketServerBindAddress=127.0.0.1 to " +
            "restrict it to this machine.");
    }

    public void Start(int port, string? bindAddress = null) {
        // Stop() is resettable; Dispose() is terminal (#202). Never resurrect a disposed server.
        ObjectDisposedException.ThrowIf(_disposed, this);
        try {
            // Create the listening socket on the address family of the resolved bind address (#149),
            // so an IPv6 bind (e.g. "::1") gets an IPv6 socket rather than throwing on an IPv4 one.
            IPAddress bindTo = ResolveBindAddress(bindAddress);
            // #149: the command port is unauthenticated. If the resolved bind is non-loopback, warn
            // loudly at startup (the MCP door refuses this; we warn to stay backward compatible).
            WarnIfBindAddressExposed(bindTo);
            _mainSocket = new Socket(bindTo.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipLocal = new IPEndPoint(bindTo, port);
            // Bind to local IP Address...
            Log4.Debug("SocketServer - Binding to IP address: " + ipLocal.Address + ":" + ipLocal.Port);
            _mainSocket.Bind(ipLocal);
            // Start listening...
            Log4.Debug("_mainSocket.Listen");
            _mainSocket.Listen(4);
            // Create the call back for any client connections...
            SetStatus(ServiceStatus.Started, $"{ipLocal.Address}:{port}");
            SetStatus(ServiceStatus.Waiting);
            _mainSocket.BeginAccept(OnClientConnect, null);
        }
        catch (SocketException se) {
            Error(ServiceError.FromSocketException(se.Message, se));
            SetStatus(ServiceStatus.Stopped);
        }
    }

    /// <summary>
    /// Stops the server: closes the listener and force-closes every tracked client. Unlike
    /// <see cref="Dispose"/> this is resettable; a stopped server may be <see cref="Start"/>ed
    /// again (the operator toggles the service on/off in Settings). Before issue #202 this
    /// called Dispose(true), conflating "stopped" with "disposed".
    /// </summary>
    public void Stop() {
        Log4.Debug("SocketServer Stop");
        CloseListenerAndClients();
        SetStatus(ServiceStatus.Stopped);
    }

    //-----------------------------------------------------------
    // Async handlers
    //-----------------------------------------------------------
    private void OnClientConnect(IAsyncResult async) {
        Log4.Debug("SocketServer OnClientConnect");

        // Capture the listener locally: Stop()/Dispose() null the field from another thread,
        // and dereferencing the field again below would race a NullReferenceException into
        // the generic catch (a spurious Error notification on every stop; #202).
        Socket? listener = _mainSocket;
        if (listener == null) {
            return;
        }

        ServerReplyContext? serverReplyContext = null;
        try {
            // Here we complete/end the BeginAccept() asynchronous call
            // by calling EndAccept() - which returns the reference to
            // a new Socket object
            Socket workerSocket = listener.EndAccept(async);

            serverReplyContext = RegisterClient(workerSocket);

            Log4.Debug("Opened Socket #" + serverReplyContext.ClientNumber);

            SetStatus(ServiceStatus.Connected);
            // Connection-level diagnostics are logged here at the emission site (#211); the old
            // ClientConnected notification existed only so MainWindow could produce this line.
            Log4.Info($"SocketServer: Client #{serverReplyContext.ClientNumber} at {DescribeEndPoint(workerSocket)} connected");

            // Send a welcome message to client
            // TODO: Notify client # & IP address
            //string msg = "Welcome client " + _clientCount + "\n";
            //SendMsgToClient(msg, m_clientCount);

            // Let the worker Socket do the further processing for the 
            // just connected client
            BeginReceive(serverReplyContext);
        }
        catch (ObjectDisposedException) {
            // Expected on Stop()/Dispose(): closing the listener completes the pending
            // BeginAccept, and EndAccept then throws ObjectDisposedException. This is
            // normal shutdown; not an error. Before issue #202 the generic catch below
            // turned every stop into a spurious Error notification (and a CloseSocket
            // call on a null context). Do not re-arm the accept; the listener is gone.
            Log4.Debug("SocketServer OnClientConnect: listener closed during shutdown");
            return;
        }
        catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted) {
            // The accept was aborted because the listener is shutting down; same benign
            // shutdown shape as the ObjectDisposedException above (#202).
            Log4.Debug("SocketServer OnClientConnect: accept aborted during shutdown");
            return;
        }
        catch (SocketException se) {
            Error(ServiceError.FromSocketException($"OnClientConnect: {se.Message}", se));
            // See http://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
            //if (se.SocketErrorCode == SocketError.ConnectionReset) // WSAECONNRESET (10054)
            if (serverReplyContext != null) {
                // Forcibly closed
                CloseSocket(serverReplyContext);
            }
        }
        catch (Exception e) {
            Error($"OnClientConnect: {e.Message}");
            if (serverReplyContext != null) {
                CloseSocket(serverReplyContext);
            }
        }

        // Since the main Socket is now free, it can go back and wait for
        // other clients who are attempting to connect
        try {
            _mainSocket?.BeginAccept(OnClientConnect, null);
        }
        catch (ObjectDisposedException) {
            // Raced with Stop()/Dispose() between the accept completing and the re-arm;
            // benign shutdown, nothing to re-arm (#202).
            Log4.Debug("SocketServer OnClientConnect: listener closed before accept re-arm");
        }
    }

    // Tracks a newly connected client. Internal so tests can exercise the
    // client-tracking logic without a live listener (InternalsVisibleTo).
    internal ServerReplyContext RegisterClient(Socket workerSocket) {
        // Now increment the client count for this client
        // in a thread safe manner
        Interlocked.Increment(ref _clientCount);

        // Allocate a unique, never-reused id for this client. Using _clientCount
        // here would repeat keys under connect/disconnect churn, leaking the new
        // socket and later closing the wrong client (#147).
        int clientId = Interlocked.Increment(ref _nextClientId);

        // Add the workerSocket reference to the list. Ids are never reused, so
        // TryAdd must always succeed; fail loudly if that invariant regresses
        // (a silent GetOrAdd is exactly how #147 hid).
        bool added = _clientList.TryAdd(clientId, workerSocket);
        System.Diagnostics.Debug.Assert(added, $"SocketServer client id {clientId} was already in use");
        if (!added) {
            Log4.Error($"SocketServer RegisterClient: duplicate client id {clientId}; socket will not be tracked");
        }

        return new ServerReplyContext(this, workerSocket, clientId);
    }

    // Start waiting for data from the client
    private void BeginReceive(ServerReplyContext serverReplyContext) {
        Log4.Debug("SocketServer BeginReceive");
        try {
            _ = serverReplyContext.Socket.BeginReceive(serverReplyContext.DataBuffer, 0,
                                serverReplyContext.DataBuffer.Length,
                                SocketFlags.None,
                                OnDataReceived,
                                serverReplyContext);
        }
        catch (SocketException se) {
            Error(ServiceError.FromSocketException($"BeginReceive: {se.Message}", se));
            CloseSocket(serverReplyContext);
        }
    }

    /// <summary>
    /// Formats a socket's remote endpoint for diagnostics without ever throwing: a socket that
    /// was never connected, already reset, or already closed makes <see cref="Socket.RemoteEndPoint"/>
    /// throw, and these log sites run on exactly those teardown paths.
    /// </summary>
    private static string DescribeEndPoint(Socket? socket) {
        try {
            return socket?.RemoteEndPoint?.ToString() ?? "n/a";
        }
        catch (SocketException) {
            return "n/a";
        }
        catch (ObjectDisposedException) {
            return "n/a";
        }
    }

    // Internal so tests can exercise the client-tracking logic (InternalsVisibleTo).
    internal void CloseSocket(ServerReplyContext serverReplyContext) {
        Log4.Debug("SocketServer CloseSocket");

        // Remove the reference to the worker socket of the closed client
        // so that this object will get garbage collected
        _ = _clientList.TryRemove(serverReplyContext.ClientNumber, out Socket? socket);
        if (socket != null) {
            Log4.Debug("Closing Socket #" + serverReplyContext.ClientNumber);
            Interlocked.Decrement(ref _clientCount);
            Log4.Info($"SocketServer: Client #{serverReplyContext.ClientNumber} at {DescribeEndPoint(socket)} has disconnected");
            socket.Close();
        }
    }

    // This the call back function which will be invoked when the socket
    // detects any client writing of data on the stream
    private void OnDataReceived(IAsyncResult async) {
        ServerReplyContext clientContext = (ServerReplyContext)async.AsyncState!;
        if (_mainSocket == null) {
            // The server is shutting down: Dispose() has already force-closed and drained
            // every tracked client, so there is nothing left to do here (and nothing to leak).
            return;
        }
        if (!EnsureClientConnectedOrClose(clientContext)) {
            // Socket is dead but still tracked; closed above; stop receiving (issue #150).
            return;
        }

        try {
            // Complete the BeginReceive() asynchronous call by EndReceive() method
            // which will return the number of characters written to the stream
            // by the client
            int iRx = clientContext.Socket.EndReceive(async, out SocketError err);
            if (err != SocketError.Success || iRx == 0) {
                CloseSocket(clientContext);
                return;
            }

            // Parse the received chunk; if it closed the client (parse failure or
            // command-length overflow), stop receiving.
            if (!ProcessReceivedData(clientContext, iRx)) {
                return;
            }

            // Continue the waiting for data on the Socket
            BeginReceive(clientContext);
        }
        catch (SocketException se) {
            HandleReceiveError(clientContext, se);
        }
    }

    /// <summary>
    /// Precondition for the receive callback: a tracked client whose socket has silently
    /// dropped to a not-<see cref="Socket.Connected"/> state can never be received from again,
    /// so it must be closed rather than left dangling. Before issue #150 this path returned
    /// without <see cref="CloseSocket"/>, permanently leaking the socket handle, the
    /// <c>_clientList</c> entry, and a connected-count slot. Idempotent: <see cref="CloseSocket"/>
    /// no-ops (and does not double-decrement; see #147) if the client was already removed.
    /// Internal so tests can drive it without a live listener (InternalsVisibleTo).
    /// </summary>
    /// <returns>true if the client is still connected and receiving may proceed; false if the
    /// client was closed and the caller must stop.</returns>
    internal bool EnsureClientConnectedOrClose(ServerReplyContext clientContext) {
        if (clientContext.Socket.Connected) {
            return true;
        }
        CloseSocket(clientContext);
        return false;
    }

    /// <summary>
    /// Handles a <see cref="SocketException"/> raised on the receive path by
    /// <see cref="Socket.EndReceive(IAsyncResult, out SocketError)"/>. ANY such error is terminal for the
    /// connection (we will not re-arm <see cref="BeginReceive"/>), so the client MUST be closed
    /// exactly once. Before issue #150 only <see cref="SocketError.ConnectionReset"/> closed;
    /// every other error code merely logged, leaving a dead-but-tracked connection that leaked
    /// its handle, <c>_clientList</c> entry, and connected-count slot (resource exhaustion under
    /// repeated non-reset errors on the unauthenticated command port). Idempotent via
    /// <see cref="CloseSocket"/> (no double-decrement; see #147). Internal so tests can drive it
    /// without a live listener (InternalsVisibleTo).
    /// </summary>
    internal void HandleReceiveError(ServerReplyContext clientContext, SocketException se) {
        Error(ServiceError.FromSocketException($"OnDataReceived: {se.Message}", se));
        CloseSocket(clientContext);
    }

    /// <summary>
    /// Parses the chunk currently in <paramref name="clientContext"/>'s DataBuffer. Guards the
    /// parse loop so a malformed/crafted packet can never escape as an unhandled exception on a
    /// ThreadPool callback and terminate the process (issue #144; unauthenticated remote crash):
    /// any parse failure closes the offending client. A client that streams more than
    /// <see cref="CommandAccumulator.MaxCommandLength"/> chars without a delimiter (issue #148;
    /// memory-exhaustion DoS) gets the accumulator's single overflow policy (#212): the partial
    /// command is dropped, an Error is notified once, and input is discarded until the next
    /// delimiter; the connection stays open and memory stays bounded. (Pre-#212 this path
    /// closed the connection, a divergent second copy of the overflow policy.)
    /// Internal so tests can drive the receive path without a live listener (InternalsVisibleTo).
    /// </summary>
    /// <returns>false if the client was closed and receiving must stop.</returns>
    internal bool ProcessReceivedData(ServerReplyContext clientContext, int count) {
        try {
            ParseReceivedData(
                clientContext.DataBuffer,
                count,
                clientContext.Accumulator,
                reply => clientContext.Socket.Send(reply),
                command => {
                    Log4.Info($"SocketServer: Received from Client #{clientContext.ClientNumber} at {DescribeEndPoint(clientContext.Socket)}: {command}");
                    OnCommandReceived(clientContext, command);
                },
                () => Error($"OnDataReceived: command exceeded maximum length ({CommandAccumulator.MaxCommandLength} chars); discarding input until next delimiter"));
        }
        catch (Exception ex) {
            Error($"OnDataReceived: parse error: {ex.Message}");
            CloseSocket(clientContext);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Parses a received chunk of bytes: strips inline telnet negotiation (IAC …); a thin
    /// byte-level pre-filter; and feeds everything else to <paramref name="accumulator"/>,
    /// which owns the CR/LF/NUL delimiter and #148 max-length logic (consolidated there by
    /// #212; this method previously re-implemented both with divergent overflow semantics).
    /// A command is emitted via <paramref name="onCommand"/> on each delimiter; on overflow
    /// the accumulator drops the partial command, invokes <paramref name="onOverflow"/> once,
    /// and discards input until the next delimiter. Extracted from
    /// <see cref="OnDataReceived"/> so it can be unit tested and hardened against malformed input.
    /// </summary>
    /// <remarks>
    /// Issue #144: the telnet option byte was read <b>before</b> its bounds check, so a crafted
    /// packet whose last two bytes are IAC DO (option byte at <c>count</c>) caused an
    /// out-of-bounds read → unhandled exception → process crash. Every buffer access here is now
    /// bounds-checked first: a truncated IAC/verb/option sequence is silently ignored.
    /// </remarks>
    internal static void ParseReceivedData(
            byte[] buffer,
            int count,
            CommandAccumulator accumulator,
            Action<byte[]> sendReply,
            Action<string> onCommand,
            Action? onOverflow = null) {
        for (int i = 0; i < count; i++) {
            byte b = buffer[i];
            switch (b) {
                case (byte)TelnetVerbs.IAC:
                    // interpret as a telnet command; need at least a verb byte
                    i++;
                    if (i >= count) {
                        break; // truncated IAC; nothing to interpret
                    }
                    byte verb = buffer[i];
                    switch (verb) {
                        case (int)TelnetVerbs.IAC:
                            // literal IAC = 255 escaped: exactly one (char)255 goes into the
                            // command; the (char) cast matters: Append(byte) would format the
                            // NUMBER as the 3-char string "255" (#148 review follow-up). The
                            // accumulator enforces the #148 cap at this append site too.
                            _ = accumulator.ProcessChar((char)verb, onOverflow);
                            break;
                        case (int)TelnetVerbs.DO:
                        case (int)TelnetVerbs.DONT:
                        case (int)TelnetVerbs.WILL:
                        case (int)TelnetVerbs.WONT:
                            // need an option byte; read it ONLY after the bounds check (#144)
                            i++;
                            if (i >= count) {
                                break; // truncated option; ignore, do not read past the buffer
                            }
                            byte inputoption = buffer[i];
                            // reply to all commands with "WONT", unless it is SGA (suppress go ahead)
                            sendReply([(byte)TelnetVerbs.IAC]);
                            if (inputoption == (int)TelnetOptions.SGA) {
                                sendReply([verb == (int)TelnetVerbs.DO
                                            ? (byte)TelnetVerbs.WILL
                                            : (byte)TelnetVerbs.DO]);
                            }
                            else {
                                sendReply([verb == (int)TelnetVerbs.DO
                                            ? (byte)TelnetVerbs.WONT
                                            : (byte)TelnetVerbs.DONT]);
                            }

                            sendReply([inputoption]);
                            break;
                    }
                    break;

                default:
                    // Delimiters (CR/LF/NUL), the #148 cap, and overflow recovery all
                    // live in CommandAccumulator now (#212).
                    string? cmd = accumulator.ProcessChar((char)b, onOverflow);
                    if (cmd != null) {
                        onCommand(cmd);
                    }
                    break;
            }
        }
    }

    // Wakeup progress is a SocketServer-only diagnostic. The old ServiceNotification.Wakeup
    // existed solely so MainWindow could log these lines; with the typed events (#211) the
    // smaller change is to log them here at the emission site instead of modeling an event.
    private void LogWakeup(string msg) {
        Log4.Info($"SocketServer: Wakeup: {msg}");
    }

    public void SendAwakeCommand(String cmd, String host, int port) {
        Log4.Debug("SocketServer SendAwakeCommand");
        if (String.IsNullOrEmpty(host)) {
            LogWakeup("No wakeup host specified");
            return;
        }
        if (port == 0) {
            LogWakeup("Invalid port");
            return;
        }
        try {
            // Try to resolve the remote host name or address
            IPHostEntry resolvedHost = Dns.GetHostEntry(host);
            Socket? clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try {
                // Create the endpoint that describes the destination
                IPEndPoint destination = new IPEndPoint(resolvedHost.AddressList[0], port);

                LogWakeup($"Attempting connection to: {destination}");
                clientSocket.Connect(destination);
            }
            catch (SocketException err) {
                // Connect failed so close the socket and try the next address
                clientSocket.Close();
                clientSocket = null;
                LogWakeup("Error connecting.\r\n" + $"   Error: {err.Message}");
            }
            // Make sure we have a valid socket before trying to use it
            if ((clientSocket != null)) {
                try {
                    _ = clientSocket.Send(Encoding.ASCII.GetBytes(cmd + "\r\n"));

                    LogWakeup("Sent request " + cmd + " to wakeup host.");

                    // For TCP, shutdown sending on our side since the client won't send any more data
                    clientSocket.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException err) {
                    LogWakeup($"Error occured while sending or receiving data.\r\n   Error: {err.Message}");
                }
                clientSocket.Dispose();
            }
            else {
                LogWakeup("Unable to establish connection to server!");
            }
        }
        catch (SocketException err) {
            LogWakeup($"Socket error occured: {err.Message}");
        }
    }

    public override void Send(string text, Reply? replyContext = null) {
        base.Send(text, replyContext);

        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        if (CurrentStatus != ServiceStatus.Connected ||
            _mainSocket == null) {
            return;
        }

        if (replyContext == null) {
            foreach (int i in _clientList.Keys) {
                if (_clientList.TryGetValue(i, out Socket? client)) {
                    Reply reply = new ServerReplyContext(this, client, i);
                    Send(text, reply);
                }
            }
        }
        else {
            SendToClient(text, (ServerReplyContext)replyContext);
        }
    }

    /// <summary>
    /// Sends <paramref name="text"/> to a single tracked client, guarding the raw
    /// <see cref="Socket.Send(byte[])"/>. A peer can vanish between the broadcast loop's
    /// tracking lookup and the send; before issue #202 the resulting exception escaped
    /// <see cref="Send"/> into whatever command handler triggered the write. A failed send is
    /// terminal for that client: it is closed and removed from tracking (complementing the
    /// receive-path fix from #150) and <see cref="ServiceBase.ErrorOccurred"/> is
    /// raised; never an unhandled throw. Internal so tests can drive it without a live
    /// listener (InternalsVisibleTo).
    /// </summary>
    internal void SendToClient(string text, ServerReplyContext replyContext) {
        // A failed write surfaces as ErrorOccurred (#211): the old Write/WriteFailed
        // notifications only existed so MainWindow could log them, and a write failure is
        // terminal for the client (it is closed and untracked below); an error by any measure.
        try {
            if (replyContext.Socket.Send(Encoding.UTF8.GetBytes(text)) > 0) {
                Log4.Info($"SocketServer: Wrote to Client #{replyContext.ClientNumber} at {DescribeEndPoint(replyContext.Socket)}: {text.Trim()}");
            }
            else {
                Error($"Write failed to Client #{replyContext.ClientNumber} at {DescribeEndPoint(replyContext.Socket)}: {text}");
            }
        }
        catch (SocketException se) {
            Error(ServiceError.FromSocketException(
                $"Write failed to Client #{replyContext.ClientNumber} at {DescribeEndPoint(replyContext.Socket)}: Send: {se.Message}", se));
            CloseSocket(replyContext);
        }
        catch (ObjectDisposedException) {
            // The client's socket was closed out from under us (e.g. the receive path
            // closed it between the tracking lookup and this send); treat as a failed
            // write and make sure the client is fully untracked.
            Error($"Write failed to Client #{replyContext.ClientNumber}: Send: client socket was already closed");
            CloseSocket(replyContext);
        }
    }

}
