// Uses a global Windows hook to detect keyboard or mouse activity
// Based on this post: https://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C
using System;
using System.Windows.Forms;

namespace MCEControl {

    public sealed class UserActivityMonitor {
        private bool logActivity = false;    // log mouse/keyboard events to MCEC window
        private int debounceTime = 5;       // Only send activity notification at most every DebounceTime seconds

        private System.DateTime LastTime;

        private static readonly Lazy<UserActivityMonitor> lazy = new Lazy<UserActivityMonitor>(() => new UserActivityMonitor());
        private UserActivityMonitor() {
        }

        private static Gma.UserActivityMonitor.GlobalEventProvider userActivityMonitor = null;

        public static UserActivityMonitor Instance => lazy.Value; public bool LogActivity { get => logActivity; set => logActivity = value; }
        public int DebounceTime {
            get => debounceTime; set {
                debounceTime = value;
            }
        }
        
        public void Start() {
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
        }

        public void Stop() {
            userActivityMonitor = null;
        }

        private void Activity() {
            if (LastTime.AddSeconds(DebounceTime) <= DateTime.Now) {
                MainWindow.AddLogEntry("MCEC: User Activity Dectected");
                if (MainWindow.Instance.Client != null) {
                    MainWindow.AddLogEntry("Client: Sending " + "activity");
                    MainWindow.Instance.Client.Send("activity\n");
                }
                LastTime = DateTime.Now;
            }
        }

        private void HookManager_KeyDown(object sender, KeyEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: KeyDown - {e.KeyCode}");
        }

        private void HookManager_KeyUp(object sender, KeyEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: KeyUp - {e.KeyCode}");
        }


        private void HookManager_KeyPress(object sender, KeyPressEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: KeyPress - {e.KeyChar}");
        }

        private void HookManager_MouseMove(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: MouseMove - x={e.X:0000}; y={e.Y:0000}");
        }

        private void HookManager_MouseClick(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: MouseClick - {e.Button}");
        }

        private void HookManager_MouseUp(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: MouseUp - {e.Button}");
        }

        private void HookManager_MouseDown(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: MouseDown - {e.Button}");
        }

        private void HookManager_MouseDoubleClick(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: MouseDoubleClick - {e.Button}");
        }

        private void HookManager_MouseWheel(object sender, MouseEventArgs e) {
            if (userActivityMonitor == null) return;
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry($"MCEC: Wheel={e.Delta:000}");
        }
    }
}
