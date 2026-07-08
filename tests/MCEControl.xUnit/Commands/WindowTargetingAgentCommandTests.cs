// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Pins the shared #261 UIA-failure mapping every element-resolving command routes through:
/// classified UIA faults and ambiguous selectors map onto the closed taxonomy with stable codes,
/// and healthy outcomes map to null (no failure).
/// </summary>
public class WindowTargetingAgentCommandTests {
    [Theory]
    [InlineData(UiaFailureKind.WindowGone, "window-closed", "stale-element")]
    [InlineData(UiaFailureKind.AccessDenied, "target-elevated", "elevation")]
    [InlineData(UiaFailureKind.Faulted, "uia-faulted", "internal")]
    public void UiaFailureFor_MapsEachKindToADistinctCodeAndCategory(UiaFailureKind kind, string code, string category) {
        CommandResult? result = UiaFailureProbeCommand.Failure("query", kind);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(category, result.ErrorCategory);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void UiaFailureFor_None_IsNoFailure() {
        Assert.Null(UiaFailureProbeCommand.Failure("query", UiaFailureKind.None));
    }

    [Fact]
    public void UiaFindFailureFor_AmbiguousSelector_CarriesTheMatchCountInTheCode() {
        UiaFindOutcome outcome = new(null, MatchCount: 4, UiaFailureKind.None);

        CommandResult? result = UiaFailureProbeCommand.FindFailure("find", "name", "OK", outcome);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("selector-matched-4", result.ErrorCode);
        Assert.Equal("ambiguous-selector", result.ErrorCategory);
        Assert.Contains("OK", result.Error);
    }

    [Fact]
    public void UiaFindFailureFor_FoundOrCleanMiss_IsNoFailure() {
        // A found element and a clean miss are NOT failures here; a miss stays per-command (find
        // reports found:false success, click/drag/invoke fail with no-target).
        UiaFindOutcome found = new(new UiaElementInfo(), MatchCount: 1, UiaFailureKind.None);

        Assert.Null(UiaFailureProbeCommand.FindFailure("find", "name", "OK", found));
        Assert.Null(UiaFailureProbeCommand.FindFailure("find", "name", "OK", UiaFindOutcome.NotFound));
    }

    [Fact]
    public void OffsetByClientOrigin_AddsWindowClientOriginToWindowRelativePoint() {
        (int X, int Y) point = UiaFailureProbeCommand.OffsetPoint((412, 88), (1923, 45));

        Assert.Equal((2335, 133), point);
    }

    [Fact]
    public void OffsetByClientOrigin_PreservesNegativeOffsets() {
        (int X, int Y) point = UiaFailureProbeCommand.OffsetPoint((-8, 0), (3200, 120));

        Assert.Equal((3192, 120), point);
    }
}
