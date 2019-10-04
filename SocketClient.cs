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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using log4net;
using MCEControl.Properties;

namespace MCEControl {
    /// <summary>
    /// SocketClient implements our TCP/IP client.
    /// 
    /// Note this class can be invoked from mutliple threads simultaneously 
    /// and must be threadsafe.
    /// 
    /// </summary>
    sealed public class SocketClient : ServiceBase, IDisposable {
        private readonly string _host = "";
        private readonly int _port;
        private readonly int _clientDelayTime;

        public SocketClient(AppSettings settings) {
            _port = settings.ClientPort;
            _host = settings.ClientHost;
            _clientDelayTime = settings.ClientDelayTime;
        }

        // Finalize 

        #region IDisposable Members
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ~SocketClient() {
            Dispose();
        }

        private TcpClient _tcpClient;
        private BackgroundWorker _bw;

        private void Dispose(bool disposing) {
            if (disposing) {
                if (_bw != null) {
                    _bw.CancelAsync();
                    _bw.Dispose();
                    _bw = null;
                }
                if (_tcpClient != null) {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
            }
        }

        public void Start(bool delay = false) {
            var currentCmd = new StringBuilder();
            _tcpClient = new TcpClient();
            _bw = new BackgroundWorker();
            _bw.WorkerReportsProgress = false;
            _bw.WorkerSupportsCancellation = true;
            _bw.DoWork += (sender, args) => {
                if (delay && _clientDelayTime > 0) {
                    SetStatus(ServiceStatus.Sleeping);
                    Thread.Sleep(_clientDelayTime);
                }
                if (_bw == null || _bw.CancellationPending || _tcpClient == null)
                    return;
                Connect();
            };
            _bw.RunWorkerAsync();
        }

        public void Stop() {
            Dispose(true);
            SetStatus(ServiceStatus.Stopped);
        }

        // Send text to remote connection
        public override void Send(string text, Reply replyContext = null) {
            if (_tcpClient == null || !_tcpClient.Connected || _bw.CancellationPending) return;
            try {
                byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(text.Replace("\0xFF", "\0xFF\0xFF"));
                _tcpClient.GetStream().Write(buf, 0, buf.Length);
            }
            catch (IOException ioe) {
                Error(ioe.Message);
            }

            // TODO: Implement notifications
        }

        private void Connect() {
            SetStatus(ServiceStatus.Started, $"{_host}:{_port}");

            IPEndPoint endPoint;
            try {
                // GetHostEntry returns a list. We need to pick the IPv4 entry.
                // TODO: Support ipv6
                IPAddress[] ipv4Addresses = Array.FindAll(Dns.GetHostEntry(_host).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4Addresses.Length == 0)
                    throw new Exception($"{_host}:{_port} didn't resolve to a valid address.");

                endPoint = new IPEndPoint(ipv4Addresses[0], _port);
                
                _tcpClient.BeginConnect(endPoint.Address, _port, ar => {
                    if (_tcpClient == null)
                        return;
                    try {
                        log4.Debug($"Client BeginConnect: { _host}:{ _port}");
                        _tcpClient.EndConnect(ar);
                        log4.Debug($"Client Back from EndConnect: { _host}:{ _port}");
                        SetStatus(ServiceStatus.Connected, $"{_host}:{_port}");
                        StringBuilder sb = new StringBuilder();
                        while (_bw != null &&
                            !_bw.CancellationPending &&
                            CurrentStatus == ServiceStatus.Connected &&
                            _tcpClient != null &&
                            _tcpClient.Connected) {
                            int input = _tcpClient.GetStream().ReadByte();
                            switch (input) {
                                case (byte)'\r':
                                case (byte)'\n':
                                case (byte)'\0':
                                    if (sb.Length > 0) {
                                        SendNotification(ServiceNotification.ReceivedData, ServiceStatus.Connected, new ClientReplyContext(_tcpClient), sb.ToString());
                                        sb.Clear();
                                        System.Threading.Thread.Sleep(100);
                                    }
                                    break;

                                case -1:
                                    Error("No more data.");
                                    return;

                                default:
                                    sb.Append((char)input);
                                    break;
                            }
                        }
                    }
                    catch (SocketException e) {
                        log4.Debug($"SocketClient SocketException: {e.GetType().Name}: {e.Message}");
                        CatchSocketException(e);
                    }
                    catch (IOException e) {
                        var sockExcept = e.InnerException as SocketException;
                        log4.Debug($"SocketClient IOException: {e.GetType().Name}: {e.Message}");
                        if (sockExcept != null) {
                            CatchSocketException(sockExcept);
                        }
                        else {
                            Error($"SocketClient IOException: {e.GetType().Name}: {e.Message}");
                        }
                    }
                    catch (Exception e) {
                        // Got this when endPoint = new IPEndPoint(Dns.GetHostEntry(_host).AddressList[0], _port) 
                        // resolved to an ipv6 address
                        log4.Debug($"SocketClient Generic Exception: {e.GetType().Name}: {e.Message}");
                        Error($"SocketClient Generic Exception: {e.GetType().Name} {e.Message}");
                    }
                    finally {
                        //log4.Debug("finally - Stopping");
                        //Stop();
                    }
                }, null);
            }
            catch (SocketException e) {
                log4.Debug($"SocketClient.Client SocketException: {e.GetType().Name}: {e.Message}");
                CatchSocketException(e);
                if (_tcpClient != null) _tcpClient.Close();
                return;
            }
            catch (Exception e) {
                log4.Debug($"SocketClient.Client Generic Exception: {e.GetType().Name}: {e.Message}");
                Error($"SocketClient.Client Generic Exception: {e.GetType().Name}: {e.Message}");
                if (_tcpClient != null) _tcpClient.Close();
                return;
            }

            log4.Debug("BeginConnect returned");
        }

        private void CatchSocketException(SocketException e) {
            switch (e.ErrorCode) {
                case 10004: // WSAEINTR - Interrupted function call
                    // Not an error - this means the client has shut down
                    break;

                default:
                    string s = Resources.ResourceManager.GetString($"WSA_{e.ErrorCode}");
                    if (s == null)
                        Error($"{e.Message} ({e.ErrorCode})");
                    else {
                        Error($"{e.Message}. {s} ({e.ErrorCode})");
                    }
                    break;
            }
        }

        #region Nested type: ClientReplyContext

        public class ClientReplyContext : Reply {
            private readonly TcpClient _tcpClient;
            // Constructor which takes a Socket and a client number
            public ClientReplyContext(TcpClient tcpClient) {
                _tcpClient = tcpClient;
            }

            public override void Write(String text) {
                if (!_tcpClient.Connected) return;

                byte[] buf = System.Text.Encoding.ASCII.GetBytes(text.Replace("\0xFF", "\0xFF\0xFF"));
                _tcpClient.GetStream().Write(buf, 0, buf.Length);
            }
        }

        #endregion
    }
}
