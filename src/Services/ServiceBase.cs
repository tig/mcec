//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
//
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using log4net;

namespace MCEControl;
public abstract class ServiceBase {
    protected ILog Log4 { get; set; } = LogManager.GetLogger("MCEControl");

    /// <summary>
    /// TELEMETRY: Enables collecting of how long sessions are connected for.
    /// </summary>
    private readonly Stopwatch _connectedTime = new();

    // Typed service events (#211). These replace the old 4-arg stringly NotificationCallback
    // (with its god-enum ServiceNotification) that forced every subscriber to demultiplex
    // lifecycle, data, and diagnostics out of one callback. Events are raised on whatever
    // thread the transport happens to be running on (ThreadPool callbacks, read threads,
    // async continuations); subscribers own any UI-thread marshaling.

    /// <summary>Raised when the service's lifecycle status changes. <c>detail</c> is optional
    /// human-readable context (e.g. the bound endpoint for Started).</summary>
    public event Action<ServiceStatus, string>? StatusChanged;

    /// <summary>Raised when a complete command has been received from the transport.
    /// <c>reply</c> is the context any command output should be written back to.</summary>
    public event Action<Reply, string>? CommandReceived;

    /// <summary>Raised when the service hits an error. The <see cref="ServiceError"/> carries
    /// the typed <see cref="System.Net.Sockets.SocketError"/>/HResult when one applies.</summary>
    public event Action<ServiceError>? ErrorOccurred;

    public ServiceStatus CurrentStatus { get; private set; } = ServiceStatus.Stopped;

    public virtual void Send(string text, Reply? replyContext = null) {
        if (text == null) {
            throw new ArgumentNullException(nameof(text));
        }

        Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending \"{Regex.Escape(text)}\"");

        // TELEMETRY:
        // what: the number of commands of each type (key) sent
        // why: to understand what commands are used to control other systems and which are not
        // how is PII protected: we only collect the text if it is a key for a built-in command
        // #209: read the command table via the UI-agnostic AgentRuntime seam (populated by both the
        // GUI and headless hosts); never MainWindow, which this engine-layer code must not touch.
        Command? userDefined = AgentRuntime.Invoker?.Values.Cast<Command>().FirstOrDefault(q => (q.Cmd == text.Trim('\r').Trim('\n') && q.UserDefined == false));
        TelemetryService.Instance.TrackMetric($"{(userDefined == null ? "<userDefined>" : text.Trim('\r').Trim('\n'))} Sent", 1);
    }

    // Set the current status and raise StatusChanged
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
                    TelemetryService.Instance.TrackMetric($"{this.GetType().Name} Connected Time", _connectedTime.ElapsedMilliseconds);
                }
                break;
        }
        CurrentStatus = status;
        StatusChanged?.Invoke(status, msg);
    }

    // Raise CommandReceived for a complete command received from the transport
    protected void OnCommandReceived(Reply reply, string command) {
        CommandReceived?.Invoke(reply, command);
    }

    // Raise ErrorOccurred for a plain-message error
    protected void Error(String msg) {
        Error(new ServiceError(msg));
    }

    // Raise ErrorOccurred with a typed error payload
    protected void Error(ServiceError error) {
        if (error is null) {
            throw new ArgumentNullException(nameof(error));
        }
        Log4.Debug(error.ToString());
        ErrorOccurred?.Invoke(error);
    }
}
