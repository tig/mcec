using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security;
#pragma warning disable CA1062
using Win32Structs;
/// <summary>
///  An access allowed ACE
/// </summary>
public class AceAccessAllowed : AceAccess {
    /// <summary>
    ///  Internal: Create an ACE from a given memory marshaler
    /// </summary>
    internal AceAccessAllowed(MemoryMarshaler m)
        : base(m, true) {
    }
    /// <summary>
    ///  Create a new Ace given a Sid, an access type and an set of flags
    /// </summary>
    /// <param name="sid">The sid (must be valid)</param>
    /// <param name="accessMask">The access accessMask</param>
    /// <param name="flags">The list of flags</param>
    public AceAccessAllowed(Sid sid, AccessType accessType, AceFlags flags)
        : base(sid, flags, accessType, true) {
    }
    /// <summary>
    ///  Create a new Ace given a Sid, an access type and a default set of flags
    /// </summary>
    /// <param name="sid">The sid (must be valid)</param>
    /// <param name="accessMask">The access accessMask</param>
    public AceAccessAllowed(Sid sid, AccessType accessType)
        : base(sid, 0, accessType, true) {
    }
}
