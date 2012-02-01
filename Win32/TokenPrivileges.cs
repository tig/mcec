using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	/// <summary>
	/// Summary description for TokenPrivileges.
	/// </summary>
	public class TokenPrivileges : CollectionBase
	{
		internal TokenPrivileges(UnmanagedHeapAlloc ptr)
		{
			MemoryMarshaler m = new MemoryMarshaler(ptr.Ptr);
			TOKEN_PRIVILEGES privs = (TOKEN_PRIVILEGES)m.ParseStruct(typeof(TOKEN_PRIVILEGES));
			for(int i = 0 ; i < privs.PrivilegeCount; i++)
			{
				TokenPrivilege priv = new TokenPrivilege(m);
				base.InnerList.Add(priv);
			}
		}
		public TokenPrivileges()
		{
		}
		public TokenPrivilege this[int index]
		{
			get
			{
				return (TokenPrivilege)base.InnerList[index];
			}
		}
		public void Add(TokenPrivilege privilege)
		{
			base.InnerList.Add(privilege);
		}
		public unsafe byte[] GetNativeTOKEN_PRIVILEGES()
		{
			Debug.Assert(Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)) == 4);

			TOKEN_PRIVILEGES tp;
			tp.PrivilegeCount = (uint)this.Count;

			int cbLength = 
				Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)) +
				Marshal.SizeOf(typeof(LUID_AND_ATTRIBUTES)) * this.Count;
			byte []res = new byte[cbLength];
			fixed(byte *privs = res)
			{
				Marshal.StructureToPtr(tp, (IntPtr)privs, false);
			}

			int resOffset = Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)); 
			for(int i = 0; i < this.Count; i++)
			{
				byte[] luida = this[i].GetNativeLUID_AND_ATTRIBUTES();
				Array.Copy(luida, 0, res, resOffset, luida.Length);
				resOffset += luida.Length;
			}
			return res;
		}
	}
}
