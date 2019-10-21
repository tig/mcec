//-------------------------------------------------------------------
// Copyright © 2017 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
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
                AdjustToken(false);
            }
            _disposed = true;
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

        public static bool Hibernate() {
            return Application.SetSuspendState(PowerState.Hibernate, false, false);
        }

        public static void Shutdown(string msg, int timeout, bool forceAppsClosed, bool rebootAfterShutdown) {
            var n = Win32.InitiateSystemShutdown(null, msg, (uint)timeout, forceAppsClosed ? Win32.TRUE : Win32.FALSE,
                                                 rebootAfterShutdown ? Win32.TRUE : Win32.FALSE);
            Win32.CheckCall(n);
        }

        public static void Abort() {
            var n = Win32.AbortSystemShutdown(null);
            Win32.CheckCall(n);
        }
    }
}
