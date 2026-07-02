//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
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
sealed public class SocketServer : ServiceBase, IDisposable {
    // An ConcurrentDictionary is used to keep track of worker sockets that are designed
    // to communicate with each connected client. For thread safety.
    private readonly ConcurrentDictionary<int, Socket> _clientList = new();

    // Number of currently connected clients (incremented on connect,
    // decremented on disconnect). Since multiple threads can access this
    // variable, modifying it should be done in a thread safe manner.
    private int _clientCount;

    // Monotonically increasing id used as the _clientList key and ClientNumber.
    // Never decremented — unlike _clientCount, which drops on disconnect — so
    // keys stay unique for the lifetime of the server (#147).
    private int _nextClientId;

    // Test seams (InternalsVisibleTo MCEControl.xUnit)
    internal int ConnectedClientCount => _clientCount;
    internal IReadOnlyDictionary<int, Socket> TrackedClients => _clientList;

    #region IDisposable Members
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion

    // Disposable members
    private Socket? _mainSocket;

    private void Dispose(bool disposing) {
        Log4.Debug("SocketServer disposing...");
        if (!disposing) {
            return;
        }

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
    public void Start(int port) {
        try {
            // Create the listening socket...
            _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, port);
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
            SendNotification(ServiceNotification.Error, CurrentStatus, null, $"{se.Message}, {se.HResult:X} ({se.SocketErrorCode})");
            SetStatus(ServiceStatus.Stopped);
        }
    }

    public void Stop() {
        Log4.Debug("SocketServer Stop");
        Dispose(true);
        SetStatus(ServiceStatus.Stopped);
    }

