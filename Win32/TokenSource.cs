using System;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	/// <summary>
	/// Summary description for TokenSource.
	/// </summary>
	public class TokenSource
	{
		private readonly string _name;
		private readonly Luid _luid;

		internal TokenSource(IntPtr ptr)
		{
			TOKEN_SOURCE ts = (TOKEN_SOURCE)Marshal.PtrToStructure(ptr, typeof(TOKEN_SOURCE));
			_name = new string(ts.Name);
			_luid = new Luid(ts.Indentifier);
		}

		public string Name
		{
			get
			{
				return _name;
			}
		}
		public Luid Luid
		{
			get
			{
				return _luid;
			}
		}
	}
}
