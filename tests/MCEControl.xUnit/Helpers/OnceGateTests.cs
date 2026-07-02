// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Linq;
using System.Threading.Tasks;
using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Helpers;

/// <summary>
/// Tests for <see cref="OnceGate"/>, the idempotence latch behind
/// <c>MainWindow.PerformShutdown()</c> (#213): menu exit runs the teardown and then Close()
/// re-enters it via FormClosing, while OS logoff enters via FormClosing alone; the gate
/// guarantees the teardown body runs exactly once whichever path (or both) fires.
/// </summary>
public class OnceGateTests {
    [Fact]
    public void TryEnter_FirstCallWins_SubsequentCallsRefused() {
        var gate = new OnceGate();

        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());
        Assert.False(gate.TryEnter());
    }

    [Fact]
    public async Task TryEnter_ConcurrentCallers_ExactlyOneWins() {
        var gate = new OnceGate();

        bool[] results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(gate.TryEnter)));

        Assert.Equal(1, results.Count(r => r));
    }
}
