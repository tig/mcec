using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security;
#pragma warning disable CA1062
using Win32Structs;
/// <summary>
///  An access denied ACE
/// </summary>
public class AceAccessDenied : AceAccess {
    internal AceAccessDenied(MemoryMarshaler m) : base(m, false) {
    }
    public AceAccessDenied(Sid sid, AccessType accessType, AceFlags flags)
        : base(sid, flags, accessType, false) {
    }
    public AceAccessDenied(Sid sid, AccessType accessType)
        : base(sid, 0, accessType, false) {
    }
}
