using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCEControl
{
    public enum ServiceNotification
    {
        Initialized = 1,
        StatusChange,
        ReceivedData,
        ClientConnected,
        ClientDisconnected,
        Write,
        WriteFailed,
        Error,
        Wakeup
    }

    public enum ServiceStatus
    {
        Connecting,
        Connected,
        Sleeping,
        Stopped
    }

    /// <summary>
    /// The base class that each of MCE Controller's services are based
    /// on (SocketServer, SocketClient, SerialServer).
    /// 
    /// Allows core code to be able to interact with services (e.g.
    /// start, stop, configure, send replies) without having to 
    /// know what servic is active.
    /// </summary>
    public abstract class Reply
    {
        //public abstract String Command { get; set; }
        public abstract void Write(String text);
        public void WriteLine(String textLine) {
            Write(textLine + Environment.NewLine);
        }
    }

    public abstract class ServiceBase {
        public delegate
            void NotificationCallback(ServiceNotification notify, ServiceStatus status, Reply reply, String msg = "");
        public event NotificationCallback Notifications;

        public ServiceStatus CurrentStatus { get; set; }

        // Send a status notification
        protected void SetStatus(ServiceStatus status, String msg = "")
        {
            CurrentStatus = status;
            SendNotification(ServiceNotification.StatusChange, status, null, msg);
        }

        protected void SendNotification(ServiceNotification notification, ServiceStatus status, Reply replyObject = null, String msg = "")
        {
            if (Notifications != null)
                Notifications(notification,
                              status,
                              replyObject,
                              msg);
        }

        // Send an error notification
        protected void Error(String msg)
        {
            if (Notifications != null)
                Notifications(ServiceNotification.Error, CurrentStatus, null, msg);
        }
    }
}
