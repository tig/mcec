// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// Native <c>WINTRUST_FILE_INFO</c> — identifies the file whose Authenticode signature
/// <see cref="AuthenticodeVerifier"/> asks <c>WinVerifyTrust</c> to validate (issue #146).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WinTrustFileInfo {
    public uint cbStruct;
    [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
    public IntPtr hFile;
    public IntPtr pgKnownSubject;
}
