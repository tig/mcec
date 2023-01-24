//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MCEControl.Properties;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MCEControl {
    /// <summary>
    /// SocketClient implements our TCP/IP client.
    /// 
    /// Note this class can be invoked from mutliple threads simultaneously 
    /// and must be threadsafe.
    /// 
    /// </summary>
    public sealed class SocketClient : ServiceBase, IDisposable {
        private readonly string _host = "";
        private readonly int _port;
        private readonly int _clientDelayTime;

        public SocketClient(AppSettings settings) {
            if (settings is null) {
                throw new ArgumentNullException(nameof(settings));
            }

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
                _bw?.CancelAsync();
                _bw?.Dispose();
                _bw = null;
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
        }

        public void Start(bool delay = false) {
            var currentCmd = new StringBuilder();
            _tcpClient = new TcpClient();
            _bw = new BackgroundWorker {
                WorkerReportsProgress = false,
                WorkerSupportsCancellation = true
            };
            _bw.DoWork += (sender, args) => {
                if (delay && _clientDelayTime > 0) {
                    SetStatus(ServiceStatus.Sleeping);
                    Thread.Sleep(_clientDelayTime);
                }
                if (_bw == null || _bw.CancellationPending || _tcpClient == null) {
                    return;
                }

                Connect();
            };
            _bw.RunWorkerAsync();
        }

        /// <summary>
        /// Stops the client
        /// </summary>
        public void Stop() {
            Dispose(true);
            SetStatus(ServiceStatus.Stopped);
        }

        /// <summary>
        ///  Send text to remote connection
        /// </summary>
        /// <param name="text"></param>
        /// <param name="replyContext"></param>
        public override void Send(string text, Reply replyContext = null) {
            base.Send(text, replyContext);

            if (text is null) {
                throw new ArgumentNullException(nameof(text));
            }

            if (_tcpClient == null || !_tcpClient.Connected || _bw.CancellationPending) {
                return;
            }

            try {
                var buf = System.Text.ASCIIEncoding.ASCII.GetBytes(text.Replace("\0xFF", "\0xFF\0xFF"));
                _tcpClient.GetStream().Write(buf, 0, buf.Length);
            }
            catch (IOException ioe) {
                Error(ioe.Message);
            }

            // TODO: Implement notifications
        }

        private void Connect() {
            IPEndPoint endPoint;
            Log4.Debug($"SocketClient: Connect - {_host}:{_port}");
            Debug.Assert(_tcpClient != null);
            try {

                // See if we've just been handed a straight IPv4 address, if so don't bother with DNS
                IPAddress hostIp;
                if (!IPAddress.TryParse(_host, out hostIp)) {
                    // GetHostEntry returns a list. We need to pick the IPv4 entry.
                    // TODO: Support ipv6
                    var ipv4Addresses = Array.FindAll(Dns.GetHostEntry(_host).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
                    Log4.Debug($"SocketClient: {ipv4Addresses.Length} IP v4 addresses found");

                    if (ipv4Addresses.Length == 0) {
                        throw new IOException($"{_host}:{_port} didn't resolve to a valid address");
                    }
                    hostIp = ipv4Addresses[0];
                }

                Log4.Debug($"SocketClient: new IPEndPoint({hostIp}, {_port})");
                endPoint = new IPEndPoint(hostIp, _port);

                // TELEMETRY: Do not pass _host to SetStatus to avoid collecting PII
                SetStatus(ServiceStatus.Started, $"{hostIp}:{_port}");

                if (_tcpClient == null) {
                    Log4.Debug($"SocketClient: Can't Connect - _tcpClient is null");
                    return;
                }

                Log4.Debug($"SocketClient: BeginConnect({endPoint.Address}, {_port})");
                _ = _tcpClient.BeginConnect(endPoint.Address, _port, ar => {
                    Log4.Debug($"SocketClient: In BeginConnect call back: {_host}:{_port}");
                    if (_tcpClient == null) {
                        Log4.Debug($"SocketClient: Can't Connect - _tcpClient is null");
                        return;
                    }

                    try {
                        Log4.Debug($"SocketClient: BeginConnect succeeded: {_host}:{_port}");
                        _tcpClient.EndConnect(ar);
                        Log4.Debug($"SocketClient: Back from EndConnect: {_host}:{_port}");
                        SetStatus(ServiceStatus.Connected, $"{_host}:{_port}");
                        var sb = new StringBuilder();
                        while (_bw != null &&
                            !_bw.CancellationPending &&
                            CurrentStatus == ServiceStatus.Connected &&
                            _tcpClient != null &&
                            _tcpClient.Connected) {
                            // TODO: Move exception handling around this
                            var input = _tcpClient.GetStream().ReadByte();
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
                        Log4.Debug($"SocketClient: {e.GetType().Name}: {e.Message}");
                        CatchSocketException(e);
                    }
                    catch (IOException e) {
                        if (e.InnerException is SocketException sockExcept) {
                            CatchSocketException(sockExcept);
                        }
                        else {
                            Error($"SocketClient: {e.GetType().Name}: {e.Message}");
                        }
                    }
                    catch (Exception e) {
                        // Got this when endPoint = new IPEndPoint(Dns.GetHostEntry(_host).AddressList[0], _port) 
                        // resolved to an ipv6 address
                        Log4.Debug($"SocketClient: Generic Exception: {e.GetType().Name}: {e.Message}");
                        Error($"SocketClient: Generic Exception: {e.GetType().Name} {e.Message}");
                    }
                    finally {
                        //log4.Debug("finally - Stopping");
                        //Stop();
                    }
                }, null);
            }
            catch (SocketException e) {
                Log4.Debug($"SocketClient: (BeginConnect) {e.GetType().Name}: {e.Message}");
                CatchSocketException(e);
                if (_tcpClient != null) {
                    _tcpClient.Close();
                }

                return;
            }
            catch (Exception e) {
                Log4.Debug($"SocketClient: (BeginConnect) {e.GetType().Name}: {e.Message}");
                Error($"SocketClient: (BeginConnect) Generic Exception: {e.GetType().Name}: {e.Message}");
                if (_tcpClient != null) {
                    _tcpClient.Close();
                }

                return;
            }
        }

        private void CatchSocketException(SocketException e) {
            switch (e.ErrorCode) {
                case 10004: // WSAEINTR - Interrupted function call
                    // Not an error - this means the client has shut down
                    break;

                default:
                    var s = Resources.ResourceManager.GetString($"WSA_{e.ErrorCode}", System.Globalization.CultureInfo.InvariantCulture);
                    if (s == null) {
                        Error($"{e.Message} ({e.ErrorCode})");
                    }
                    else {
                        Error($"{e.Message}. {s} ({e.ErrorCode})");
                    }
                    break;
            }
        }

        #region Nested type: ClientReplyContext

        internal class ClientReplyContext : Reply {
            private readonly TcpClient _tcpClient;
            // Constructor which takes a Socket and a client number
            public ClientReplyContext(TcpClient tcpClient) {
                _tcpClient = tcpClient;
            }

            public override void Write(String text) {
                if (text is null) {
                    throw new ArgumentNullException(nameof(text));
                }

                if (!_tcpClient.Connected) {
                    return;
                }

                var buf = System.Text.Encoding.ASCII.GetBytes(text.Replace("\0xFF", "\0xFF\0xFF"));
                _tcpClient.GetStream().Write(buf, 0, buf.Length);
            }
        }

        #endregion
    }
}
