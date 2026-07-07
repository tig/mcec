// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using FlaUI.UIA3;

namespace MCEControl;

/// <summary>
/// Process-wide accessor for the lazy UIA worker. Fields exposed to <see cref="UiaService"/> use only
/// <see cref="IUiaWorkerStopHandle"/> (and <c>object</c> for the enqueue host) so shutdown never loads FlaUI (#317).
/// </summary>
internal static class UiaWorkerAccess {
    private static readonly Lock _gate = new();
    private static IUiaWorkerStopHandle? _stop;
    private static object? _host;
    private static int _generation;

    internal static int Generation {
        get {
            lock (_gate) {
                return _generation;
            }
        }
    }

    internal static void Enqueue(Action<UIA3Automation> work) {
        lock (_gate) {
            if (_host is null) {
                UiaWorkerHost created = new();
                _host = created;
                _stop = created;
                _generation++;
            }
            ((UiaWorkerHost)_host).Enqueue(work);
        }
    }

    internal static IUiaWorkerStopHandle? TakeStopHandle() {
        lock (_gate) {
            IUiaWorkerStopHandle? stop = _stop;
            _stop = null;
            _host = null;
            return stop;
        }
    }
}