//-------------------------------------------------------------------
// By Charlie Kindel
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MCEControl {
    /// <summary>
    /// SocketClient implements our TCP/IP client.
    /// 
    /// Note this class can be invoked from mutliple threads simultaneously 
    /// and must be threadsafe.
    /// 
    /// </summary>
    public class SocketClient : IDisposable {
        #region Delegates

        public delegate
            void NotificationCallback(Notification notify, Object data);

        #endregion

        #region Notification enum

        public enum Notification {
            Initialized = 1,
            StatusChange,
            ReceivedData,
            End,
            Error,
            Wakeup
        }

        #endregion

        // Nested enum for supported states

        #region Status enum

        public enum Status {
            Listening,
            Connected,
            Sleeping,
            Closed
        }

        #endregion

        private readonly int _clientDelayTime;
        private readonly string _host = "";
        private readonly int _port;
        private String _currentCmd;

        private Status _currentStatus;

        // These settings are passed into the SocketClient default constructor
        // 
        private bool _delay;
        private Socket _listeningSocket;
        private TextReader _reader;
        private Socket _serverSocket;
        private TextWriter _writer;

        public SocketClient(AppSettings settings) {
            _port = settings.ClientPort;
            _host = settings.ClientHost;
            _clientDelayTime = settings.ClientDelayTime;
        }

        // Finalize 

        #region IDisposable Members

        public void Dispose() {
            GC.SuppressFinalize(this);
            Stop();
        }

        #endregion

        ~SocketClient() {
            Dispose();
        }

        // Nested delegate class and matching event

        public event NotificationCallback Notifications;

        // Nested enum for notifications


        public void Start() {
            ThreadPool.QueueUserWorkItem(EstablishSocket, this);
        }

        // If delay is true the client will delay connecting 
        // ClientStartDelay milliseconds
        public void Start(bool delay) {
            _delay = delay;
            Start();
        }


        public void Stop() {
            if (_reader != null) {
                _reader.Close();
                _reader = null;
            }
            if (_writer != null) {
                _writer.Close();
                _writer = null;
            }
            if (_listeningSocket != null) {
                _listeningSocket.Close();
                _listeningSocket = null;
            }
            if (_serverSocket != null) {
                _serverSocket.Close();
                _serverSocket = null;
            }
            if (_currentStatus != Status.Closed)
                SetStatus(Status.Closed);
        }

        // Send text to remote connection
        public void Send(String newText) {
            if (_writer != null && _currentStatus == Status.Connected) {
                _writer.Write(newText);
                _writer.Flush();
            }
            else {
                Notifications(Notification.Error, "Send attempted without valid connection: " + newText);
            }
        }

        // Send a status notification
        private void SetStatus(Status status) {
            _currentStatus = status;
            if (Notifications != null)
                Notifications(Notification.StatusChange, status);
        }

        // Establish a socket connection and start receiving (either as a 
        // client or a server)
        //
        private void EstablishSocket(Object state) {
            var sp = (SocketClient) state;

            try {
                var endPoint = new IPEndPoint(Dns.GetHostEntry(sp._host).AddressList[0], sp._port);

                try {
                    // Do we need to delay? If so sleep the thread.
                    if (_delay && _clientDelayTime > 0) {
                        SetStatus(Status.Sleeping);
                        Thread.Sleep(_clientDelayTime);
                        if (_currentStatus == Status.Closed)
                            return;
                    }
                    var temp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                        Blocking = true
                    };
                    SetStatus(Status.Listening);
                    temp.Connect(endPoint);
                    _serverSocket = temp;
                    //ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);

                    // Commented out 4/15/05. I see no reason for a send timeout since we send nothing; this may
                    // be the cause of the bug that is causing the connection to silently drop.
                    //
                    //ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 5000);
                    var stream = new NetworkStream(_serverSocket);
                    _reader = new StreamReader(stream);
                    _writer = new StreamWriter(stream);
                }
                catch (SocketException e) {
                    switch (e.ErrorCode) {
                        case 10061:
                            // Connection refused
                            Notifications(Notification.End, "Connection refused.");
                            break;

                        case 10060:
                            // Connection timeout
                            Notifications(Notification.End, "Connection timed out.");
                            break;

                        default:
                            Notifications(Notification.End, String.Format("SocketException. ErrorCode: {0}{1}{2}", e.ErrorCode, Environment.NewLine, e.Message));
                            break;
                    }
                    _listeningSocket = null;
                    _serverSocket = null;
                    return;
                }

                // If it all worked out, create stream objects
                if (_serverSocket != null) {
                    SetStatus(Status.Connected);
                    Notifications(Notification.Initialized, this);
                    // Start receiving talk
                    // Note: on w2k and later platforms, the NetworkStream.Read()
                    // method called in ReceiveData will generate an exception when
                    // the remote connection closes. We handle this case in our
                    // catch block below.
                    ReceiveData();

                    // On Win9x platforms, NetworkStream.Read() returns 0 when
                    // the remote connection closes, prompting a graceful return
                    // from ReceiveData() above. We will generate a Notification.End
                    // message here to handle the case and shut down the remaining
                    // WinTalk instance.
                    Notifications(Notification.End, "Remote connection has closed.");
                }
                else {
                    Notifications(Notification.End,
                                  "Could not connect.");
                }
            }
            catch (IOException e) {
                var sockExcept = e.InnerException as SocketException;

                if (sockExcept != null) {
                    switch (sockExcept.ErrorCode)
                    {
                        case 10054:
                            Notifications(Notification.End, "Remote connection has closed.");
                            break;

                        case 10053:
                            SetStatus(Status.Closed);
                            break;

                        case 10060:
                            // Connection timeout
                            Notifications(Notification.End, "Connection timed out.");
                            break;

                        default:
                            Notifications(Notification.End, 
                                String.Format("SocketException (RecieveData). ErrorCode: {0}{1}{2}", sockExcept.ErrorCode, Environment.NewLine, e.Message));
                            break;
                    }                    
                }
                else {
                    Notifications(Notification.End, String.Format("IOException. ErrorCode: {0}{1}{2}", sockExcept.ErrorCode, Environment.NewLine, e.Message));
                }
            }
            catch (Exception e) {
                Notifications(Notification.End, "General Error:\r\n" + e.Message);
            }
        }

        /// <summary>
        /// NewData is called when new data arrives.
        /// </summary>
        private void ReceiveData() {
            for (var b = _reader.Read(); b != -1; b = _reader.Read()) {
                if (b == 0x0a || b == 0x0d) {
                    if (!string.IsNullOrEmpty(_currentCmd)) {
                        Notifications(Notification.ReceivedData, _currentCmd);
                        _currentCmd = null;
                    }
                }
                else {
                    _currentCmd = _currentCmd + Convert.ToChar(b);
                }
            }
        }
    }
}