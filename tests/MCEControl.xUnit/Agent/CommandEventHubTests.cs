// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>Tests the #119 command-event hub: subscribers receive published events, unsubscribe works,
/// and a throwing subscriber can't break publishing (commands must never fail because of the overlay).</summary>
public class CommandEventHubTests {
    private static CommandEvent Sample(string text = "capture window=\"About\"") =>
        new("capture", text, CommandOutcome.Ok, "s-1");

    [Fact]
    public void Subscribe_ReceivesPublishedEvents_UntilUnsubscribed() {
        List<CommandEvent> seen = [];
        void Handler(CommandEvent e) => seen.Add(e);

        CommandEventHub.Subscribe(Handler);
        try {
            CommandEventHub.Publish(Sample("one"));
            CommandEventHub.Unsubscribe(Handler);
            CommandEventHub.Publish(Sample("two"));
        }
        finally {
            CommandEventHub.Unsubscribe(Handler);
        }

        Assert.Single(seen);
        Assert.Equal("one", seen[0].TerseText);
    }

    [Fact]
    public void Publish_IsBestEffort_AThrowingSubscriberDoesNotBreakOthersOrThrow() {
        List<CommandEvent> seen = [];
        void Throws(CommandEvent e) => throw new InvalidOperationException("boom");
        void Records(CommandEvent e) => seen.Add(e);

        CommandEventHub.Subscribe(Throws);
        CommandEventHub.Subscribe(Records);
        try {
            CommandEventHub.Publish(Sample("survives")); // must not throw despite Throws
        }
        finally {
            CommandEventHub.Unsubscribe(Throws);
            CommandEventHub.Unsubscribe(Records);
        }

        Assert.Single(seen);
        Assert.Equal("survives", seen[0].TerseText);
    }
}
