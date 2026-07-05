// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Runtime.InteropServices;

namespace MCEControl;

/// <summary>
/// Win32 <c>GUITHREADINFO</c>: a snapshot of one GUI thread's input state, filled by
/// <see cref="AgentNativeMethods.GetGUIThreadInfo"/>. The focus tool (#91, #270) reads
/// <see cref="HwndFocus"/> to VERIFY that keyboard focus actually landed in the target window after it
/// foregrounded and clicked it; a zero or foreign focus window is the detectable <c>focus</c> failure
/// the closed taxonomy reserves a category for. <see cref="Cb"/> must be set to the struct size before
/// the call or the API rejects it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GuiThreadInfo {
    public int Cb;
    public int Flags;
    public IntPtr HwndActive;
    public IntPtr HwndFocus;
    public IntPtr HwndCapture;
    public IntPtr HwndMenuOwner;
    public IntPtr HwndMoveSize;
    public IntPtr HwndCaret;
    public NativeRect RcCaret;
}
