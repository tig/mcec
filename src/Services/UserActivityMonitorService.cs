//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using static Gma.UserActivityMonitor.NativeMethods;

namespace MCEControl {


    /// <summary>
    /// Singleton service  that monitors user activity and sends a message to a control system when detected. This enables utilizing
    /// a PC as a room occupancy sensor (e.g. if the PC is being used, there must be someone in the room).
    /// 
    /// Uses a global Windows hook to detect keyboard or mouse activity and send a message. 
    /// Based on this post: https://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C
    /// 
    /// </summary>
#pragma warning disable CA1724
    public sealed class UserActivityMonitorService : IDisposable {
        private System.DateTime LastTime;

        private static readonly Lazy<UserActivityMonitorService> lazy = new Lazy<UserActivityMonitorService>(() => new UserActivityMonitorService());
        private UserActivityMonitorService() {
        }

        private static Gma.UserActivityMonitor.GlobalEventProvider _userActivityMonitor = null;
        private Timer _PresencePresumedTimer = null;

        public static UserActivityMonitorService Instance => lazy.Value;
        public bool LogActivity { get; set; }
        public string ActivityMsg { get; set; } = "activity";
        public int DebounceTime { get; set; } = 5;
        public bool UnlockDetection { get; set; }
        public bool InputDetection { get; set; }

        // https://docs.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
        public bool PowerBroadcastDetection { get; set; }

