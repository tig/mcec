// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// A recording <see cref="IAppHost"/> for the #209 seam tests: captures <see cref="SendLine"/>
/// lines and <see cref="RequestShutdown"/> calls, and serves a settable fake message-window handle.
/// </summary>
internal sealed class RecordingAppHost : IAppHost {
    public List<string> Lines { get; } = [];
    public int ShutdownRequests { get; private set; }
    public IntPtr Handle { get; set; } = new(0x1234);

    public void SendLine(string line) => Lines.Add(line);
    public void RequestShutdown() => ShutdownRequests++;
    public IntPtr MessageWindowHandle => Handle;
}
