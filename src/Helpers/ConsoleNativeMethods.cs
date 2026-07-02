// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// P/Invoke surface for console attachment. mcec.exe is a WinExe, so it has no console unless the
/// parent hands it one; the CLI surface (Terminal.Gui.Cli: <c>--help</c>/<c>--version</c>/
/// <c>--opencli</c>) attaches to the parent's console so its output is visible when run from a
/// terminal. Kept as its own interop island (see ARCHITECTURE.md §6).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
    Justification = "Console P/Invokes are grouped thematically, matching the repo's existing Win32 grouping.")]
internal static class ConsoleNativeMethods {
    /// <summary>The pseudo process id selecting the parent process's console.</summary>
    internal const int AttachParentProcess = -1;

    /// <summary>
    /// Attaches to the given process's console. Fails harmlessly (returns false) when the parent has
    /// no console or the standard handles are already piped; callers treat it as best effort.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);
}
