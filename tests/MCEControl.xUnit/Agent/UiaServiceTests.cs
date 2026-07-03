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
        // An unsupported action is rejected before any UIA attach; even a bogus handle never gets
        // that far; and reports ActionUnknown (fix the argument), not a not-found/pattern failure.
        UiaInvokeResult result = UiaService.Invoke(new IntPtr(0x1), "name", "OK", action, null);

        Assert.Equal(UiaInvokeResult.ActionUnknown, result);
    }

    [Fact]
    public void DumpTree_ZeroHandle_ReturnsEmptyUntruncatedResult() {
        // The guard path must return a well-formed result (null root, no nodes, not truncated, no
        // classified failure) rather than null, so query can always read nodeCount/truncated.
        UiaTreeResult result = UiaService.DumpTree(IntPtr.Zero, maxDepth: 6, maxNodes: 1000);

        Assert.NotNull(result);
        Assert.Null(result.Root);
        Assert.Equal(0, result.NodeCount);
        Assert.False(result.Truncated);
        Assert.Equal(UiaFailureKind.None, result.Failure);
    }

    [Fact]
    public void Find_ZeroHandle_ReturnsCleanMiss() {
        // #261: the guard path is a clean miss (no match, no fault), never a classified failure.
        UiaFindOutcome outcome = UiaService.Find(IntPtr.Zero, "name", "OK", timeoutMs: 0);

        Assert.Null(outcome.Element);
        Assert.Equal(0, outcome.MatchCount);
        Assert.False(outcome.Ambiguous);
        Assert.Equal(UiaFailureKind.None, outcome.Failure);
    }

    // --- #261: UIA exception classification onto the closed error taxonomy.

    [Fact]
    public void ClassifyUiaFailure_ElementNotAvailableHResult_IsWindowGone() {
        // UIA_E_ELEMENTNOTAVAILABLE: the window/element backing a UIA object is gone -> stale-element.
        var e = new System.Runtime.InteropServices.COMException("gone", unchecked((int)0x80040201));

        Assert.Equal(UiaFailureKind.WindowGone, UiaService.ClassifyUiaFailure(e));
    }

    [Fact]
    public void ClassifyUiaFailure_FlaUIElementNotAvailable_IsWindowGone() {
        // FlaUI wraps the COM error in a typed exception on some paths; both forms classify the same.
        var e = new FlaUI.Core.Exceptions.ElementNotAvailableException("gone");

        Assert.Equal(UiaFailureKind.WindowGone, UiaService.ClassifyUiaFailure(e));
    }

    [Fact]
    public void ClassifyUiaFailure_AccessDenied_IsElevationCase() {
        // E_ACCESSDENIED on a valid window is the UIPI/elevation case, via COMException or the
        // UnauthorizedAccessException COM interop sometimes surfaces instead.
        var com = new System.Runtime.InteropServices.COMException("denied", unchecked((int)0x80070005));
        var uae = new UnauthorizedAccessException("denied");

        Assert.Equal(UiaFailureKind.AccessDenied, UiaService.ClassifyUiaFailure(com));
        Assert.Equal(UiaFailureKind.AccessDenied, UiaService.ClassifyUiaFailure(uae));
    }

    [Fact]
    public void ClassifyUiaFailure_WalksInnerExceptions() {
        // A classified COM error wrapped by an outer exception must still classify.
        var inner = new System.Runtime.InteropServices.COMException("gone", unchecked((int)0x80040201));
        var outer = new InvalidOperationException("wrapper", inner);

        Assert.Equal(UiaFailureKind.WindowGone, UiaService.ClassifyUiaFailure(outer));
    }

    [Fact]
    public void ClassifyUiaFailure_UnrecognizedException_IsFaulted() {
        Assert.Equal(UiaFailureKind.Faulted, UiaService.ClassifyUiaFailure(new InvalidOperationException("boom")));
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
    public async Task Worker_CallsFromDifferentThreads_LandOnTheSameWorker() {
        (int fromHere, _, _) = UiaService.ProbeWorker();
        (int fromElsewhere, _, _) = await Task.Run(UiaService.ProbeWorker);

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
