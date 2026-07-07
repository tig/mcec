// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// P/Invoke surface for console attachment. mcec.exe is a WinExe, so it has no console unless the
/// parent hands it one; the CLI surface (Terminal.Gui.Cli: <c>--help</c>/<c>--version</c>/
/// <c>--opencli</c>) attaches to the parent's console so its output is visible when run from a
/// terminal. Kept as its own interop island.
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

    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private static readonly IntPtr _invalidHandleValue = new(-1);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    ///     Enables ANSI/VT escape-sequence interpretation on this process's console. A
    ///     console-subsystem app inherits a session whose output mode the shell/terminal already set
    ///     up, but a GUI-subsystem (WinExe) process that ATTACHES to a console does not get
    ///     ENABLE_VIRTUAL_TERMINAL_PROCESSING, so Terminal.Gui.Cli's rendered help/viewer ANSI would
    ///     print as literal <c>[39m</c>-style garbage (and the mangled lines wrap, wrecking the
    ///     layout). Call after <see cref="AttachConsole" />.
    ///     <para>
    ///     NOT via GetStdHandle: a GUI process's std-handle table is launcher-dependent and
    ///     AttachConsole does not reliably reinitialize it (GetStdHandle can return NULL right after
    ///     a successful attach). The attached console's active screen buffer is always reachable as
    ///     <c>CONOUT$</c>, and the output mode is a property of that BUFFER, not of any particular
    ///     handle, so setting VT there covers every writer. Best effort: with no console attached,
    ///     the <c>CONOUT$</c> open fails and this is a harmless no-op.
    ///     </para>
    /// </summary>
    internal static void TryEnableVtProcessing() {
        IntPtr conout = CreateFile("CONOUT$", GenericRead | GenericWrite, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (conout == IntPtr.Zero || conout == _invalidHandleValue) {
            return;
        }
        try {
            if (GetConsoleMode(conout, out uint mode)) {
                _ = SetConsoleMode(conout, mode | EnableVirtualTerminalProcessing);
            }
        }
        finally {
            _ = CloseHandle(conout);
        }
    }
}
