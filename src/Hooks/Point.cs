// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.InteropServices;

namespace MCEControl.Hooks;

/// <summary>
/// The Win32 POINT structure: the X- and Y-coordinates of a point (used inside
/// <see cref="MouseLLHookStruct"/>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Point {
    /// <summary>
    /// The X-coordinate of the point.
    /// </summary>
    public int X;
    /// <summary>
    /// The Y-coordinate of the point.
    /// </summary>
    public int Y;
}
