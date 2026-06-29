// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// Win32 <c>RECT</c> (screen coordinates) used by the agent native window calls
/// (<see cref="AgentNativeMethods.GetWindowRect"/>, DWM extended frame bounds, etc.).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NativeRect {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}
