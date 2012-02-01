using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	using DWORD = System.UInt32;
	using BOOL = System.Int32;

	/// <summary>
	///  Exception thrown when trying to attach to the token of a thread which
	///  is not impersonating.
	/// </summary>
	public class NoThreadTokenException : Exception
	{
		public NoThreadTokenException(string msg) :
			base(msg)
		{
		}
		public NoThreadTokenException(string msg, Exception innerException) :
			base(msg, innerException)
		{
		}
	}

	/// <summary>
	///  Encapsulation of a Win32 token handle.
	///  The object is disposable because it maintains the Win32 handle.
	/// </summary>
	public abstract class AccessToken : DisposableObject
	{
		/// The win32 token handle.
		private IntPtr _handle;

		public HandleRef Handle
		{
			get
			{
				return new HandleRef(this, _handle);
			}
		}
		protected internal AccessToken(IntPtr handle)
		{
			_handle = handle;
		}
		protected override void Dispose(bool disposing)
		{
			if (_handle != IntPtr.Zero)
			{
				// We don't want to throw an exception here, because there's not
				// much we can do when failing to close a handle.
				BOOL rc = Win32.CloseHandle(_handle);
				if (rc != Win32.FALSE)
					_handle = IntPtr.Zero;
			}
		}
		/// <summary>
		///  Generic call to Win32.GetTokenInformation. The returned object contains the 
		///  token information in unmanaged heap (UnmanagedHeapAlloc). 
		///  It must be disposed by the caller.
		/// </summary>
		/// <param name="TokenInformationClass">The type of info to retrieve</param>
		/// <returns>The ptr to the token information in unmanaged memory</returns>
		private UnmanagedHeapAlloc GetTokenInformation(TokenInformationClass TokenInformationClass)
		{
			DWORD cbLength;
			BOOL rc = Win32.GetTokenInformation(_handle, TokenInformationClass, IntPtr.Zero, 0, out cbLength);
			switch(Marshal.GetLastWin32Error())
			{
				case Win32.SUCCESS:
					throw new ArgumentException("Unexpected error code returned by GetTokenInformation");

				case Win32.ERROR_BAD_LENGTH:
					// Special case for TokenSessionId. Win32 doesn't want to return
					// us the size of a DWORD.
					cbLength = (DWORD)Marshal.SizeOf(typeof(DWORD));
					goto case Win32.ERROR_INSUFFICIENT_BUFFER;

				case Win32.ERROR_INSUFFICIENT_BUFFER:
					UnmanagedHeapAlloc res = new UnmanagedHeapAlloc(cbLength);
					try
					{
						rc = Win32.GetTokenInformation(_handle, TokenInformationClass, res.Ptr, res.Size, out cbLength);
						Win32.CheckCall(rc);
					}
					catch
					{
						res.Dispose();
						throw;
					}
					return res;

				default:
					Win32.ThrowLastError();
					return null; // uneeded
			}
		}
		public Dacl DefaultDacl
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenDefaultDacl))
				{
					TOKEN_DEFAULT_DACL acl = (TOKEN_DEFAULT_DACL)Marshal.PtrToStructure(ptr.Ptr, typeof(TOKEN_DEFAULT_DACL));
					return new Dacl(acl.DefaultDacl);
				}
			}
		}
		public TokenGroups Groups
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenGroups))
				{
					return new TokenGroups(ptr);
				}
			}
		}
		public TokenPrivileges Privileges
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenPrivileges))
				{
					return new TokenPrivileges(ptr);
				}
			}
		}
		public SecurityImpersonationLevel ImpersonationLevel
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenImpersonationLevel))
				{
					return (SecurityImpersonationLevel)Marshal.ReadInt32(ptr.Ptr);
				}
			}
		}
		public Sid Owner
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenOwner))
				{
					TOKEN_OWNER to = (TOKEN_OWNER)Marshal.PtrToStructure(ptr.Ptr, typeof(TOKEN_OWNER));
					return new Sid(to.Owner);
				}
			}
		}
		public Sid PrimaryGroup
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenPrimaryGroup))
				{
					TOKEN_PRIMARY_GROUP to = (TOKEN_PRIMARY_GROUP)Marshal.PtrToStructure(ptr.Ptr, typeof(TOKEN_PRIMARY_GROUP));
					return new Sid(to.PrimaryGroup);
				}
			}
		}
		public UInt32 TerminalServicesSessionId
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenSessionId))
				{
					return (UInt32)Marshal.ReadInt32(ptr.Ptr);
				}
			}
		}
		public TokenSource Source
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenSource))
				{
					return new TokenSource(ptr.Ptr);
				}
			}
		}
		public TokenStatistics Statistics
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenStatistics))
				{
					return new TokenStatistics(ptr.Ptr);
				}
			}
		}
		public TokenType TokenType
		{
			get 
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenType))
				{
					return (TokenType)Marshal.ReadInt32(ptr.Ptr);
				}
			}
		}
		public Sid User
		{
			get
			{
				using(UnmanagedHeapAlloc ptr = GetTokenInformation(TokenInformationClass.TokenUser))
				{
					TOKEN_USER to = (TOKEN_USER)Marshal.PtrToStructure(ptr.Ptr, typeof(TOKEN_USER));
					return new Sid(to.User.Sid);
				}
			}
		}
		public bool IsRestricted
		{
			get
			{
				Win32.SetLastError(Win32.SUCCESS);

				BOOL rc = Win32.IsTokenRestricted(_handle);

				if (Marshal.GetLastWin32Error() != Win32.SUCCESS)
					Win32.CheckCall(rc);
				return (rc != Win32.FALSE);
			}
		}
		/// <summary>
		/// Enable a single privilege on the process.
		/// </summary>
		/// <param name="privilege"></param>
		/// <exception cref="">Throws an exception if the privilege is not present
		///  in the privilege list of the process</exception>
		public void EnablePrivilege(TokenPrivilege privilege)
		{
			TokenPrivileges privs = new TokenPrivileges();
			privs.Add(privilege);
			EnableDisablePrivileges(privs);
		}
		private void EnableDisablePrivileges(TokenPrivileges privileges)
		{
			UnsafeEnableDisablePrivileges(privileges);
		}
		private unsafe void UnsafeEnableDisablePrivileges(TokenPrivileges privileges)
		{
			byte[] privBytes = privileges.GetNativeTOKEN_PRIVILEGES();
			fixed(byte *priv = privBytes)
			{
				UInt32 cbLength;

				Win32.SetLastError(Win32.SUCCESS);

				BOOL rc = Win32.AdjustTokenPrivileges(
					_handle,
					Win32.FALSE,
					(IntPtr)priv,
					0,
					IntPtr.Zero,
					out cbLength);
				Win32.CheckCall(rc);

				// Additional check: privilege can't be added, and in that case,
				// rc indicates a success, but GetLastError() has a specific meaning.
				if (Marshal.GetLastWin32Error() == Win32.ERROR_NOT_ALL_ASSIGNED)
					Win32.ThrowLastError();
			}
		}
	}

	/// <summary>
	///  Access token for a process
	/// </summary>
	public class AccessTokenProcess : AccessToken
	{
		private static IntPtr TryOpenProcessToken(int pid, TokenAccessType desiredAccess)
		{
			IntPtr processHandle = Win32.OpenProcess(
				ProcessAccessType.PROCESS_QUERY_INFORMATION,
				Win32.FALSE,
				(uint)pid);
			if (processHandle == IntPtr.Zero)
				return IntPtr.Zero;
			Win32.CheckCall(processHandle);
			try
			{
				IntPtr handle;
				BOOL rc = Win32.OpenProcessToken(processHandle, desiredAccess, out handle);
				if (rc == Win32.FALSE)
					return IntPtr.Zero;
				return handle;
			}
			finally
			{
				Win32.CloseHandle(processHandle);
			}
		}
		private static IntPtr OpenProcessToken(int pid, TokenAccessType desiredAccess)
		{
			IntPtr handle = TryOpenProcessToken(pid, desiredAccess);
			if (handle == IntPtr.Zero)
				Win32.ThrowLastError();
			return handle;
		}
		public static AccessTokenProcess TryOpenToken(int pid, TokenAccessType desiredAccess)
		{
			IntPtr handle = TryOpenProcessToken (pid, desiredAccess);
			if (handle != IntPtr.Zero)
				return new AccessTokenProcess(handle);
			return null;
		}
		private AccessTokenProcess(IntPtr handle)
			: base(handle)
		{
		}
		public AccessTokenProcess(int pid, TokenAccessType desiredAccess)
			: base(OpenProcessToken(pid, desiredAccess))
		{
		}
	}

	/// <summary>
	/// Access token for a thread
	/// </summary>
	public class AccessTokenThread : AccessToken
	{
		/// <summary>
		///  Return the token handle of a thread given its id.
		///  If the thread is not impersonating, return "null".
		/// </summary>
		/// <param name="threadId">The system-wide thread id</param>
		/// <param name="desiredAccess">The desired access to the token</param>
		/// <returns>The token handle or null if the thread is not impersonating</returns>
		private static IntPtr TryOpenThreadToken(int threadId, TokenAccessType desiredAccess)
		{
			IntPtr threadHandle = Win32.OpenThread(
				ThreadAccessType.THREAD_QUERY_INFORMATION,
				Win32.FALSE,
				(uint)threadId);
			if (threadHandle == IntPtr.Zero)
				return IntPtr.Zero;
			Win32.CheckCall(threadHandle);
			try
			{
				IntPtr handle;
				BOOL rc = Win32.OpenThreadToken(threadHandle, (uint)desiredAccess, Win32.FALSE, out handle);
				if (rc == Win32.FALSE)
					return IntPtr.Zero;
				return handle;
			}
			finally
			{
				Win32.CloseHandle(threadHandle);
			}
		}
		private static IntPtr OpenThreadToken(int threadId, TokenAccessType desiredAccess)
		{
			IntPtr hToken = TryOpenThreadToken(threadId, desiredAccess);
			if (hToken == IntPtr.Zero)
				throw new NoThreadTokenException("No token on thread " + threadId);
			return hToken;
		}
		/// <summary>
		///  Return "true" if the thread "tid" has a impersonation token
		/// </summary>
		/// <param name="tid">The system-wide thread id</param>
		public static bool HasToken(int tid)
		{
			IntPtr hToken = TryOpenThreadToken(tid, TokenAccessType.TOKEN_QUERY);
			bool rc = (hToken != IntPtr.Zero);
			Win32.CloseHandle(hToken);
			return rc;
		}

		public static AccessTokenThread TryOpenToken(int pid, TokenAccessType desiredAccess)
		{
			IntPtr handle = TryOpenThreadToken (pid, desiredAccess);
			if (handle != IntPtr.Zero)
				return new AccessTokenThread(handle);
			return null;
		}
		private AccessTokenThread(IntPtr handle)
			: base(handle)
		{
		}
		public AccessTokenThread(int threadId, TokenAccessType desiredAccess)
			: base(OpenThreadToken(threadId, desiredAccess))
		{
		}
	}
}
