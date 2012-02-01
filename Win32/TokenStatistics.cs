using System;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security
{
	using Win32Structs;

	/// <summary>
	/// Summary description for TokenStatistics.
	/// </summary>
	public class TokenStatistics
	{
		public Luid _tokenId;
		public Luid _authenticationId;
		public DateTime _expirationTime;
		public TokenType _tokenType;
		public SecurityImpersonationLevel _impersonationLevel;
		public UInt32 _dynamicCharged;
		public UInt32 _dynamicAvailable;
		public UInt32 _groupCount;
		public UInt32 _privilegeCount;
		public Luid _modifiedId;
		
		internal TokenStatistics(IntPtr ptr)
		{
			TOKEN_STATISTICS ts = (TOKEN_STATISTICS)Marshal.PtrToStructure(ptr, typeof(TOKEN_STATISTICS));
			_tokenId = new Luid(ts.TokenId);
			_authenticationId = new Luid(ts.AuthenticationId);
			_expirationTime = new DateTime(ts.ExpirationTime);
			_tokenType = ts.TokenType;
			_impersonationLevel = ts.ImpersonationLevel;
			_dynamicCharged = ts.DynamicCharged;
			_dynamicAvailable = ts.DynamicAvailable;
			_groupCount = ts.GroupCount;
			_privilegeCount = ts.PrivilegeCount;
			_modifiedId = new Luid(ts.ModifiedId);
		}
	}
}
