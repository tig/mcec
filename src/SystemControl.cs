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
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32.Security;

namespace MCEControl {
    /// <summary>
    /// Summary description for SystemControl.
    /// </summary>
    public sealed class SystemControl : IDisposable {
        private bool _disposed;

        public SystemControl() {
            AdjustToken(true);
        }

        #region IDisposable Members

        public void Dispose() {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        #endregion

        ~SystemControl() {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            Dispose(false);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        private void Dispose(bool disposing) {
            // Check to see if Dispose has already been called.
            if (!_disposed && disposing) {
                AdjustToken(true);
            }
            _disposed = true;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "FormatMessageA", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int FormatMessage(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, int Arguments);
        private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        /// <summary>
        /// Formats an error number into an error message.
        /// </summary>
        /// <param name="number">The error number to convert.</param>
        /// <returns>A string representation of the specified error number.</returns>
        protected static string FormatError(int number) {
            try {
                StringBuilder buffer = new StringBuilder(255);
#pragma warning disable CA1806 // Do not ignore method results
                FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, number, 0, buffer, buffer.Capacity, 0);
#pragma warning restore CA1806 // Do not ignore method results
                return buffer.ToString();
            }
            catch (Exception) {
                return $"Unspecified error [{number}]";
            }
        }

        public static void AdjustToken(bool enable) {
            var p = Process.GetCurrentProcess();
            var at = new AccessTokenProcess(p.Id, TokenAccessType.TOKEN_ADJUST_PRIVILEGES);
            var tp = new TokenPrivilege(TokenPrivilege.SE_SHUTDOWN_NAME, enable);
            at.EnablePrivilege(tp);
            at.Dispose();
        }

        public static bool Standby() {
            return Application.SetSuspendState(PowerState.Suspend, true, false);
        }

        public static bool Hibernate(string msg, int timeout, bool forceAppsClosed, bool rebootAfterShutdown) {
            return false;
        }

        public static void Shutdown(string msg, int timeout, bool forceAppsClosed, bool rebootAfterShutdown, string option = "") {
            string force = forceAppsClosed ? "/f" : "";
            string reboot = rebootAfterShutdown ? "/r" : "/s";
            msg = msg is null ? "" : msg;
            var proc = System.Diagnostics.Process.Start("ShutDown", $"{reboot} /t {timeout} {force} /c \"{msg}\"");
            proc.WaitForExit(1000);
            if (proc.ExitCode != 0x0)
                throw new System.ComponentModel.Win32Exception(proc.ExitCode);
        }

        public static void Abort() {
            var proc = System.Diagnostics.Process.Start("ShutDown", "/a");
            proc.WaitForExit(1000);
            if (proc.ExitCode != 0x0)
                throw new System.ComponentModel.Win32Exception(proc.ExitCode);
        }
    }
}
