using System;
using System.Collections;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	/// <summary>
	/// Summary description for TokenGroups.
	/// </summary>
	public class TokenGroups : CollectionBase
	{
		internal TokenGroups(UnmanagedHeapAlloc ptr)
		{
			MemoryMarshaler m = new MemoryMarshaler(ptr.Ptr);
			TOKEN_GROUPS grps = (TOKEN_GROUPS)m.ParseStruct(typeof(TOKEN_GROUPS));
			for(int i = 0 ; i < grps.GroupCount; i++)
			{
				TokenGroup grp = new TokenGroup(m);
				base.InnerList.Add(grp);
			}
		}
		public TokenGroup this[int index]
		{
			get
			{
				return (TokenGroup)base.InnerList[index];
			}
		}
	}
}
