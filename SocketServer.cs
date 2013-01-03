//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MCEControl {
    /// <summary>
    /// Implements the TCP/IP server using asynchronous sockets
    /// </summary>
    sealed public class SocketServer : ServiceBase, IDisposable {
        // An ConcurrentDictionary is used to keep track of worker sockets that are designed
        // to communicate with each connected client. For thread safety.
        private readonly ConcurrentDictionary<int, Socket> _socketList = new ConcurrentDictionary<int, Socket>();

        // The following variable will keep track of the cumulative 
        // total number of clients connected at any time. Since multiple threads
        // can access this variable, modifying this variable should be done
        // in a thread safe manner
        private int _clientCount;
        public int Port { get; set; }

        #region IDisposable Members
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        // Disposable members
        private Socket _mainSocket;

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            foreach (var i in _socketList.Keys)
            {
                Socket socket;
                _socketList.TryRemove(i, out socket);
                if (socket != null)
                {
                    Debug.WriteLine("Closing Socket #" + i);
                    socket.Close();
                }
            }
            if (_mainSocket != null)
            {
                _mainSocket.Close();
                _mainSocket = null;
            }
        }

        //-----------------------------------------------------------
        // Control functions (Start, Stop, etc...)
        //-----------------------------------------------------------
        public void Start(int port) {
            try {
                Debug.WriteLine("Server Start");
                // Create the listening socket...
                _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var ipLocal = new IPEndPoint(IPAddress.Any, port);
                // Bind to local IP Address...
                _mainSocket.Bind(ipLocal);
                // Start listening...
                _mainSocket.Listen(4);
                // Create the call back for any client connections...
                _mainSocket.BeginAccept(OnClientConnect, null);

                SetStatus(ServiceStatus.Connecting);
            }
            catch (SocketException se) {
                SendNotification(ServiceNotification.Error, CurrentStatus, null, "Start: " + se.Message);
                SetStatus(ServiceStatus.Stopped);
            }
        }

        public void Stop() {
            Dispose(true);
            Debug.WriteLine("Server Stop");
            SetStatus(ServiceStatus.Stopped);
        }

        //-----------------------------------------------------------
        // Async handlers
        //-----------------------------------------------------------
        private void OnClientConnect(IAsyncResult async) {
            if (_mainSocket == null) return;
            ServerReplyContext serverReplyContext = null;
            try {
                // Here we complete/end the BeginAccept() asynchronous call
                // by calling EndAccept() - which returns the reference to
                // a new Socket object
                var workerSocket = _mainSocket.EndAccept(async);

                // Now increment the client count for this client 
                // in a thread safe manner
                Interlocked.Increment(ref _clientCount);

                // Add the workerSocket reference to the list
                _socketList.GetOrAdd(_clientCount, workerSocket);

                serverReplyContext = new ServerReplyContext(this, workerSocket, _clientCount);

                Debug.WriteLine("Opened Socket #" + _clientCount);

                // Send a welcome message to client
                SetStatus(ServiceStatus.Connected);
                SendNotification(ServiceNotification.ClientConnected, CurrentStatus, serverReplyContext);

                // TODO: Notify client # & IP address

                //string msg = "Welcome client " + _clientCount + "\n";
                //SendMsgToClient(msg, m_clientCount);

                // Let the worker Socket do the further processing for the 
                // just connected client
                BeginReceive(serverReplyContext);

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                _mainSocket.BeginAccept(OnClientConnect, null);
            }
            catch (ObjectDisposedException) {
                // Ignore this
                //SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnClientConnection: Socket has been closed: " + e.Message);
            }
            catch (SocketException se) {
                SendNotification(ServiceNotification.Error, CurrentStatus, serverReplyContext, "OnClientConnection: " + se.Message);
            }
        }

        // Start waiting for data from the client
        private void BeginReceive(ServerReplyContext serverReplyContext) {
            try {
                serverReplyContext.Socket.BeginReceive(serverReplyContext.DataBuffer, 0,
                                    serverReplyContext.DataBuffer.Length,
                                    SocketFlags.None,
                                    OnDataReceived,
                                    serverReplyContext);
            }
            catch (SocketException se) {
                SendNotification(ServiceNotification.Error, CurrentStatus, serverReplyContext, "BeginReceive: " + se.Message);
            }
        }

        private void CloseSocket(ServerReplyContext serverReplyContext) {
            // Remove the reference to the worker socket of the closed client
            // so that this object will get garbage collected
            Socket socket;
            _socketList.TryRemove(serverReplyContext.ClientNumber, out socket);
            Debug.WriteLine("Closing Socket #" + serverReplyContext.ClientNumber);
            SendNotification(ServiceNotification.ClientDisconnected, CurrentStatus, serverReplyContext);
            socket.Close();
        }

        enum TelnetVerbs
        {
            WILL = 251,
            WONT = 252,
            DO = 253,
            DONT = 254,
            IAC = 255
        }

        enum TelnetOptions
        {
            SGA = 3
        }


        // This the call back function which will be invoked when the socket
        // detects any client writing of data on the stream
        private void OnDataReceived(IAsyncResult async) {
            var clientContext = (ServerReplyContext)async.AsyncState;
            if (_mainSocket == null || !clientContext.Socket.Connected) return;
            try {
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client
                SocketError err;
                var iRx = clientContext.Socket.EndReceive(async, out err);
                if (err != SocketError.Success || iRx == 0) {
                    CloseSocket(clientContext);
                    return;
                }

                // _currentCommand contains the current command we are parsing out and 
                // _currentIndex is the index into it
                //int n = 0;
                for (int i = 0; i < iRx; i++) {
                    byte b = clientContext.DataBuffer[i];
                    switch (b)
                    {
                        case (byte)TelnetVerbs.IAC:
                            // interpret as a command
                            i++;
                            if (i < iRx) {
                                byte verb = clientContext.DataBuffer[i];
                                switch (verb)
                                {
                                    case (int)TelnetVerbs.IAC:
                                        //literal IAC = 255 escaped, so append char 255 to string
                                        clientContext.CmdBuilder.Append(verb);
                                        break;
                                    case (int)TelnetVerbs.DO:
                                    case (int)TelnetVerbs.DONT:
                                    case (int)TelnetVerbs.WILL:
                                    case (int)TelnetVerbs.WONT:
                                        // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                                        i++;
                                        byte inputoption = clientContext.DataBuffer[i];
                                        if (i < iRx) {
                                            clientContext.Socket.Send(new[]{(byte)TelnetVerbs.IAC});
                                            if (inputoption == (int) TelnetOptions.SGA)
                                                clientContext.Socket.Send(new[]{verb == (int) TelnetVerbs.DO
                                                                        ? (byte) TelnetVerbs.WILL
                                                                        : (byte) TelnetVerbs.DO});
                                            else
                                                clientContext.Socket.Send(new[]{verb == (int) TelnetVerbs.DO
                                                                        ? (byte) TelnetVerbs.WONT
                                                                        : (byte) TelnetVerbs.DONT});
                                            clientContext.Socket.Send(new[]{inputoption});
                                        }
                                        break;
                                }
                            }
                            break;

                        case (byte)'\r':
                        case (byte)'\n':
                        case (byte)'\0':
                            // Skip any delimiter chars that might have been left from earlier input
                            if (clientContext.CmdBuilder.Length > 0)
                            {
                                SendNotification(ServiceNotification.ReceivedData, CurrentStatus, clientContext, clientContext.CmdBuilder.ToString());
                                // Reset n to start new command
                                clientContext.CmdBuilder.Clear();
                            }
                            break;

                        default:
                            clientContext.CmdBuilder.Append((char)b);
                            break;
                    }
                }

                // Continue the waiting for data on the Socket
                BeginReceive(clientContext);
            }
            //catch (ObjectDisposedException) {
                //SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnDataReceived: Socket has been closed: " + e.Message);
            //}
            catch (SocketException se) {
                if (se.ErrorCode == 10054) // Error code for Connection reset by peer
                {
                    CloseSocket(clientContext);
                }
                else {
                    SendNotification(ServiceNotification.Error, CurrentStatus, clientContext, "OnDataReceived: " + se.Message);
                }
            }
        }

        public void SendAwakeCommand(String cmd, String host, int port) {
            if (String.IsNullOrEmpty(host)) {
                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null, "No wakeup host specified.");
                return;
            }
            if (port == 0) {
                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null, "Invalid port.");
                return;
            }
            try {
                // Try to resolve the remote host name or address
                var resolvedHost = Dns.GetHostEntry(host);
                var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try {
                    // Create the endpoint that describes the destination
                    var destination = new IPEndPoint(resolvedHost.AddressList[0], port);

                    SendNotification(ServiceNotification.Wakeup, CurrentStatus, null, 
                                     String.Format("Attempting connection to: {0}", destination));
                    clientSocket.Connect(destination);
                }
                catch (SocketException err) {
                    // Connect failed so close the socket and try the next address
                    clientSocket.Close();
                    clientSocket = null;
                    SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                     "Error connecting.\r\n" + String.Format("   Error: {0}", err.Message));
                }
                // Make sure we have a valid socket before trying to use it
                if ((clientSocket != null)) {
                    try {
                        clientSocket.Send(Encoding.ASCII.GetBytes(cmd + "\r\n"));

                        SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                         "Sent request " + cmd + " to wakeup host.");

                        // For TCP, shutdown sending on our side since the client won't send any more data
                        clientSocket.Shutdown(SocketShutdown.Send);
                    }
                    catch (SocketException err) {
                        SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                         "Error occured while sending or receiving data.\r\n" +
                                         String.Format("   Error: {0}", err.Message));
                    }
                }
                else {
                    SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                     "Unable to establish connection to server!");
                }
            }
            catch (SocketException err) {
                SendNotification(ServiceNotification.Wakeup, CurrentStatus, null,
                                 String.Format("Socket error occured: {0}", err.Message));
            }
        }

        #region Nested type: ServerReplyContext

        public class ServerReplyContext : Reply {
            public StringBuilder CmdBuilder { get; set; }
            public Socket Socket { get; set; }
            public int ClientNumber { get; set; }
            // Buffer to store the data sent by the client
            public byte[] DataBuffer = new byte[1024];

            private readonly SocketServer _server;

            // Constructor which takes a Socket and a client number
            public ServerReplyContext(SocketServer server, Socket socket, int clientNumber) {
                CmdBuilder = new StringBuilder();
                _server = server;
                Socket = socket;
                ClientNumber = clientNumber;
            }

            protected string Command {
                get { return CmdBuilder.ToString(); }
                set { } 
            }

            public override void Write(String text)
            {
                if (_server == null 
                    || _server.CurrentStatus != ServiceStatus.Connected 
                    || Socket == null 
                    || !Socket.Connected)
                    return;
                
                if (Socket.Send(Encoding.UTF8.GetBytes(text)) > 0) {
                    _server.SendNotification(ServiceNotification.Write, _server.CurrentStatus, this, text.Trim());
                }
                else {
                    _server.SendNotification(ServiceNotification.WriteFailed, _server.CurrentStatus, this, text);                    
                }
            }
        }

        #endregion
    }
}