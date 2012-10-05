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
    public class SocketServer : IDisposable {
        // An ConcurrentDictionary is used to keep track of worker sockets that are designed
        // to communicate with each connected client. For thread safety.

        #region Delegates

        public delegate void NotificationCallback(
            Notification notification, Status status, int client, String ipaddress, Object data);

        #endregion

        #region Notification enum

        public enum Notification {
            Initialized = 1,
            StatusChange,
            ReceivedData,
            ClientConnected,
            ClientDisconnected,
            Error,
            Wakeup
        }

        #endregion

        // Nested enum for supported states

        #region Status enum

        public enum Status {
            Listening,
            Connected,
            Stopped
        }

        #endregion

        private readonly ConcurrentDictionary<int, Socket> _socketList = new ConcurrentDictionary<int, Socket>();
        public AsyncCallback ReceiveCallback;

        // The following variable will keep track of the cumulative 
        // total number of clients connected at any time. Since multiple threads
        // can access this variable, modifying this variable should be done
        // in a thread safe manner
        private int _clientCount;
        private Socket _mainSocket;

        public Status CurrentStatus { get; set; }

        public int Port { get; set; }

        #region IDisposable Members

        public void Dispose() {
            GC.SuppressFinalize(this);
            Stop();
        }

        #endregion

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

                SetStatus(Status.Listening);
            }
            catch (SocketException se) {
                SendNotification(Notification.Error, Status.Connected, 0, "n/a", "Start: " + se.Message);
                SetStatus(Status.Stopped);
            }
        }

        public void Stop() {
            CloseSockets();
            Debug.WriteLine("Server Stop");
            SetStatus(Status.Stopped);
        }

        private void CloseSockets() {
            if (_mainSocket != null) {
                _mainSocket.Close();
            }

            foreach (var i in _socketList.Keys) {
                Socket socket;
                _socketList.TryRemove(i, out socket);
                if (socket != null) {
                    Debug.WriteLine("Closing Socket #" + i);
                    socket.Close();
                }
            }
        }

        //-----------------------------------------------------------
        // Events
        //-----------------------------------------------------------
        // Nested delegate class and matching event for Notification events

        public event NotificationCallback Notifications;

        // Nested enum for notification events

        // Send a status notification
        private void SetStatus(Status status) {
            CurrentStatus = status;
            SendNotification(Notification.StatusChange, status, 0, null, null);
        }

        private void SendNotification(Notification notification, Status status, int client, String ipaddress,
                                      Object data) {
            if (Notifications != null)
                Notifications(notification,
                              status,
                              client,
                              ipaddress,
                              data);
        }

        //-----------------------------------------------------------
        // Async handlers
        //-----------------------------------------------------------
        public void OnClientConnect(IAsyncResult async) {
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

                Debug.WriteLine("Opened Socket #" + _clientCount);

                // Send a welcome message to client
                SetStatus(Status.Connected);
                SendNotification(Notification.ClientConnected, Status.Connected, _clientCount,
                                 workerSocket.RemoteEndPoint.ToString(), null);

                // TODO: Notify client # & IP address

                //string msg = "Welcome client " + _clientCount + "\n";
                //SendMsgToClient(msg, m_clientCount);

                // Let the worker Socket do the further processing for the 
                // just connected client
                BeginReceive(workerSocket, _clientCount);

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                _mainSocket.BeginAccept(OnClientConnect, null);
            }
            catch (ObjectDisposedException) {
                // Ignore this
                //SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnClientConnection: Socket has been closed: " + e.Message);
            }
            catch (SocketException se) {
                SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnClientConnection: " + se.Message);
            }
        }

        // Start waiting for data from the client
        public void BeginReceive(Socket socket, int clientNumber) {
            try {
                if (ReceiveCallback == null) {
                    // Specify the call back function which is to be 
                    // invoked when there is any write activity by the 
                    // connected client
                    ReceiveCallback = OnDataReceived;
                }

                var packet = new SocketData(socket, clientNumber);
                socket.BeginReceive(packet.DataBuffer, 0,
                                    packet.DataBuffer.Length,
                                    SocketFlags.None,
                                    ReceiveCallback,
                                    packet);
            }
            catch (SocketException se) {
                SendNotification(Notification.Error, Status.Connected, 0, "n/a", "BeginReceive: " + se.Message);
            }
        }

        private void CloseSocket(SocketData socketData) {
            // Remove the reference to the worker socket of the closed client
            // so that this object will get garbage collected
            Socket socket;
            _socketList.TryRemove(socketData.ClientNumber, out socket);
            Debug.WriteLine("Closing Socket #" + socketData.ClientNumber);
            SendNotification(Notification.ClientDisconnected, Status.Connected, socketData.ClientNumber,
                             socket.RemoteEndPoint.ToString(), null);
            socket.Close();

            if (_socketList.Count == 0)
                SetStatus(Status.Listening);
        }

        // This the call back function which will be invoked when the socket
        // detects any client writing of data on the stream
        public void OnDataReceived(IAsyncResult async) {
            var socketData = (SocketData) async.AsyncState;
            try {
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client
                SocketError err;
                var iRx = socketData.Socket.EndReceive(async, out err);
                if (err != SocketError.Success || iRx == 0) {
                    CloseSocket(socketData);
                    return;
                }
                // Extract the characters as a buffer
                char[] charsToTrim = {'\r', '\n', '\0'};
                var data = Encoding.UTF8.GetString(socketData.DataBuffer, 0, iRx).Trim(charsToTrim);

                // TODO: Notify with client #
                //string msg = "" + socketData.m_clientNumber + ":";
                //AppendToRichEditControl(msg + szData);
                SendNotification(Notification.ReceivedData,
                                 Status.Connected, socketData.ClientNumber,
                                 socketData.Socket.RemoteEndPoint.ToString(), data);

                // Continue the waiting for data on the Socket
                BeginReceive(socketData.Socket, socketData.ClientNumber);
            }
            catch (ObjectDisposedException) {
                //SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnDataReceived: Socket has been closed: " + e.Message);
            }
            catch (SocketException se) {
                if (se.ErrorCode == 10054) // Error code for Connection reset by peer
                {
                    CloseSocket(socketData);
                }
                else {
                    SendNotification(Notification.Error, Status.Connected, 0, "n/a", "OnDataReceived: " + se.Message);
                }
            }
        }

        public void SendAwakeCommand(String cmd, String host, int port) {
            if (String.IsNullOrEmpty(host)) {
                SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a", "No wakeup host specified.");
                return;
            }
            if (port == 0) {
                SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a", "Invalid port.");
                return;
            }
            try {
                // Try to resolve the remote host name or address
                var resolvedHost = Dns.GetHostEntry(host);
                var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try {
                    // Create the endpoint that describes the destination
                    var destination = new IPEndPoint(resolvedHost.AddressList[0], port);

                    SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                     String.Format("Attempting connection to: {0}", destination));
                    clientSocket.Connect(destination);
                }
                catch (SocketException err) {
                    // Connect failed so close the socket and try the next address
                    clientSocket.Close();
                    clientSocket = null;
                    SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                     "Error connecting.\r\n" + String.Format("   Error: {0}", err.Message));
                }
                // Make sure we have a valid socket before trying to use it
                if ((clientSocket != null)) {
                    try {
                        clientSocket.Send(Encoding.ASCII.GetBytes(cmd + "\r\n"));

                        SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                         "Sent request " + cmd + " to wakeup host.");

                        // For TCP, shutdown sending on our side since the client won't send any more data
                        clientSocket.Shutdown(SocketShutdown.Send);
                    }
                    catch (SocketException err) {
                        SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                         "Error occured while sending or receiving data.\r\n" +
                                         String.Format("   Error: {0}", err.Message));
                    }
                }
                else {
                    SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                     "Unable to establish connection to server!");
                }
            }
            catch (SocketException err) {
                SendNotification(Notification.Wakeup, Status.Connected, 0, "n/a",
                                 String.Format("Socket error occured: {0}", err.Message));
            }
        }

        #region Nested type: SocketData

        public class SocketData {
            // Constructor which takes a Socket and a client number
            public int ClientNumber;
            public Socket Socket;
            // Buffer to store the data sent by the client
            public byte[] DataBuffer = new byte[1024];

            public SocketData(Socket socket, int clientNumber) {
                Socket = socket;
                ClientNumber = clientNumber;
            }
        }

        #endregion
    }
}