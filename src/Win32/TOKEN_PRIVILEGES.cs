using System;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.Security.Win32Structs;
#pragma warning disable CA1052, CA1707, CA2211, CA1714, CA1028, CA1008, CA1720, CA1724, CA1052, CA1051, CA1815, CS3003, IDE0044

using ACCESS_MASK = AccessMask;
using BOOL = System.Int32;
using BYTE = System.Byte;
using DWORD = System.UInt32;
using GUID = System.Guid;
using HANDLE = System.IntPtr;
using LARGE_INTEGER = System.Int64;
using LONG = System.Int32;
using LPWSTR = System.String;
using PACL = System.IntPtr;
using PSID = System.IntPtr;
using PVOID = System.IntPtr;
using UCHAR = System.Byte;
using WORD = System.UInt16;

[StructLayout(LayoutKind.Sequential)]
public struct TOKEN_PRIVILEGES {
    public DWORD PrivilegeCount;
    // Followed by this:
    //LUID_AND_ATTRIBUTES Privileges[ANYSIZE_ARRAY];
}
