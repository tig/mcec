//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using Microsoft.Win32.Security;

namespace MCEControl {
    /// <summary>
    /// Summary description for SystemControl.
    /// </summary>
    sealed public class SystemControl : IDisposable {
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

        public void AdjustToken(bool enable) {
            var p = Process.GetCurrentProcess();
            var at = new AccessTokenProcess(p.Id, TokenAccessType.TOKEN_ADJUST_PRIVILEGES);
            var tp = new TokenPrivilege(TokenPrivilege.SE_SHUTDOWN_NAME, enable);
            at.EnablePrivilege(tp);
        }

        public bool Standby() {
            return (Win32.SetSystemPowerState(Win32.TRUE, Win32.FALSE) == Win32.TRUE);
        }

        public bool Hibernate() {
            return (Win32.SetSystemPowerState(Win32.FALSE, Win32.FALSE) == Win32.TRUE);
        }

        public void Shutdown(string msg, uint timeout, bool forceAppsClosed, bool rebootAfterShutdown) {
            var n = Win32.InitiateSystemShutdown(null, msg, timeout, forceAppsClosed ? Win32.TRUE : Win32.FALSE,
                                                 rebootAfterShutdown ? Win32.TRUE : Win32.FALSE);
            Win32.CheckCall(n);
        }

        public void Abort() {
            var n = Win32.AbortSystemShutdown(null);
            Win32.CheckCall(n);
        }
    }
}