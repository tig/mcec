//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MCEControl.Hooks;
using Microsoft.Win32;
using static MCEControl.Hooks.PowerNativeMethods;
using Timer = System.Windows.Forms.Timer;

#pragma warning disable CA1416

namespace MCEControl;

/// <summary>
///     Singleton service  that monitors user activity and sends a message to a control system when detected. This enables
///     utilizing
///     a PC as a room occupancy sensor (e.g. if the PC is being used, there must be someone in the room).
///     Uses a global Windows hook to detect keyboard or mouse activity and send a message.
///     Based on this post: https://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C
/// </summary>
#pragma warning disable CA1724
public sealed class UserActivityMonitorService : IDisposable {
    private static readonly Lazy<UserActivityMonitorService> _lazy = new(() => new UserActivityMonitorService());

    // True while our activity handlers are attached to HookManager's static events. HookManager
    // installs the global WH_MOUSE_LL/WH_KEYBOARD_LL hooks on first subscribe and uninstalls them
    // when the last handler is removed, so attach/detach must stay symmetric (issue #197 — the old
    // GlobalEventProvider wrapper never detached, leaking the hooks and stacking duplicate handlers
    // on every Stop/Start cycle).
    private bool _inputEventsSubscribed;
    private IntPtr? _hAwayMode;
    private IntPtr? _hMonitorPower;

    private IntPtr? _hUserPresence;
    private DateTime _lastTime;
    private Timer _presencePresumedTimer = null!;

    // Captured on Start()'s thread (the WinForms UI thread in the GUI host). The heavy per-activity
    // work — log4net file I/O, a telemetry metric, and SendLine's synchronous socket/serial writes —
    // is Post()ed here so the low-level hook callback returns immediately (#198). Windows silently
    // evicts a WH_KEYBOARD_LL/WH_MOUSE_LL hook whose callback exceeds LowLevelHooksTimeout, and the
    // emergency-stop hotkey (#135) rides the same keyboard hook — a blocked serial write inside the
    // hook proc could kill the panic hotkey with no error surfaced.
    private SynchronizationContext? _activitySyncContext;

    private UserActivityMonitorService() {
    }

    public static UserActivityMonitorService Instance => _lazy.Value;
    public bool LogActivity { get; set; }
    public string ActivityMsg { get; set; } = "activity";
    public int DebounceTime { get; set; } = 5;
    public bool UnlockDetection { get; set; }
    public bool InputDetection { get; set; }

    // https://docs.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
    public bool PowerBroadcastDetection { get; set; }

    /// <summary>
    ///     Starts the Activity Monitor.
    /// </summary>
    public void Start() {
        _lastTime = DateTime.Now;
        _activitySyncContext = SynchronizationContext.Current;

        if (InputDetection) {
            Debug.Assert(!_inputEventsSubscribed);
            SubscribeToInputEvents();
        }


        if (UnlockDetection) {
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        if (PowerBroadcastDetection) {
            StartPowerBroadcastDetection();
        }

        // The Timer does get enabled until a user input is detected (See Activity() below).
        // No message is sent until user input is detected.
        StartPresencePresumedTimer();

        Logger.Instance.Log4.Info("ActivityMonitor: Start");
        Logger.Instance.Log4.Info($"ActivityMonitor: Keyboard/mouse input detection: {InputDetection}");
        Logger.Instance.Log4.Info($"ActivityMonitor: Desktop locked detection: {UnlockDetection}");
        Logger.Instance.Log4.Info($"ActivityMonitor: Power API User Presence Detection: {PowerBroadcastDetection}");
        Logger.Instance.Log4.Info($"ActivityMonitor: Command: {ActivityMsg}");
        Logger.Instance.Log4.Info($"ActivityMonitor: Debounce Time: {DebounceTime} seconds");

        // TELEMETRY: 
        // what: when activity monitoring is turned off
        // why: to understand how user to run activity monitoring on and off
        // how is PII protected: whether activity monitoring is on or off is not PII
        TelemetryService.Instance.TrackEvent("ActivityMonitor Start");
    }

    /// <summary>
    ///     Call back from SessionSwitch API; detects desktop locked/unlocked
    ///     There is no documented way to detect whether a session is unlocked.
    ///     (This does not seem to be very reliable).
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e) {
        if (!UnlockDetection) {
            return;
        }

        if (e.Reason == SessionSwitchReason.SessionLock) {
            // Desktop has been locked - Pretty good signal there's not going to be any activity
            // Stop the timer
            Logger.Instance.Log4.Info("ActivityMonitor: Session Locked");
            Debug.Assert(_presencePresumedTimer != null);
            _presencePresumedTimer.Enabled = false;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock) {
            // Desktop has been Unlocked - this is a signal there's activity. 
            // Start a repeating timer (using the same duration as debounce + 1 second) 
            Logger.Instance.Log4.Info("ActivityMonitor: Session Unlocked");
            ActivityPresumedTimerTick(null, null);
        }
    }


