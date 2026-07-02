// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

// Identifiers mirror the Win32 WinTrust constant/parameter names (WTD_*, TRUST_E_*, pgActionID, pWVTData).
// ReSharper disable InconsistentNaming

namespace MCEControl;

/// <summary>
/// Verifies that a file carries a valid Authenticode signature from the expected publisher before the
/// auto-updater launches it (issue #146). Uses <c>WinVerifyTrust</c> to confirm the signature is
/// cryptographically valid AND chains to a trusted root (this also validates the PE hash, so a tampered
/// file fails even if it embeds a copied signature blob), then checks the signer certificate's subject
/// names the expected publisher.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "WinVerifyTrust is grouped with its own verifier, matching the repo's thematic Win32 grouping.")]
internal static class AuthenticodeVerifier {
    // WINTRUST_ACTION_GENERIC_VERIFY_V2
    private static readonly Guid _genericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const int TRUST_E_SUCCESS = 0;

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, [In] IntPtr pWVTData);

    /// <summary>
    /// True only if <paramref name="filePath"/> has a valid Authenticode signature that chains to a
    /// trusted root and whose signer certificate subject contains <paramref name="expectedSubjectSubstring"/>
    /// (case-insensitive). On any failure, <paramref name="reason"/> explains why and the method returns false.
    /// </summary>
    internal static bool Verify(string filePath, string expectedSubjectSubstring, out string reason) {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
            reason = $"file not found: {filePath}";
            return false;
        }
        if (!HasValidSignature(filePath, out int trustResult)) {
            reason = $"Authenticode signature missing or invalid (WinVerifyTrust=0x{trustResult:X8})";
            return false;
        }
        X509Certificate2? signer = TryGetSigner(filePath);
        if (signer is null) {
            reason = "could not read the signer certificate";
            return false;
        }
        using (signer) {
            // Anchor the publisher check to the certificate's simple name (the CN / subject common name),
            // where the publisher identity lives, rather than a substring anywhere in the full DN (which
            // could match an OU/serial/etc.). The Kindel Trusted Signing certificate's CN is "Kindel LLC".
            string commonName = signer.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (string.IsNullOrEmpty(commonName)
                || commonName.IndexOf(expectedSubjectSubstring, StringComparison.OrdinalIgnoreCase) < 0) {
                reason = $"signer '{signer.Subject}' does not match expected publisher '{expectedSubjectSubstring}'";
                return false;
            }
        }
        reason = "ok";
        return true;
    }

    /// <summary>Runs WinVerifyTrust (no UI) for GENERIC_VERIFY_V2. Returns true iff the result is S_OK.</summary>
    private static bool HasValidSignature(string filePath, out int trustResult) {
        WinTrustFileInfo fileInfo = new() {
            cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            pcwszFilePath = filePath,
            hFile = IntPtr.Zero,
            pgKnownSubject = IntPtr.Zero,
        };
        IntPtr pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        IntPtr pData = IntPtr.Zero;
        try {
            Marshal.StructureToPtr(fileInfo, pFileInfo, false);

            WinTrustData data = new() {
                cbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFileInfo,
                dwStateAction = WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0,
                dwUIContext = 0,
            };
            pData = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(data, pData, false);

            Guid action = _genericVerifyV2;
            trustResult = WinVerifyTrust(IntPtr.Zero, ref action, pData);

            // Always release the state data with a CLOSE call.
            WinTrustData closeData = Marshal.PtrToStructure<WinTrustData>(pData);
            closeData.dwStateAction = WTD_STATEACTION_CLOSE;
            Marshal.StructureToPtr(closeData, pData, false);
            _ = WinVerifyTrust(IntPtr.Zero, ref action, pData);

            return trustResult == TRUST_E_SUCCESS;
        }
        finally {
            if (pData != IntPtr.Zero) {
                Marshal.FreeHGlobal(pData);
            }
            // DestroyStructure first: StructureToPtr marshaled the LPWStr file path into a nested native
            // allocation that FreeHGlobal(pFileInfo) alone would not release.
            Marshal.DestroyStructure<WinTrustFileInfo>(pFileInfo);
            Marshal.FreeHGlobal(pFileInfo);
        }
    }

    private static X509Certificate2? TryGetSigner(string filePath) {
        try {
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is the supported way to read the Authenticode signer
            X509Certificate cert = X509Certificate.CreateFromSignedFile(filePath);
            return new X509Certificate2(cert);
#pragma warning restore SYSLIB0057
        }
        catch (Exception) {
            return null;
        }
    }
}