    //-----------------------------------------------------------
    // Async handlers
    //-----------------------------------------------------------
    private void OnClientConnect(IAsyncResult async) {
        Log4.Debug("SocketServer OnClientConnect");

        if (_mainSocket == null) {
            return;
        }

        ServerReplyContext? serverReplyContext = null;
        try {
            // Here we complete/end the BeginAccept() asynchronous call
            // by calling EndAccept() - which returns the reference to
            // a new Socket object
            Socket workerSocket = _mainSocket.EndAccept(async);

            serverReplyContext = RegisterClient(workerSocket);

            Log4.Debug("Opened Socket #" + serverReplyContext.ClientNumber);

            SetStatus(ServiceStatus.Connected);
            SendNotification(ServiceNotification.ClientConnected, CurrentStatus, serverReplyContext);

            // Send a welcome message to client
            // TODO: Notify client # & IP address
            //string msg = "Welcome client " + _clientCount + "\n";
            //SendMsgToClient(msg, m_clientCount);

            // Let the worker Socket do the further processing for the 
            // just connected client
            BeginReceive(serverReplyContext);
        }
        catch (SocketException se) {
            SendNotification(ServiceNotification.Error, CurrentStatus, serverReplyContext, $"OnClientConnect: {se.Message}, {se.HResult:X} ({se.SocketErrorCode})");
            // See http://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
            //if (se.SocketErrorCode == SocketError.ConnectionReset) // WSAECONNRESET (10054)
            {
                // Forcibly closed
                CloseSocket(serverReplyContext!);
            }
        }
        catch (Exception e) {
            SendNotification(ServiceNotification.Error, CurrentStatus, serverReplyContext, $"OnClientConnect: {e.Message}");
            CloseSocket(serverReplyContext!);
        }

        // Since the main Socket is now free, it can go back and wait for
        // other clients who are attempting to connect
        _mainSocket?.BeginAccept(OnClientConnect, null);
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
            SendNotification(ServiceNotification.Error, CurrentStatus, serverReplyContext, $"BeginReceive: {se.Message}, {se.HResult:X} ({se.SocketErrorCode})");
            CloseSocket(serverReplyContext);
        }
    }

    // Internal so tests can exercise the client-tracking logic (InternalsVisibleTo).
    internal void CloseSocket(ServerReplyContext serverReplyContext) {
        Log4.Debug("SocketServer CloseSocket");
        if (serverReplyContext == null) {
            return;
        }

        // Remove the reference to the worker socket of the closed client
        // so that this object will get garbage collected
        _ = _clientList.TryRemove(serverReplyContext.ClientNumber, out Socket? socket);
        if (socket != null) {
            Log4.Debug("Closing Socket #" + serverReplyContext.ClientNumber);
            Interlocked.Decrement(ref _clientCount);
            SendNotification(ServiceNotification.ClientDisconnected, CurrentStatus, serverReplyContext);
            socket.Close();
        }
    }

    // This the call back function which will be invoked when the socket
    // detects any client writing of data on the stream
    private void OnDataReceived(IAsyncResult async) {
        ServerReplyContext clientContext = (ServerReplyContext)async.AsyncState!;
        if (_mainSocket == null || !clientContext.Socket.Connected) {
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

            // Parse the received chunk. Guard the parse loop so a malformed/crafted packet can
            // never escape as an unhandled exception on this ThreadPool callback and terminate
            // the process (issue #144 — unauthenticated remote crash). Any parse failure closes
            // the offending client instead.
            try {
                ParseReceivedData(
                    clientContext.DataBuffer,
                    iRx,
                    clientContext.CmdBuilder,
                    reply => clientContext.Socket.Send(reply),
                    command => SendNotification(ServiceNotification.ReceivedData, CurrentStatus, clientContext, command));
            }
            catch (Exception ex) {
                SendNotification(ServiceNotification.Error, CurrentStatus, clientContext, $"OnDataReceived: parse error: {ex.Message}");
                CloseSocket(clientContext);
                return;
            }

            // Continue the waiting for data on the Socket
            BeginReceive(clientContext);
        }
        catch (SocketException se) {
            if (se.SocketErrorCode == SocketError.ConnectionReset) // Error code for Connection reset by peer 
            {
                SendNotification(ServiceNotification.Error, CurrentStatus, clientContext, $"OnDataReceived: {se.Message}, {se.HResult:X} ({se.SocketErrorCode})");
                CloseSocket(clientContext);
            }
            else {
                SendNotification(ServiceNotification.Error, CurrentStatus, clientContext, $"OnDataReceived: {se.Message}, {se.HResult:X} ({se.SocketErrorCode})");
            }
        }
    }

    /// <summary>
    /// Parses a received chunk of bytes: strips inline telnet negotiation (IAC …) and emits a
    /// command via <paramref name="onCommand"/> on each CR/LF/NUL delimiter. Extracted from
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
            StringBuilder cmdBuilder,
            Action<byte[]> sendReply,
            Action<string> onCommand) {
        for (int i = 0; i < count; i++) {
            byte b = buffer[i];
            switch (b) {
                case (byte)TelnetVerbs.IAC:
                    // interpret as a telnet command; need at least a verb byte
                    i++;
                    if (i >= count) {
                        break; // truncated IAC — nothing to interpret
                    }
                    byte verb = buffer[i];
                    switch (verb) {
                        case (int)TelnetVerbs.IAC:
                            //literal IAC = 255 escaped, so append char 255 to string
                            cmdBuilder.Append(verb);
                            break;
                        case (int)TelnetVerbs.DO:
                        case (int)TelnetVerbs.DONT:
                        case (int)TelnetVerbs.WILL:
                        case (int)TelnetVerbs.WONT:
                            // need an option byte; read it ONLY after the bounds check (#144)
                            i++;
                            if (i >= count) {
                                break; // truncated option — ignore, do not read past the buffer
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

                case (byte)'\r':
                case (byte)'\n':
                case (byte)'\0':
                    // Skip any delimiter chars that might have been left from earlier input
                    if (cmdBuilder.Length > 0) {
                        onCommand(cmdBuilder.ToString());
                        cmdBuilder.Clear();
                    }
                    break;

                default:
                    cmdBuilder.Append((char)b);
                    break;
            }
        }
    }

    public void SendAwakeCommand(String cmd, String host, int port) {
        Log4.Debug("SocketServer SendAwakeCommand");
        if (String.IsNullOrEmpty(host)) {
            SendNotification(ServiceNotification.Wakeup, CurrentStatus, null, "No wakeup host specified");
            return;
        }
        if (port == 0) {
            SendNotification(ServiceNotification.Wakeup, CurrentStatus, null, "Invalid port");
            return;
        }
        try {
            // Try to resolve the remote host name or address
            IPHostEntry resolvedHost = Dns.GetHostEntry(host);
            Socket? clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try {
                // Create the endpoint that describes the destination
                IPEndPoint destination = new IPEndPoint(resolvedHost.AddressList[0], port);

                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                 $"Attempting connection to: {destination}");
                clientSocket.Connect(destination);
            }
            catch (SocketException err) {
                // Connect failed so close the socket and try the next address
                clientSocket.Close();
                clientSocket = null;
                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                 "Error connecting.\r\n" + $"   Error: {err.Message}");
            }
            // Make sure we have a valid socket before trying to use it
            if ((clientSocket != null)) {
                try {
                    _ = clientSocket.Send(Encoding.ASCII.GetBytes(cmd + "\r\n"));

                    SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                     "Sent request " + cmd + " to wakeup host.");

                    // For TCP, shutdown sending on our side since the client won't send any more data
                    clientSocket.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException err) {
                    SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                     $"Error occured while sending or receiving data.\r\n   Error: {err.Message}");
                }
                clientSocket.Dispose();
            }
            else {
                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                 "Unable to establish connection to server!");
            }
        }
        catch (SocketException err) {
            SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                             $"Socket error occured: {err.Message}");
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
            if (((ServerReplyContext)replyContext).Socket.Send(Encoding.UTF8.GetBytes(text)) > 0) {
                SendNotification(ServiceNotification.Write, CurrentStatus, replyContext, text.Trim());
            }
            else {
                SendNotification(ServiceNotification.WriteFailed, CurrentStatus, replyContext, text);
            }
        }
    }

}
