// Uses a global Windows hook to detect keyboard or mouse activity
// Based on this post: https://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C
using System;
using System.Windows.Forms;

namespace MCEControl {

    public sealed class UserActivityMonitor {
        private bool logActivity = false;    // log mouse/keyboard events to MCEC window
        private uint debounceTime = 5;       // Only send activity notification at most every DebounceTime seconds
        private string activityCmd = "activity";

        private System.DateTime LastTime;

        private static readonly Lazy<UserActivityMonitor> lazy = new Lazy<UserActivityMonitor>(() => new UserActivityMonitor());
        private UserActivityMonitor() {
        }

        private static Gma.UserActivityMonitor.GlobalEventProvider userActivityMonitor = null;
        
        public static UserActivityMonitor Instance => lazy.Value; public bool LogActivity { get => logActivity; set => logActivity = value; }
           
        public void Start(string cmd, uint DebounceTime) {
            if (userActivityMonitor != null)
                userActivityMonitor = null;

            LastTime = DateTime.Now;

            userActivityMonitor = new Gma.UserActivityMonitor.GlobalEventProvider();
            userActivityMonitor.MouseMove += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseMove);
            userActivityMonitor.MouseClick += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseClick);
            userActivityMonitor.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.HookManager_KeyPress);
            userActivityMonitor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.HookManager_KeyDown);
            userActivityMonitor.MouseDown += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseDown);
            userActivityMonitor.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseUp);
            userActivityMonitor.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HookManager_KeyUp);
            userActivityMonitor.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.HookManager_MouseDoubleClick);

            debounceTime = DebounceTime;
        }

        public void Stop() {
            userActivityMonitor = null;
        }

        private void Activity(string logInfo) {
            if (userActivityMonitor == null) return;

            if (LogActivity)
                Logger.Instance.Log4.Info($"ActivityMonitor: {logInfo}");

            if (LastTime.AddSeconds(debounceTime) <= DateTime.Now) {
                Logger.Instance.Log4.Info("ActivityMonitor: User Activity Dectected");
                if (MainWindow.Instance.Client != null) {
                    Logger.Instance.Log4.Info("ActivityMonitor: Sending " + activityCmd);
                    MainWindow.Instance.SendLine(activityCmd);
                }
                LastTime = DateTime.Now;
            }
        }

        private void HookManager_KeyDown(object sender, KeyEventArgs e) {
            Activity($"KeyDown - {e.KeyCode}");
        }

        private void HookManager_KeyUp(object sender, KeyEventArgs e) {
            Activity($"KeyUp - {e.KeyCode}");
        }


        private void HookManager_KeyPress(object sender, KeyPressEventArgs e) {
            Activity($"KeyPress - {e.KeyChar}");
        }

        private void HookManager_MouseMove(object sender, MouseEventArgs e) {
            Activity($"MouseMove - x={e.X:0000}; y={e.Y:0000}");
        }

        private void HookManager_MouseClick(object sender, MouseEventArgs e) {
            Activity($"MouseClick - {e.Button}");
        }

        private void HookManager_MouseUp(object sender, MouseEventArgs e) {
            Activity($"MouseUp - {e.Button}");
        }

        private void HookManager_MouseDown(object sender, MouseEventArgs e) {
            Activity($"MouseDown - {e.Button}");
        }

        private void HookManager_MouseDoubleClick(object sender, MouseEventArgs e) {
            Activity($"MouseDoubleClick - {e.Button}");
        }

        private void HookManager_MouseWheel(object sender, MouseEventArgs e) {
            Activity($"Wheel={e.Delta:000}");
        }
    }
}