    internal void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam) {
        int pbt = wParam.ToInt32();

        // https://docs.microsoft.com/en-us/windows/win32/power/power-setting-guids
        switch (pbt) {
            case PBT_POWERSETTINGCHANGE:
                POWERBROADCAST_SETTING pbSetting =
                    (POWERBROADCAST_SETTING)Marshal.PtrToStructure(lParam, typeof(POWERBROADCAST_SETTING))!;
                if (pbSetting.PowerSetting == GUID_SESSION_USER_PRESENCE) {
                    // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                    switch (pbSetting.Data) {
                        case 0: // PowerUserPresent (0) - The user is providing input to the session.
                            Logger.Instance.Log4.Info(
                                "ActivityMonitor: PowerBroadcast: The user is providing input to the session.");
                            //this.Activity("PowerBroadcast: The user is providing input to the session");
                            break;


                        case 2
                            : // PowerUserInactive(2) - The user activity timeout has elapsed with no interaction from the user.
                            _presencePresumedTimer.Enabled = false;
                            Logger.Instance.Log4.Info(
                                "ActivityMonitor: PowerBroadcast: The user activity timeout has elapsed with no interaction from the user.");
                            break;

                        default:
                            Logger.Instance.Log4.Error(
                                $"ActivityMonitor: PowerBroadcast: Unknown GUID_SESSION_USER_PRESENCE data {pbSetting.Data}.");
                            break;
                    }
                }
                else if (pbSetting.PowerSetting == GUID_SYSTEM_AWAYMODE) {
                    // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                    switch (pbSetting.Data) {
                        case 0: // 0x0 - The computer is exiting away mode.
                            Logger.Instance.Log4.Info(
                                "ActivityMonitor: PowerBroadcast: The computer is exiting away mode.");
                            //this.Activity("PowerBroadcast: The computer is exiting away mode");
                            break;


                        case 1: // 0x1 - The computer is entering away mode.
                            _presencePresumedTimer.Enabled = false;
                            Logger.Instance.Log4.Info(
                                "ActivityMonitor: PowerBroadcast: The computer is entering away mode.");
                            break;

                        default:
                            Logger.Instance.Log4.Error(
                                $"ActivityMonitor: PowerBroadcast: Unknown GUID_SYSTEM_AWAYMODE data {pbSetting.Data}.");
                            break;
                    }
                }
                else if (pbSetting.PowerSetting == GUID_MONITOR_POWER_ON) {
                    // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                    switch (pbSetting.Data) {
                        case 0: // 0x0 - The monitor is off.
                            _presencePresumedTimer.Enabled = false;
                            Logger.Instance.Log4.Info("ActivityMonitor: PowerBroadcast: The monitor is off");
                            break;


                        case 1: // 0x1 - The monitor is on.
                            Logger.Instance.Log4.Info("ActivityMonitor: PowerBroadcast: The monitor is on");
                            //this.Activity("PowerBroadcast: The monitor is on.");
                            break;

                        default:
                            Logger.Instance.Log4.Error(
                                $"ActivityMonitor: PowerBroadcast: Unknown GUID_MONITOR_POWER_ON data {pbSetting.Data}.");
                            break;
                    }
                }

                break;

            default:
                Logger.Instance.Log4.Error($"ActivityMonitor: PowerBroadcast: Unknown PBT {pbt}.");
                break;
        }
    }

    private void StartPowerBroadcastDetection() {
        // #209: the handle comes from the AgentRuntime host seam, not MainWindow directly. In GUI
        // mode this is the MainWindow handle (whose WndProc forwards WM_POWERBROADCAST to
        // HandlePowerBroadcast); this service is only ever started by the GUI host, so headless the
        // seam throws with a pointed message rather than lazily constructing a Form.
        IntPtr messageWindowHandle = AgentRuntime.MessageWindowHandle;

        _hUserPresence = RegisterPowerSettingNotification(messageWindowHandle,
            ref GUID_SESSION_USER_PRESENCE,
            DEVICE_NOTIFY_WINDOW_HANDLE);

        _hAwayMode = RegisterPowerSettingNotification(messageWindowHandle,
            ref GUID_SYSTEM_AWAYMODE,
            DEVICE_NOTIFY_WINDOW_HANDLE);

        _hMonitorPower = RegisterPowerSettingNotification(messageWindowHandle,
            ref GUID_MONITOR_POWER_ON,
            DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    private void StopPowerBroadcastDetection() {
        if (_hUserPresence != null) {
            UnregisterPowerSettingNotification(_hUserPresence.Value);
            _hUserPresence = null;
        }

        if (_hAwayMode != null) {
            UnregisterPowerSettingNotification(_hAwayMode.Value);
            _hAwayMode = null;
        }

        if (_hMonitorPower != null) {
            UnregisterPowerSettingNotification(_hMonitorPower.Value);
            _hMonitorPower = null;
        }
    }

    /// <summary>
    ///     Starts the timer that sends an activity message every DebounceTime seconds if
    ///     user presence is presumed (either by session lock/unlock or powersettingchange).
    ///     Does NOT enable the timer.
    /// </summary>
    private void StartPresencePresumedTimer() {
        Debug.Assert(_presencePresumedTimer == null);
        _presencePresumedTimer = new Timer();
        _presencePresumedTimer.Tick += ActivityPresumedTimerTick;
        // #203: DebounceTime is in seconds everywhere else in this class; Timer.Interval is milliseconds.
        _presencePresumedTimer.Interval = DebounceTime * 1000;
    }

    private void StopPresencePresumedTimer() {
        Debug.Assert(_presencePresumedTimer != null);
        _presencePresumedTimer.Stop();
        _presencePresumedTimer.Dispose();
        _presencePresumedTimer = null!;
    }

    public void Stop() {
        Logger.Instance.Log4.Info("ActivityMonitor: Stop");
        // TELEMETRY: 
        // what: when activity monitoring is turned off
        // why: to understand how user to run activity monitoring on and off
        // how is PII protected: whether activity monitoring is on or off is not PII
        TelemetryService.Instance.TrackEvent("ActivityMonitor Stop");

        UnsubscribeFromInputEvents();

        if (_presencePresumedTimer != null) {
            StopPresencePresumedTimer();
        }

        if (UnlockDetection) {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        }

        if (PowerBroadcastDetection) {
            StopPowerBroadcastDetection();
        }
    }

    /// <summary>
    ///     Attaches the activity handlers directly to <see cref="HookManager" />'s static events (which
    ///     installs the global low-level hooks on first subscribe). Must stay symmetric with
    ///     <see cref="UnsubscribeFromInputEvents" /> — a handler attached here and not removed there keeps
    ///     the system-wide hook installed forever (issue #197).
    /// </summary>
    private void SubscribeToInputEvents() {
        if (_inputEventsSubscribed) {
            return;
        }

        // Note: HookManager has no KeyPress event. KeyDown suffices for activity detection, and the
        // vendored hook code's KeyPress/ToAscii path — the well-known Gma bug where ToAscii consumes
        // the keyboard's dead-key state and breaks accented-character composition system-wide while
        // the hook is installed (#198) — was deleted outright when the code became first-party (#214).
        HookManager.MouseMove += HookManager_MouseMove;
        HookManager.MouseClick += HookManager_MouseClick;
        HookManager.KeyDown += HookManager_KeyDown;
        HookManager.MouseDown += HookManager_MouseDown;
        HookManager.MouseUp += HookManager_MouseUp;
        HookManager.KeyUp += HookManager_KeyUp;
        HookManager.MouseDoubleClick += HookManager_MouseDoubleClick;
        _inputEventsSubscribed = true;
    }

    /// <summary>
    ///     Detaches every handler attached by <see cref="SubscribeToInputEvents" />. When these are the
    ///     last subscribers, <see cref="HookManager" /> uninstalls the global WH_MOUSE_LL/WH_KEYBOARD_LL
    ///     hooks (UnhookWindowsHookEx). Idempotent.
    /// </summary>
    private void UnsubscribeFromInputEvents() {
        if (!_inputEventsSubscribed) {
            return;
        }

        HookManager.MouseMove -= HookManager_MouseMove;
        HookManager.MouseClick -= HookManager_MouseClick;
        HookManager.KeyDown -= HookManager_KeyDown;
        HookManager.MouseDown -= HookManager_MouseDown;
        HookManager.MouseUp -= HookManager_MouseUp;
        HookManager.KeyUp -= HookManager_KeyUp;
        HookManager.MouseDoubleClick -= HookManager_MouseDoubleClick;
        _inputEventsSubscribed = false;
    }

    private void ActivityPresumedTimerTick(object? sender, EventArgs? e) {
        Activity($"{DebounceTime} seconds since user activity detected; User Presence Assumed");
    }

    /// <summary>
    ///     Called anytime user activity is detected — including from INSIDE the WH_KEYBOARD_LL /
    ///     WH_MOUSE_LL hook callbacks (the HookManager_* handlers below run in the hook proc, before
    ///     CallNextHookEx). It must stay cheap: Windows silently evicts a low-level hook whose callback
    ///     exceeds LowLevelHooksTimeout, and the emergency-stop hotkey (#135) rides the same keyboard
    ///     hook. Only the debounce check runs here; the heavy work (log file I/O, telemetry, SendLine's
    ///     synchronous socket/serial writes) is dispatched off the hook path by
    ///     <see cref="DispatchActivityWork" /> (#198).
    ///     Internal for testing (InternalsVisibleTo MCEControl.xUnit).
    /// </summary>
    /// <param name="source">Indicates the source of the detection; for logging</param>
    /// <param name="moreInfo">More info about the activity.</param>
    internal void Activity(string source, string moreInfo = "") {
        if (!_inputEventsSubscribed) {
            return;
        }

        // Enable user presence presumed timer
        if (_presencePresumedTimer != null) {
            _presencePresumedTimer.Enabled = true;
        }

        // Debounce HERE, on the hook callback path, so the dispatch below happens at most once per
        // DebounceTime — not on every mouse move.
        if (_lastTime.AddSeconds(DebounceTime) > DateTime.Now) {
            return;
        }
        _lastTime = DateTime.Now;

        DispatchActivityWork(source, moreInfo);
    }

    /// <summary>
    ///     Test seam (InternalsVisibleTo MCEControl.xUnit): when non-null,
    ///     <see cref="DispatchActivityWork" /> hands the heavy work here instead of posting it, so tests
    ///     can assert that the hook-path handler completes without running the work inline.
    /// </summary>
    internal Action<Action>? DispatchForTesting { get; set; }

    /// <summary>
    ///     Test seam (InternalsVisibleTo MCEControl.xUnit): the debounce clock — the time of the last
    ///     dispatched activity. Settable so tests can step past the debounce window deterministically.
    /// </summary>
    internal DateTime LastActivityTimeForTesting {
        get => _lastTime;
        set => _lastTime = value;
    }

    /// <summary>
    ///     Dispatches the heavy per-activity work so the hook callback returns immediately. Posts to
    ///     the SynchronizationContext captured at <see cref="Start" /> (the WinForms UI context in the
    ///     GUI host — the work runs as a normal posted message AFTER the hook proc has returned) and
    ///     falls back to the thread pool when there is none.
    /// </summary>
    private void DispatchActivityWork(string source, string moreInfo) {
        Action work = () => PerformActivityWork(source, moreInfo);

        Action<Action>? dispatchForTesting = DispatchForTesting;
        if (dispatchForTesting != null) {
            dispatchForTesting(work);
            return;
        }

        SynchronizationContext? context = _activitySyncContext;
        if (context != null) {
            context.Post(static state => ((Action)state!)(), work);
        }
        else {
            ThreadPool.QueueUserWorkItem(static state => ((Action)state!)(), work);
        }
    }

    /// <summary>
    ///     The heavy part of activity handling — log file I/O, a telemetry metric, and SendLine's
    ///     synchronous socket/serial writes. Runs OFF the hook callback path (see
    ///     <see cref="Activity" /> / <see cref="DispatchActivityWork" />).
    /// </summary>
    private void PerformActivityWork(string source, string moreInfo) {
        Logger.Instance.Log4.Info($@"ActivityMonitor: Activity detected: {source} {moreInfo}");

        // TELEMETRY:
        // what: the count of activity detected
        // why: to understand how frequently activity is detected
        // how is PII protected: the frequency of activity is not PII
        TelemetryService.Instance.TrackMetric("activity Sent", 1);

        // #209: outbound line goes through the AgentRuntime host seam (GUI: MainWindow.SendLine to
        // the connected transports; no host registered: logged drop — never an exception on this
        // background dispatch path).
        AgentRuntime.SendLine(ActivityMsg);
    }

    private void HookManager_KeyDown(object? sender, KeyEventArgs e) {
        Activity("KeyDown", LogActivity ? $"{e.KeyCode}" : "");
    }

    private void HookManager_KeyUp(object? sender, KeyEventArgs e) {
        Activity("KeyUp", LogActivity ? $"{e.KeyCode}" : "");
    }


    private void HookManager_MouseMove(object? sender, MouseEventArgs e) {
        Activity("MouseMove", LogActivity ? $"x={e.X:0000}; y={e.Y:0000}" : "");
    }

    private void HookManager_MouseClick(object? sender, MouseEventArgs e) {
        Activity("MouseClick", LogActivity ? $"{e.Button}" : "");
    }

    private void HookManager_MouseUp(object? sender, MouseEventArgs e) {
        Activity("MouseUp", LogActivity ? $"{e.Button}" : "");
    }

    private void HookManager_MouseDown(object? sender, MouseEventArgs e) {
        Activity("MouseDown", LogActivity ? $"{e.Button}" : "");
    }

    private void HookManager_MouseDoubleClick(object? sender, MouseEventArgs e) {
        Activity("MouseDoubleClick", LogActivity ? $"{e.Button}" : "");
    }

    #region IDisposable Support

    private bool _disposedValue; // To detect redundant calls

    void Dispose(bool disposing) {
        if (!_disposedValue) {
            if (disposing) {
                UnsubscribeFromInputEvents();
                _presencePresumedTimer?.Dispose();
                _presencePresumedTimer = null!;
            }

            _disposedValue = true;
        }
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        // TODO: uncomment the following line if the finalizer is overridden above.
        // GC.SuppressFinalize(this);
    }

    #endregion
}
