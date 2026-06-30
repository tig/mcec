// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>Callback for <see cref="AgentNativeMethods.EnumWindows"/>.</summary>
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
