using System;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	using HANDLE = System.IntPtr;
	using DWORD = System.UInt32;
	using BOOL = System.Int32;
	using LPVOID = System.IntPtr;
	using PSID = System.IntPtr;

	/// <summary>
	/// Summary description for TokenGroup.
	/// </summary>
	public class TokenGroup
	{
		private Sid _sid;
		private GroupAttributes _attributes;

		internal TokenGroup(MemoryMarshaler m)
		{
			SID_AND_ATTRIBUTES sa = (SID_AND_ATTRIBUTES)m.ParseStruct(typeof(SID_AND_ATTRIBUTES));
			_sid = new Sid(sa.Sid);
			_attributes = (GroupAttributes)sa.Attributes;
		}

		public Sid Sid
		{
			get
			{
				return _sid;
			}
		}
		public GroupAttributes Attributes
		{
			get
			{
				return _attributes;
			}
		}
	}
}
