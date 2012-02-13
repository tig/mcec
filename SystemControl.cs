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
using Microsoft.Win32.Security;
using System.Diagnostics;

namespace MCEControl
{
	/// <summary>
	/// Summary description for SystemControl.
	/// </summary>
	public class SystemControl : IDisposable
	{
		private bool disposed = false;

		public SystemControl()
		{
			AdjustToken(true);
		}

		~SystemControl()      
		{
			// Do not re-create Dispose clean-up code here.
			// Calling Dispose(false) is optimal in terms of
			// readability and maintainability.
			Dispose(false);
		}


		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		public void Dispose( bool disposing )
		{
			// Check to see if Dispose has already been called.
			if(!this.disposed)
			{
				// If disposing equals true, dispose all managed 
				// and unmanaged resources.
				if( disposing )
					AdjustToken(false);
			}
			disposed = true;
		}

		public void AdjustToken(bool enable)
		{
			Process p = Process.GetCurrentProcess();
			AccessTokenProcess at = new AccessTokenProcess(p.Id, Microsoft.Win32.Security.TokenAccessType.TOKEN_ADJUST_PRIVILEGES);

			TokenPrivilege tp = new TokenPrivilege(TokenPrivilege.SE_SHUTDOWN_NAME, enable);
			at.EnablePrivilege(tp);
		}

		public bool Standby()
		{
			unsafe
			{
				return (Win32.SetSystemPowerState( Win32.TRUE, Win32.FALSE) == Win32.TRUE);
			}
		}

		public bool Hibernate()
		{
			unsafe
			{
				return (Win32.SetSystemPowerState( Win32.FALSE, Win32.FALSE) == Win32.TRUE);
			}
		}

		public void Shutdown(string msg, uint timeout, bool forceAppsClosed, bool rebootAfterShutdown)
		{
			unsafe
			{
				int n = Win32.InitiateSystemShutdown(null, msg, timeout, forceAppsClosed ? Win32.TRUE : Win32.FALSE, rebootAfterShutdown ? Win32.TRUE : Win32.FALSE);
				Win32.CheckCall(n);
			}
		}

		public void Abort()
		{
			unsafe
			{
				int n = Win32.AbortSystemShutdown(null);
				Win32.CheckCall(n);
			}
		}
		#region IDisposable Members

		 void System.IDisposable.Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue 
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		#endregion
	}
}
