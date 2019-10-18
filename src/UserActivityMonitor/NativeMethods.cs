using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1502, CA1508
namespace Gma.UserActivityMonitor {
    // See https://docs.microsoft.com/en-us/visualstudio/code-quality/ca1060-move-p-invokes-to-nativemethods-class?view=vs-2019
    internal static class NativeMethods {
        // See https://stackoverflow.com/questions/17897646/setwindowshookex-fails-with-error-126
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);
    }
}
