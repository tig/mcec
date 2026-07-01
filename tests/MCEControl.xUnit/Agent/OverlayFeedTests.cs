// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 overlay feed: bounded by line count and self-pruning by age (deterministic
/// via injected time), so the on-screen feed stays small with no scrollbars.</summary>
public class OverlayFeedTests {
    private static readonly DateTime T0 = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    private static CommandEvent Ev(string text) => new("capture", text, CommandOutcome.Ok);

    [Fact]
    public void Add_EvictsOldestBeyondLineCap() {
        OverlayFeed feed = new(maxLines: 2, lifetime: TimeSpan.FromMinutes(10));

        feed.Add(Ev("a"), T0);
        feed.Add(Ev("b"), T0);
        feed.Add(Ev("c"), T0);

        var visible = feed.Visible(T0);
        Assert.Equal(2, visible.Count);
        Assert.Equal("b", visible[0].TerseText);
        Assert.Equal("c", visible[1].TerseText);
    }

    [Fact]
    public void Visible_DropsLinesOlderThanLifetime() {
        OverlayFeed feed = new(maxLines: 10, lifetime: TimeSpan.FromSeconds(5));

        feed.Add(Ev("old"), T0);
        feed.Add(Ev("fresh"), T0.AddSeconds(4));

        var visible = feed.Visible(T0.AddSeconds(6)); // old is 6s (expired), fresh is 2s
        Assert.Single(visible);
        Assert.Equal("fresh", visible[0].TerseText);
    }

    [Fact]
    public void Visible_EmptyWhenAllExpired() {
        OverlayFeed feed = new(maxLines: 10, lifetime: TimeSpan.FromSeconds(2));
        feed.Add(Ev("x"), T0);

        Assert.Empty(feed.Visible(T0.AddSeconds(3)));
    }

    [Fact]
    public void Visible_ReturnsOldestFirst() {
        OverlayFeed feed = new(maxLines: 10, lifetime: TimeSpan.FromMinutes(1));
        feed.Add(Ev("1"), T0);
        feed.Add(Ev("2"), T0.AddSeconds(1));

        var visible = feed.Visible(T0.AddSeconds(2));
        Assert.Equal("1", visible[0].TerseText);
        Assert.Equal("2", visible[1].TerseText);
    }
}
