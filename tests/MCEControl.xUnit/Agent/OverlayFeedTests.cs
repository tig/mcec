// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 overlay feed: a bounded, no-timeout scrolling buffer that returns the most
/// recent action first (newest at the top) and evicts the oldest only when the line cap is exceeded.</summary>
public class OverlayFeedTests {
    private static CommandEvent Ev(string text) => new("capture", text, CommandOutcome.Ok);

    [Fact]
    public void Add_EvictsOldestBeyondLineCap() {
        OverlayFeed feed = new(maxLines: 2);

        feed.Add(Ev("a"));
        feed.Add(Ev("b"));
        feed.Add(Ev("c"));

        var snapshot = feed.Snapshot();
        Assert.Equal(2, snapshot.Count);
        // Newest first, and "a" was evicted by the cap.
        Assert.Equal("c", snapshot[0].TerseText);
        Assert.Equal("b", snapshot[1].TerseText);
    }

    [Fact]
    public void Snapshot_ReturnsNewestFirst() {
        OverlayFeed feed = new(maxLines: 10);
        feed.Add(Ev("1"));
        feed.Add(Ev("2"));
        feed.Add(Ev("3"));

        var snapshot = feed.Snapshot();
        Assert.Equal("3", snapshot[0].TerseText);
        Assert.Equal("2", snapshot[1].TerseText);
        Assert.Equal("1", snapshot[2].TerseText);
    }

    [Fact]
    public void Snapshot_EmptyWhenNothingAdded() {
        OverlayFeed feed = new(maxLines: 10);

        Assert.Empty(feed.Snapshot());
    }

    [Fact]
    public void Entries_DoNotTimeOut() {
        // Regression for the "actions time out and disappear" behavior: entries persist until the line
        // cap pushes them off, never by age.
        OverlayFeed feed = new(maxLines: 10);
        feed.Add(Ev("kept"));

        Assert.Single(feed.Snapshot());
        Assert.Equal("kept", feed.Snapshot()[0].TerseText);
    }
}
