// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl.xUnit.Integration;

/// <summary>
/// Minimal native helpers for the opt-in desktop E2E test: the physical primary-display size used to
/// normalize pixel coordinates (from MCEC's UIA query) into InputSimulator's 0..65535 mouse space.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "Test-only P/Invoke helper, grouped with its single caller.")]
internal static class DesktopE2ENative {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
}
