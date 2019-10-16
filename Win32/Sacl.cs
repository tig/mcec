using System;

namespace Microsoft.Win32.Security
{
#pragma warning disable CA1710, CA1010
    /// <summary>
    /// Summary description for Sacl.
    /// </summary>
    public class Sacl : Acl
	{
		internal Sacl(IntPtr pacl) : base(pacl)
		{
		}
		public Sacl() : base()
		{
		}
		protected override void PrepareAcesForACL()
		{
			// We don't need to sort them for SACL
			return;
		}
	}
}
