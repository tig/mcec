//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the bounded command accumulator (issue #148): received bytes accumulated into a
/// command builder must never grow without bound. A peer that streams bytes without a CR/LF/NUL
/// delimiter must not be able to drive the process to OutOfMemoryException; on overflow the
/// partial command is dropped, the caller is notified (so it can log), and input is discarded
/// until the next delimiter so a subsequent valid command still parses.
/// </summary>
public class CommandAccumulatorTests {
    private static List<string> Feed(CommandAccumulator accumulator, string input, Action? onOverflow = null) {
        var commands = new List<string>();
        foreach (char c in input) {
            string? cmd = accumulator.ProcessChar(c, onOverflow);
            if (cmd != null) {
                commands.Add(cmd);
            }
        }
        return commands;
    }

    [Fact]
    public void Delimiter_EmitsAccumulatedCommand() {
        var accumulator = new CommandAccumulator();

        var commands = Feed(accumulator, "vol+\r");

        Assert.Equal(new[] { "vol+" }, commands);
        Assert.Equal(0, accumulator.Length);
    }

    [Fact]
    public void AllThreeDelimiters_EmitCommands_AndEmptyLinesAreIgnored() {
        var accumulator = new CommandAccumulator();

        var commands = Feed(accumulator, "a\rb\nc\0\r\n\0");

        Assert.Equal(new[] { "a", "b", "c" }, commands);
    }

    [Fact]
    public void DelimiterlessFlood_NeverExceedsMaxCommandLength() {
        var accumulator = new CommandAccumulator();

        for (int i = 0; i < CommandAccumulator.MaxCommandLength * 3; i++) {
            _ = accumulator.ProcessChar('x');
            Assert.True(accumulator.Length <= CommandAccumulator.MaxCommandLength,
                $"accumulator grew to {accumulator.Length} chars after {i + 1} delimiter-less chars");
        }
    }

    [Fact]
    public void Overflow_InvokesCallbackOnce_AndResetsBuffer() {
        var accumulator = new CommandAccumulator();
        int overflows = 0;

        // One char past the cap triggers the overflow; the rest of the run must not
        // re-notify (no log flooding) and nothing may accumulate while discarding.
        _ = Feed(accumulator, new string('x', CommandAccumulator.MaxCommandLength + 100), () => overflows++);

        Assert.Equal(1, overflows);
        Assert.Equal(0, accumulator.Length);
    }

    [Fact]
    public void Overflow_DropsOversizedCommand_AndNextCommandStillParses() {
        var accumulator = new CommandAccumulator();

        // An oversized delimiter-less run, then a delimiter, then a valid command: the
        // oversized run (including its tail after the overflow) must never surface as a
        // command, and the accumulator must recover to parse the valid command.
        var commands = Feed(accumulator, new string('x', CommandAccumulator.MaxCommandLength + 100) + "\n" + "ok\n");

        Assert.Equal(new[] { "ok" }, commands);
    }

    [Fact]
    public void CommandOfExactlyMaxLength_IsDelivered() {
        var accumulator = new CommandAccumulator();
        string max = new('x', CommandAccumulator.MaxCommandLength);
        int overflows = 0;

        var commands = Feed(accumulator, max + "\n", () => overflows++);

        Assert.Equal(new[] { max }, commands);
        Assert.Equal(0, overflows);
    }
}
