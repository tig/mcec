// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// UiaService guard-path and #215 worker-lifecycle tests. Joins the serial agent collection because
/// the worker (and its cached UIA3Automation) is process-global state that <see cref="UiaService.Shutdown"/>
/// tears down mid-test.
/// </summary>
[Collection("AgentSerial")]
public class UiaServiceTests {
    [Theory]
    [InlineData("invoke")]
    [InlineData("Invoke")]   // case-insensitive
    [InlineData("toggle")]
    [InlineData("setvalue")]
    [InlineData("setfocus")]
    public void IsSupportedAction_KnownActions_ReturnsTrue(string action) {
        Assert.True(UiaService.IsSupportedAction(action));
    }

    [Theory]
    [InlineData("click")]       // CR P2: a coordinate-click typo must not fall through to Invoke
    [InlineData("set-value")]   // typo of setvalue
    [InlineData("")]
    [InlineData("foo")]
    public void IsSupportedAction_UnknownActions_ReturnsFalse(string action) {
        Assert.False(UiaService.IsSupportedAction(action));
    }

    [Fact]
    public void Invoke_ZeroHandle_ReturnsElementNotFound() {
        // #206: the guard paths return distinct categorical outcomes, not a conflating bool.
        UiaInvokeResult result = UiaService.Invoke(IntPtr.Zero, "name", "OK", "invoke", null);

        Assert.Equal(UiaInvokeResult.ElementNotFound, result);
    }

    [Theory]
    [InlineData("click")]
    [InlineData("set-value")]
    [InlineData("")]
    public void Invoke_UnknownAction_ReturnsActionUnknown_WithoutTouchingUia(string action) {
        // An unsupported action is rejected before any UIA attach — even a bogus handle never gets
        // that far — and reports ActionUnknown (fix the argument), not a not-found/pattern failure.
        UiaInvokeResult result = UiaService.Invoke(new IntPtr(0x1), "name", "OK", action, null);

        Assert.Equal(UiaInvokeResult.ActionUnknown, result);
    }

    [Fact]
    public void DumpTree_ZeroHandle_ReturnsEmptyUntruncatedResult() {
        // The guard path must return a well-formed result (null root, no nodes, not truncated) rather
        // than null, so query can always read nodeCount/truncated.
        UiaTreeResult result = UiaService.DumpTree(IntPtr.Zero, maxDepth: 6, maxNodes: 1000);

        Assert.NotNull(result);
        Assert.Null(result.Root);
        Assert.Equal(0, result.NodeCount);
        Assert.False(result.Truncated);
    }

    // --- #215: all UIA work funnels through ONE dedicated MTA worker with one cached UIA3Automation.

    [Fact]
    public void Worker_ThreadIdentityAndCachedAutomation_AreStableAcrossCalls() {
        (int thread1, int automation1, int gen1) = UiaService.ProbeWorker();
        (int thread2, int automation2, int gen2) = UiaService.ProbeWorker();

        Assert.Equal(thread1, thread2);           // one dedicated worker thread, reused
        Assert.Equal(automation1, automation2);   // one cached UIA3Automation, not per-call construction
        Assert.Equal(gen1, gen2);                 // no silent worker churn between calls
        Assert.NotEqual(Environment.CurrentManagedThreadId, thread1); // never the caller's thread
    }

    [Fact]
    public async System.Threading.Tasks.Task Worker_CallsFromDifferentThreads_LandOnTheSameWorker() {
        (int fromHere, _, _) = UiaService.ProbeWorker();
        (int fromElsewhere, _, _) = await System.Threading.Tasks.Task.Run(UiaService.ProbeWorker);

        Assert.Equal(fromHere, fromElsewhere);
    }

    [Fact]
    public void Shutdown_DisposesWorker_AndALaterCallRestartsIt() {
        (_, _, int genBefore) = UiaService.ProbeWorker();

        UiaService.Shutdown();

        // Idempotent…
        UiaService.Shutdown();

        // …and the next UIA call lazily starts a fresh worker (new generation, fresh automation).
        (_, _, int genAfter) = UiaService.ProbeWorker();
        Assert.Equal(genBefore + 1, genAfter);
    }
}
