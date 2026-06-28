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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using log4net;

namespace MCEControl;
public abstract class ServiceBase {
    protected ILog Log4 { get; set; }

    /// <summary>
    /// TELEMETRY: Enables collecting of how long sessions are connected for.
    /// </summary>
    private Stopwatch _connectedTime = new();

    protected ServiceBase() {
        Log4 = log4net.LogManager.GetLogger("MCEControl");
        CurrentStatus = ServiceStatus.Stopped;
    }

    public delegate void NotificationCallback(ServiceNotification notify, ServiceStatus status, Reply reply, string msg = "");
    public event NotificationCallback Notifications = null!;

    public ServiceStatus CurrentStatus { get; set; }

    public virtual void Send(string text, Reply? replyContext = null) {
        if (text == null) {
            throw new ArgumentNullException(nameof(text));
        }

        Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending \"{Regex.Escape(text)}\"");

        // TELEMETRY: 
        // what: the number of commands of each type (key) sent
        // why: to understand what commands are used to control other systems and which are not
        // how is PII protected: we only collect the text if it is a key for a built-in command
        Command? userDefined = MainWindow.Instance.Invoker.Values.Cast<Command>().FirstOrDefault(q => (q.Cmd == text.Trim('\r').Trim('\n') && q.UserDefined == false));
        TelemetryService.Instance.TelemetryClient!.GetMetric($"{(userDefined == null ? "<userDefined>" : text.Trim('\r').Trim('\n'))} Sent").TrackValue(1);
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
                    TelemetryService.Instance.TelemetryClient!.GetMetric($"{this.GetType().Name} Connected Time").TrackValue(_connectedTime.ElapsedMilliseconds);
                }
                break;
        }
        CurrentStatus = status;
        SendNotification(ServiceNotification.StatusChange, status, null, msg);
    }

    protected void SendNotification(ServiceNotification notification, ServiceStatus status, Reply? replyContext = null, String msg = "") {
        Notifications?.Invoke(notification, status, replyContext!, msg);
    }

    // Send an error notification
    protected void Error(String msg) {
        Log4.Debug(msg);
        Notifications?.Invoke(ServiceNotification.Error, CurrentStatus, null!, msg);
    }
}
