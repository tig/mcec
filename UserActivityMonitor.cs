using System;
using System.Windows.Forms;

namespace MCEControl
{
    public sealed class UserActivityMonitor
    {
        private bool logActivity = false;    // log mouse/keyboard events to MCEC window
        private int debounceTime = 10;       // Only send activity notification at most every DebounceTime seconds

        private System.DateTime LastTime;

        private static readonly Lazy<UserActivityMonitor> lazy = new Lazy<UserActivityMonitor>(() => new UserActivityMonitor());

        public static UserActivityMonitor Instance { get { return lazy.Value; } }

        public bool LogActivity { get => logActivity; set => logActivity = value; }
        public int DebounceTime { get => debounceTime; set => debounceTime = value; }

        private UserActivityMonitor()
        {
        }

        // Global Mouse/Keyboard Activity Monitor
        private static Gma.UserActivityMonitor.GlobalEventProvider _userActivityMonitor = null;

        public void Start()
        {
            if (_userActivityMonitor != null)
                _userActivityMonitor = null;

            LastTime = DateTime.Now;

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

        public static void Stop()
        {
            _userActivityMonitor = null;
        }

        private void Activity()
        {
            if (LastTime.AddSeconds(DebounceTime) <= DateTime.Now)
            {
                MainWindow.AddLogEntry("MCEC: User Activity Dectected");
                MainWindow.AddLogEntry("Client: Sending Command: activity");
                if (MainWindow.Instance.Client != null)
                {
                    MainWindow.AddLogEntry("Client: Sending " + "event1");
                    MainWindow.Instance.Client.Send("event1\n");
                }
                LastTime = DateTime.Now;
            }
        }

        private void HookManager_KeyDown(object sender, KeyEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: KeyDown - {0}", e.KeyCode));
        }

        private void HookManager_KeyUp(object sender, KeyEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: KeyUp - {0}", e.KeyCode));
        }


        private void HookManager_KeyPress(object sender, KeyPressEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: KeyPress - {0}", e.KeyChar));
        }

        private void HookManager_MouseMove(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: KeyPress - x={0:0000}; y={1:0000}", e.X, e.Y));
        }

        private void HookManager_MouseClick(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: MouseClick - {0}", e.Button));
        }

        private void HookManager_MouseUp(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: MouseUp - {0}", e.Button));
        }

        private void HookManager_MouseDown(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: MouseDown - {0}", e.Button));
        }

        private void HookManager_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: MouseDoubleClick - {0}", e.Button));
        }

        private void HookManager_MouseWheel(object sender, MouseEventArgs e)
        {
            Activity();
            if (LogActivity)
                MainWindow.AddLogEntry(string.Format("MCEC: Wheel={0:000}", e.Delta));
        }
    }
}
