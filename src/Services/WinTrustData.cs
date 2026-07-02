// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// Native <c>WINTRUST_DATA</c> passed to <c>WinVerifyTrust</c> by <see cref="AuthenticodeVerifier"/>
/// (issue #146). Only the file-choice union member is used, so the union is modeled as a single
/// pointer to a <see cref="WinTrustFileInfo"/>. The newer <c>pSignatureSettings</c> tail field is
/// intentionally omitted; <c>cbStruct</c> is set to this (older) size, which Windows accepts.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WinTrustData {
    public uint cbStruct;
    public IntPtr pPolicyCallbackData;
    public IntPtr pSIPClientData;
    public uint dwUIChoice;
    public uint fdwRevocationChecks;
    public uint dwUnionChoice;
    public IntPtr pFile; // union: WINTRUST_FILE_INFO* (we only use WTD_CHOICE_FILE)
    public uint dwStateAction;
    public IntPtr hWVTStateData;
    public IntPtr pwszURLReference;
    public uint dwProvFlags;
    public uint dwUIContext;
}
