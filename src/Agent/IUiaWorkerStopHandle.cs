// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Shutdown-only surface for the UIA worker (#317). Kept free of FlaUI types so
/// <see cref="UiaService.Shutdown"/> never JIT-loads FlaUI.UIA3 when the worker never started.
/// </summary>
internal interface IUiaWorkerStopHandle {
    void Stop(int joinTimeoutMs);
}