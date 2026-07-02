// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Threading;

namespace MCEControl;

/// <summary>
/// A latch for one-shot operations (#213): the first <see cref="TryEnter"/> returns true, every
/// later call returns false. Used by <c>MainWindow.PerformShutdown()</c> so the two exit paths
/// (menu exit calls it directly, then <c>Close()</c> re-enters via FormClosing; OS logoff enters
/// via FormClosing alone) converge on exactly one teardown. Thread-safe (Interlocked) so a
/// re-entry from another thread can never double-run the teardown.
/// </summary>
internal sealed class OnceGate {
    private int _entered;

    /// <summary>Returns true exactly once (the first call); false forever after.</summary>
    public bool TryEnter() => Interlocked.Exchange(ref _entered, 1) == 0;
}
