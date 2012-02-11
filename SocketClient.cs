//-------------------------------------------------------------------
// By Charlie Kindel
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the BSD License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.Windows.Forms;
using System.Threading;

namespace MCEControl
{
    /// <summary>
    /// SocketClient implements our TCP/IP client.
    /// 
    /// Note this class can be invoked from mutliple threads simultaneously 
    /// and must be threadsafe.
    /// 
    /// </summary>
    public class SocketClient : IDisposable
    {
        private TextReader Reader;
        private TextWriter Writer;

        private Socket ServerSocket = null;
        private Socket ListeningSocket = null;
        private Status CurrentStatus;

        // These settings are passed into the SocketClient default constructor
        // 
        private bool Delay = false;
        private int Port = 0;
        private string Host = "";
        private int ClientDelayTime = 0;

        public SocketClient(AppSettings settings)
        {
            this.Port = settings.ClientPort;
            this.Host = settings.ClientHost;
            this.ClientDelayTime = settings.ClientDelayTime;
        }

        // Finalize 
        ~SocketClient()
        {
            Dispose();
        }

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Stop();
        }

        #endregion

        // Nested delegate class and matching event
        public delegate
            void NotificationCallback(Notification notify, Object data);
        public event NotificationCallback Notifications;

        // Nested enum for notifications
        public enum Notification
        {
            Initialized = 1,
            StatusChange,
            ReceivedData,
            End,
            Error,
            Wakeup
        }

        // Nested enum for supported states
        public enum Status
        {
            Listening,
            Connected,
            Sleeping,
            Closed
        }


        public void Start()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(EstablishSocket), this);
        }

        // If delay is true the client will delay connecting 
        // ClientStartDelay milliseconds
        public void Start(bool delay)
        {
            Delay = delay;
            Start();
        }


        public void Stop()
        {
            if (Reader != null)
            {
                Reader.Close();
                Reader = null;
            }
            if (Writer != null)
            {
                Writer.Close();
                Writer = null;
            }
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
                ListeningSocket = null;
            }
            if (ServerSocket != null)
            {
                ServerSocket.Close();
                ServerSocket = null;
            }
            if (CurrentStatus != Status.Closed)
                SetStatus(Status.Closed);
        }

        // Send text to remote connection
        public void Send(String newText)
        {
            Writer.Write(newText);
            Writer.Flush();
        }

        // Send a status notification
        private void SetStatus(Status status)
        {
            this.CurrentStatus = status;
            if (Notifications != null)
                Notifications(Notification.StatusChange, status);
        }

        // Establish a socket connection and start receiving (either as a 
        // client or a server)
        //
        private void EstablishSocket(Object state)
        {
            NetworkStream stream = null;
            IPEndPoint endPoint = null;
            SocketClient SP = (SocketClient)state;

            try
            {
                endPoint = new IPEndPoint(Dns.GetHostEntry(SP.Host).AddressList[0], SP.Port);

                try
                {
                    // Do we need to delay? If so sleep the thread.
                    if (Delay && ClientDelayTime > 0)
                    {
                        SetStatus(Status.Sleeping);
                        Thread.Sleep(ClientDelayTime);
                        if (CurrentStatus == Status.Closed)
                            return;
                    }
                    Socket temp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    temp.Blocking = true;
                    SetStatus(Status.Listening);
                    temp.Connect(endPoint);
                    ServerSocket = temp;
                    //ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);

                    // Commented out 4/15/05. I see no reason for a send timeout since we send nothing; this may
                    // be the cause of the bug that is causing the connection to silently drop.
                    //
                    //ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 5000);
                    stream = new NetworkStream(ServerSocket);
                    Reader = new StreamReader(stream);
                    Writer = new StreamWriter(stream);
                }
                catch (SocketException e)
                {
                    if (e != null && 10061 == e.ErrorCode)
                    {
                        // Connection refused
                        ListeningSocket = null;
                        ServerSocket = null;
                        Notifications(Notification.End, "Remote connection was refused.");
                        return;
                    }
                    else
                        Notifications(Notification.Error, "Error Initializing Socket:\r\n" + e.Message);
                }

                // If it all worked out, create stream objects
                if (ServerSocket != null)
                {
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
                else
                {
                    Notifications(Notification.Error,
                        "Failed to Establish Socket, did you specify the correct port?");
                }
            }
            catch (IOException e)
            {
                SocketException sockExcept = e.InnerException as SocketException;
                if (sockExcept != null && 10054 == sockExcept.ErrorCode)
                {
                    Notifications(Notification.End, "Remote connection has closed.");
                }
                else if (sockExcept != null && 10053 == sockExcept.ErrorCode)
                {
                    SetStatus(Status.Closed);
                }
                else
                {
                    if (Notifications != null)
                        Notifications(Notification.Error, "Socket Error:\r\n" + e.Message);
                }
            }
            catch (Exception e)
            {
                Notifications(Notification.Error, "General Error:\r\n" + e.Message);
            }
        }

        String CurrentCmd = null;
        /// <summary>
        /// NewData is called when new data arrives.
        /// </summary>
        private void ReceiveData()
        {
            for (int b = Reader.Read(); b != -1; b = Reader.Read())
            {
                if (b == 0x0a || b == 0x0d)
                {
                    if (CurrentCmd != null && CurrentCmd.Length > 0)
                    {
                        Notifications(Notification.ReceivedData, CurrentCmd);
                        CurrentCmd = null;
                    }
                }
                else
                {
                    CurrentCmd = CurrentCmd + Convert.ToChar(b);
                }
            }
        }
    }
}