        /// <summary>
        /// Starts the Activity Monitor. 
        /// </summary>
        /// <param name="debounceTime">Specifies the maximum frequency at which activity messages will be sent in seconds.</param>
        public void Start() {
            LastTime = DateTime.Now;

            if (InputDetection) {
                Debug.Assert(_userActivityMonitor == null);
                _userActivityMonitor = new Gma.UserActivityMonitor.GlobalEventProvider();
                _userActivityMonitor.MouseMove += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseMove);
                _userActivityMonitor.MouseClick += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseClick);
                _userActivityMonitor.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.HookManager_KeyPress);
                _userActivityMonitor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.HookManager_KeyDown);
                _userActivityMonitor.MouseDown += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseDown);
                _userActivityMonitor.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseUp);
                _userActivityMonitor.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HookManager_KeyUp);
                _userActivityMonitor.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseDoubleClick);
            }


            if (UnlockDetection) {
                Microsoft.Win32.SystemEvents.SessionSwitch += new Microsoft.Win32.SessionSwitchEventHandler(SystemEvents_SessionSwitch);
            }

            if (PowerBroadcastDetection) {
                StartPowerBroadcastDetection();
            }

            // The Timer does get enabled until a user input is detected (See Activity() below).
            // No message is sent until user input is detected.
            StartPresencePresumedTimer();

            Logger.Instance.Log4.Info($"ActivityMonitor: Start");
            Logger.Instance.Log4.Info($"ActivityMonitor: Keyboard/mouse input detection: {InputDetection}");
            Logger.Instance.Log4.Info($"ActivityMonitor: Desktop locked detection: {UnlockDetection}");
            Logger.Instance.Log4.Info($"ActivityMonitor: Power API User Presence Detection: {PowerBroadcastDetection}");
            Logger.Instance.Log4.Info($"ActivityMonitor: Command: {ActivityMsg}");
            Logger.Instance.Log4.Info($"ActivityMonitor: Debounce Time: {DebounceTime} seconds");

            // TELEMETRY: 
            // what: when activity montioring is turned off
            // why: to understand how user trun activity monitoring on and off
            // how is PII protected: whether activity monitoring is on or off is not PII
            TelemetryService.Instance.TrackEvent("ActivityMonitor Start");
        }

        /// <summary>
        /// Call back from SessionSwitch API; detects destkop locked/unlocked
        /// There is no documented way to detect whehter a session is unlocked.
        /// (This does not seem to be very reliable). 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e) {
            if (!UnlockDetection) return;

            if (e.Reason == SessionSwitchReason.SessionLock) {
                // Desktop has been locked - Pretty good signal there's not going to be any activity
                // Stop the timer
                Logger.Instance.Log4.Info($"ActivityMonitor: Session Locked");
                Debug.Assert(_PresencePresumedTimer != null);
                _PresencePresumedTimer.Enabled = false;
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock) {
                // Desktop has been Unlocked - this is a signal there's activity. 
                // Start a repeating timer (using the same duration as debounce + 1 second) 
                Logger.Instance.Log4.Info($"ActivityMonitor: Session Unlocked");
                ActivityPresumedTimerTick(null, null);
            }
        }


        internal void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam) {
            var pbt = wParam.ToInt32();

            // https://docs.microsoft.com/en-us/windows/win32/power/power-setting-guids
            switch (pbt) {
                case PBT_POWERSETTINGCHANGE:
                    POWERBROADCAST_SETTING pbSetting = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(lParam, typeof(POWERBROADCAST_SETTING));
                    if (pbSetting.PowerSetting == GUID_SESSION_USER_PRESENCE) {
                        // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                        switch (pbSetting.Data) {
                            case 0: // PowerUserPresent (0) - The user is providing input to the session.
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The user is providing input to the session.");
                                //this.Activity("PowerBroadcast: The user is providing input to the session");
                                break;


                            case 2: // PowerUserInactive(2) - The user activity timeout has elapsed with no interaction from the user.
                                _PresencePresumedTimer.Enabled = false;
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The user activity timeout has elapsed with no interaction from the user.");
                                break;

                            default:
                                Logger.Instance.Log4.Error($"ActivityMonitor: PowerBroadcast: Unknown GUID_SESSION_USER_PRESENCE data {pbSetting.Data}.");
                                break;
                        }
                    }
                    else if (pbSetting.PowerSetting == GUID_SYSTEM_AWAYMODE) {
                        // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                        switch (pbSetting.Data) {
                            case 0: // 0x0 - The computer is exiting away mode.
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The computer is exiting away mode.");
                                //this.Activity("PowerBroadcast: The computer is exiting away mode");
                                break;


                            case 1: // 0x1 - The computer is entering away mode.
                                _PresencePresumedTimer.Enabled = false;
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The computer is entering away mode.");
                                break;

                            default:
                                Logger.Instance.Log4.Error($"ActivityMonitor: PowerBroadcast: Unknown GUID_SYSTEM_AWAYMODE data {pbSetting.Data}.");
                                break;
                        }
                    }
                    else if (pbSetting.PowerSetting == GUID_MONITOR_POWER_ON) {
                        // BUGBUG: Data is just a byte and we're lucky the MSB has our value in it
                        switch (pbSetting.Data) {
                            case 0: // 0x0 - The monitor is off.
                                _PresencePresumedTimer.Enabled = false;
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The monitor is off");
                                break;


                            case 1: // 0x1 - The monitor is on.
                                Logger.Instance.Log4.Info($"ActivityMonitor: PowerBroadcast: The monitor is on");
                                //this.Activity("PowerBroadcast: The monitor is on.");
                                break;

                            default:
                                Logger.Instance.Log4.Error($"ActivityMonitor: PowerBroadcast: Unknown GUID_MONITOR_POWER_ON data {pbSetting.Data}.");
                                break;
                        }
                    }
                    break;

                default:
                    Logger.Instance.Log4.Error($"ActivityMonitor: PowerBroadcast: Unknown PBT {pbt}.");
                    break;
            }

        }

        private IntPtr _hUserPresence;
        private IntPtr _hAwayMode;
        private IntPtr _hMonitorPower;

        private void StartPowerBroadcastDetection() {
            _hUserPresence = RegisterPowerSettingNotification(MainWindow.Instance.Handle,
                    ref GUID_SESSION_USER_PRESENCE,
                    DEVICE_NOTIFY_WINDOW_HANDLE);

            _hAwayMode = RegisterPowerSettingNotification(MainWindow.Instance.Handle,
                    ref GUID_SYSTEM_AWAYMODE,
                    DEVICE_NOTIFY_WINDOW_HANDLE);

            _hMonitorPower = RegisterPowerSettingNotification(MainWindow.Instance.Handle,
                    ref GUID_MONITOR_POWER_ON,
                    DEVICE_NOTIFY_WINDOW_HANDLE);
        }

        private void StopPowerBroadcastDetection() {
            if (_hUserPresence != null) {
                UnregisterPowerSettingNotification(_hUserPresence);
                _hUserPresence = (IntPtr)null;
            }

            if (_hAwayMode != null) {
                UnregisterPowerSettingNotification(_hAwayMode);
                _hAwayMode = (IntPtr)null;
            }

            if (_hMonitorPower != null) {
                UnregisterPowerSettingNotification(_hMonitorPower);
                _hMonitorPower = (IntPtr)null;
            }
        }

        /// <summary>
        /// Starts the timer that sends an activity message every DebounceTime seconds if
        /// user presence is presumed (either by session lock/unlock or powersettingchange).
        /// Does NOT enable the timer.
        /// </summary>
        private void StartPresencePresumedTimer() {
            Debug.Assert(_PresencePresumedTimer == null);
            _PresencePresumedTimer = new Timer();
            _PresencePresumedTimer.Tick += ActivityPresumedTimerTick;
            _PresencePresumedTimer.Interval = this.DebounceTime;
        }

        private void StopPresencePresumedTimer() {
            Debug.Assert(_PresencePresumedTimer != null);
            _PresencePresumedTimer.Stop();
            _PresencePresumedTimer.Dispose();
            _PresencePresumedTimer = null;
        }

        public void Stop() {
            Logger.Instance.Log4.Info($"ActivityMonitor: Stop");
            // TELEMETRY: 
            // what: when activity montioring is turned off
            // why: to understand how user trun activity monitoring on and off
            // how is PII protected: whether activity monitoring is on or off is not PII
            TelemetryService.Instance.TrackEvent("ActivityMonitor Stop");

            if (_userActivityMonitor != null) {
                _userActivityMonitor.Dispose();
                _userActivityMonitor = null;
            }

            if (_PresencePresumedTimer != null) {
                StopPresencePresumedTimer();
            }

            if (UnlockDetection) {
                Microsoft.Win32.SystemEvents.SessionSwitch -= new Microsoft.Win32.SessionSwitchEventHandler(SystemEvents_SessionSwitch);
            }

            if (PowerBroadcastDetection) {
                StopPowerBroadcastDetection();
            }

        }

        private void ActivityPresumedTimerTick(object sender, EventArgs e) {
            this.Activity($"{DebounceTime} seconds since user activity detected; User Presence Assumed");
        }

        /// <summary>
        /// Called anytime user activity is detected.
        /// </summary>
        /// <param name="source">Indicates the source of the detection; for logging</param>
        /// <param name="moreInfo">More info about the activity.</param>
        private void Activity(string source, string moreInfo = "") {
            if (_userActivityMonitor == null) {
                return;
            }

            // Enable user presence presumed timer
            if (_PresencePresumedTimer != null) {
                _PresencePresumedTimer.Enabled = true;
            }

            if (LastTime.AddSeconds(DebounceTime) <= DateTime.Now) {
                // Only log/trigger if outside of debounce time
                Logger.Instance.Log4.Info($@"ActivityMonitor: Activity detected: {source} {moreInfo}");

                // TELEMETRY: 
                // what: the count of activity dectected
                // why: to understand how frequently activity is detected
                // how is PII protected: the frequency of activity is not PII
                TelemetryService.Instance.TelemetryClient.GetMetric($"activity Sent").TrackValue(1);

                MainWindow.Instance.SendLine(ActivityMsg);

                LastTime = DateTime.Now;
            }
        }

        private void HookManager_KeyDown(object sender, KeyEventArgs e) {
            Activity("KeyDown", LogActivity ? $"{e.KeyCode}" : "");
        }

        private void HookManager_KeyUp(object sender, KeyEventArgs e) {
            Activity("KeyUp", LogActivity ? $"{e.KeyCode}" : "");
        }


        private void HookManager_KeyPress(object sender, KeyPressEventArgs e) {
            Activity("KeyPress", LogActivity ? $"{e.KeyChar}" : "");
        }

        private void HookManager_MouseMove(object sender, MouseEventArgs e) {
            Activity($"MouseMove", LogActivity ? $"x={e.X:0000}; y={e.Y:0000}" : "");
        }

        private void HookManager_MouseClick(object sender, MouseEventArgs e) {
            Activity($"MouseClick", LogActivity ? $"{e.Button}" : "");
        }

        private void HookManager_MouseUp(object sender, MouseEventArgs e) {
            Activity($"MouseUp", LogActivity ? $"{e.Button}" : "");
        }

        private void HookManager_MouseDown(object sender, MouseEventArgs e) {
            Activity($"MouseDown", LogActivity ? $"{e.Button}" : "");
        }

        private void HookManager_MouseDoubleClick(object sender, MouseEventArgs e) {
            Activity($"MouseDoubleClick", LogActivity ? $"{e.Button}" : "");
        }

        private void HookManager_MouseWheel(object sender, MouseEventArgs e) {
            Activity($"MouseWheel", LogActivity ? $"{e.Delta:000}" : "");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    _PresencePresumedTimer?.Dispose();
                    _PresencePresumedTimer = null;
                }
                disposedValue = true;
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
}
