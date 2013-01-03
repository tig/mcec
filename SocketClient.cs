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

namespace MCEControl {
    /// <summary>
    /// SocketClient implements our TCP/IP client.
    /// 
    /// Note this class can be invoked from mutliple threads simultaneously 
    /// and must be threadsafe.
    /// 
    /// </summary>
    public class SocketClient : ServiceBase, IDisposable {

        private TcpClient _tcpClient;
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
            GC.SuppressFinalize(this);
        }
        #endregion

        ~SocketClient() {
            Dispose();
        }

        private BackgroundWorker _bw;
        public void Start(bool delay = false) {
            var currentCmd = new StringBuilder();
            _tcpClient = new TcpClient();
            _bw = new BackgroundWorker();
            _bw.WorkerReportsProgress = false;
            _bw.WorkerSupportsCancellation = true;
            _bw.DoWork += (sender, args) => {
                if (delay && _clientDelayTime > 0)
                {
                    SetStatus(ServiceStatus.Sleeping);
                    Thread.Sleep(_clientDelayTime);
                    if (_bw.CancellationPending)
                        return;
                }
                Connect();
            };
            _bw.RunWorkerAsync();
        }

        public void Stop() {
            _bw.CancelAsync();
            if (_tcpClient != null) {
                _tcpClient.Close();
                _tcpClient= null;
            }
            if (CurrentStatus != ServiceStatus.Stopped)
                SetStatus(ServiceStatus.Stopped);
        }

        // Send text to remote connection
        public void Send(String newText) {
            if (!_tcpClient.Connected || _bw.CancellationPending) return;
            try {
                byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(newText.Replace("\0xFF", "\0xFF\0xFF"));
                _tcpClient.GetStream().Write(buf, 0, buf.Length);
            }
            catch (IOException ioe) {
                Error(ioe.Message);
            }
        }

        private void Connect() {
            SetStatus(ServiceStatus.Connecting, String.Format("{0}:{1}", _host, _port));
            var endPoint = new IPEndPoint(Dns.GetHostEntry(_host).AddressList[0], _port);
            _tcpClient.BeginConnect(endPoint.Address, _port, ar => {
                if (_tcpClient == null)
                    return;
                try {
                    _tcpClient.EndConnect(ar);
                    SetStatus(ServiceStatus.Connected);
                    StringBuilder sb = new StringBuilder();
                    while (!_bw.CancellationPending && CurrentStatus == ServiceStatus.Connected && _tcpClient != null &&
                           _tcpClient.Connected) {
                        int input = _tcpClient.GetStream().ReadByte();
                        switch (input) {
                            case (byte) '\r':
                            case (byte) '\n':
                            case (byte) '\0':
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
                                sb.Append((char) input);
                                break;
                        }
                    }
                }
                catch (SocketException e) {
                    switch (e.ErrorCode) {
                        case 10061:
                            Error("Connection refused.");
                            break;

                        case 10060:
                            Error("Connection timed out.");
                            break;

                        default:
                            Error(String.Format("SocketException. ErrorCode: {0}{1}{2}", e.ErrorCode,
                                                        Environment.NewLine, e.Message));
                            break;
                    }
                }
                catch (IOException e) {
                    var sockExcept = e.InnerException as SocketException;

                    if (sockExcept != null) {
                        switch (sockExcept.ErrorCode) {
                            case 10054:
                                Error("Remote connection has closed.");
                                break;

                            case 10053:
                                SetStatus(ServiceStatus.Stopped);
                                break;

                            case 10060:
                                Error("Connection timed out.");
                                break;

                            default:
                                Error(String.Format("SocketException (RecieveData). ErrorCode: {0}{1}{2}",
                                                            sockExcept.ErrorCode, Environment.NewLine, e.Message));
                                break;
                        }
                    }
                    else {
                        Error(String.Format("IOException. {0}", e.Message));
                    }
                }
            }, null);

            Debug.WriteLine("BeginConnect returned");
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