//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using log4net;

namespace MCEControl {
    public enum ServiceNotification {
        None = 0,
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

    public enum ServiceStatus {
        Started,
        Waiting,
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
    public abstract class Reply {
        //public abstract String Command { get; set; }
        public abstract void Write(String text);
        public void WriteLine(String textLine) {
            Write(textLine + Environment.NewLine);
        }
    }

    public abstract class ServiceBase {
        private log4net.ILog log4;
        protected ILog Log4 { get => log4; set => log4 = value; }

        /// <summary>
        /// TELEMETRY: Enables collecting of how long sessions are connected for.
        /// </summary>
        private Stopwatch _connectedTime = new Stopwatch();

        protected ServiceBase() {
            Log4 = log4net.LogManager.GetLogger("MCEControl");
            CurrentStatus = ServiceStatus.Stopped;
        }

        public delegate
            void NotificationCallback(ServiceNotification notify, ServiceStatus status, Reply reply, String msg = "");
        public event NotificationCallback Notifications;

        public ServiceStatus CurrentStatus { get; set; }

        public virtual void Send(string text, Reply replyContext = null) {
            if (text == null) throw new ArgumentNullException(nameof(text));

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending \"{text}\"");

            // TELEMETRY: 
            // what: the number of commands of each type (key) sent
            // why: to understand what commands are used to control other systems and which are not
            // how is PII protected: we only collect the text if it is a key for a built-in command
            var userDefined = MainWindow.Instance.Invoker.Values.Cast<Command>().FirstOrDefault(q => (q.Cmd == text.Trim('\r').Trim('\n') && q.UserDefined == false));
            TelemetryService.Instance.GetTelemetryClient().GetMetric($"{(userDefined == null ? "<userDefined>" : text.Trim('\r').Trim('\n'))} Sent").TrackValue(1);
        }

        // Send a status notification
        protected void SetStatus(ServiceStatus status, String msg = "") {
            // TELEMETRY: 
            // what: Service status
            // why: to understand the typical/non-typical conenction flows
            // how is PII protected: no PII is involved
            TelemetryService.Instance.TrackEvent($"{this.GetType().Name} {Enum.GetName(typeof(ServiceStatus), status)}", properties: new Dictionary<string, string> { { "msg", msg } });

            switch (status) {
                case ServiceStatus.Connected:
                    _connectedTime.Start();
                    break;

                case ServiceStatus.Stopped:
                    if (_connectedTime.IsRunning) {
                        _connectedTime.Stop();
                        // TELEMETRY: 
                        // what: how long the session was connected for
                        // why: to understand the typical/non-typical connection scenarios
                        // how is PII protected: no PII is involved
                        TelemetryService.Instance.GetTelemetryClient().GetMetric($"{this.GetType().Name} Connected Time").TrackValue(_connectedTime.ElapsedMilliseconds);
                    }
                    break;
            }
            CurrentStatus = status;
            SendNotification(ServiceNotification.StatusChange, status, null, msg);
        }

        protected void SendNotification(ServiceNotification notification, ServiceStatus status, Reply replyContext = null, String msg = "") {
            Notifications?.Invoke(notification, status, replyContext, msg);
        }

        // Send an error notification
        protected void Error(String msg) {
            Log4.Debug(msg);
            Notifications?.Invoke(ServiceNotification.Error, CurrentStatus, null, msg);
        }
    }
}
