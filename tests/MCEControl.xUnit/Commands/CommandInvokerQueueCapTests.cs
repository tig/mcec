// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Verifies the command execution queue cap and embedded-expansion bound (#154).
/// The queue is drained synchronously on the UI thread with a paced sleep between items, so an
/// unbounded queue lets a remote client cause memory growth and a UI freeze.
///
/// Enqueue is ALL-OR-NOTHING per command tree: a command whose whole tree (itself plus all
/// recursively embedded commands) exceeds <see cref="CommandInvoker.MaxEmbeddedExpansion"/>, or
/// does not fit in the queue's remaining capacity (<see cref="CommandInvoker.MaxQueueDepth"/>),
/// is dropped WHOLE with a warning — never partially enqueued. Partial enqueue could split paired
/// input commands (e.g. shiftdown:/shiftup:) and leave a modifier key latched host-wide.
///
/// Uses the serial collection because the log4net hierarchy and Logger are process-global.
/// </summary>
[Collection("AgentSerial")]
public class CommandInvokerQueueCapTests {
    /// <summary>
    /// Attaches a MemoryAppender to the root of the (already configured, see Logger) log4net
    /// hierarchy so tests can assert on what CommandInvoker logged. Caller must detach.
    /// </summary>
    private static MemoryAppender AttachLogCapture() {
        _ = Logger.Instance.Log4; // ensure the hierarchy is configured
        MemoryAppender appender = new() { Name = "QueueCapTestCapture" };
        appender.ActivateOptions();
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
        hierarchy.Root.AddAppender(appender);
        return appender;
    }

    private static void DetachLogCapture(MemoryAppender appender) {
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
        hierarchy.Root.RemoveAppender(appender);
    }

    private static bool IsInvokerDropWarning(LoggingEvent e) =>
        e.Level >= Level.Warn &&
        e.RenderedMessage!.Contains("CommandInvoker", StringComparison.Ordinal) &&
        e.RenderedMessage!.Contains("dropp", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void EnqueueCommand_QueueDepthNeverExceedsCap_ExcessDroppedAndLogged() {
        MemoryAppender capture = AttachLogCapture();
        try {
            CommandInvoker invoker = [];
            const int overflow = 25;

            for (int i = 0; i < CommandInvoker.MaxQueueDepth + overflow; i++) {
                invoker.EnqueueCommand(new TestCommand() { Cmd = $"cmd{i}" });
                Assert.True(invoker.QueuedCommandCount <= CommandInvoker.MaxQueueDepth,
                    $"queue depth {invoker.QueuedCommandCount} exceeded cap {CommandInvoker.MaxQueueDepth} after enqueue #{i}");
            }

            Assert.Equal(CommandInvoker.MaxQueueDepth, invoker.QueuedCommandCount);
            Assert.Contains(capture.GetEvents(), IsInvokerDropWarning);
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Fact]
    public void EnqueueCommand_TreeBeyondExpansionBound_NothingEnqueuedAndLogged() {
        MemoryAppender capture = AttachLogCapture();
        try {
            CommandInvoker invoker = [];
            TestCommand parent = new() { Cmd = "parent", EmbeddedCommands = [] };
            for (int i = 0; i < CommandInvoker.MaxEmbeddedExpansion + 10; i++) {
                parent.EmbeddedCommands.Add(new TestCommand() { Cmd = $"child{i}" });
            }

            invoker.EnqueueCommand(parent);

            // ALL-OR-NOTHING: a tree over the bound must not be partially enqueued — a partial
            // tree could split paired input (shiftdown:/shiftup:) and latch a modifier key.
            Assert.Equal(0, invoker.QueuedCommandCount);
            Assert.Contains(capture.GetEvents(), IsInvokerDropWarning);
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Fact]
    public void EnqueueCommand_NestedTreeBeyondBound_NothingEnqueued() {
        CommandInvoker invoker = [];

        // A chain nested deeper than the bound: parent -> child -> grandchild -> ...
        // The bound applies to the whole recursive tree, not just direct children.
        TestCommand root = new() { Cmd = "root" };
        TestCommand current = root;
        for (int i = 0; i < CommandInvoker.MaxEmbeddedExpansion + 5; i++) {
            TestCommand next = new() { Cmd = $"nested{i}" };
            current.EmbeddedCommands = [next];
            current = next;
        }

        invoker.EnqueueCommand(root);

        Assert.Equal(0, invoker.QueuedCommandCount);
    }

    [Fact]
    public void EnqueueCommand_TreeExceedingRemainingCapacity_DroppedWhole_ExistingItemsIntact() {
        MemoryAppender capture = AttachLogCapture();
        try {
            CommandInvoker invoker = [];
            int preload = CommandInvoker.MaxQueueDepth - 5;
            for (int i = 0; i < preload; i++) {
                invoker.EnqueueCommand(new TestCommand() { Cmd = $"cmd{i}" });
            }
            Assert.Equal(preload, invoker.QueuedCommandCount);

            // A 10-command tree (root + 9 children) is within the expansion bound but does not
            // fit in the 5 remaining slots: the WHOLE tree must be dropped, not the first 5.
            TestCommand tooBig = new() { Cmd = "tooBig", EmbeddedCommands = [] };
            for (int i = 0; i < 9; i++) {
                tooBig.EmbeddedCommands.Add(new TestCommand() { Cmd = $"tooBigChild{i}" });
            }
            invoker.EnqueueCommand(tooBig);

            Assert.Equal(preload, invoker.QueuedCommandCount); // nothing added, nothing lost
            Assert.Contains(capture.GetEvents(), IsInvokerDropWarning);

            // A 5-command tree exactly fits the remaining capacity and must be accepted whole.
            TestCommand fits = new() { Cmd = "fits", EmbeddedCommands = [] };
            for (int i = 0; i < 4; i++) {
                fits.EmbeddedCommands.Add(new TestCommand() { Cmd = $"fitsChild{i}" });
            }
            invoker.EnqueueCommand(fits);

            Assert.Equal(CommandInvoker.MaxQueueDepth, invoker.QueuedCommandCount);
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Fact]
    public void EnqueueCommand_WithinBounds_AllCommandsEnqueuedAndExecuted() {
        CommandInvoker invoker = [];
        TestCommand parent = new() { Cmd = "parent", EmbeddedCommands = [] };
        List<TestCommand> children = [];
        for (int i = 0; i < 5; i++) {
            TestCommand child = new() { Cmd = $"child{i}" };
            children.Add(child);
            parent.EmbeddedCommands.Add(child);
        }

        invoker.EnqueueCommand(parent);
        Assert.Equal(6, invoker.QueuedCommandCount);

        AgentRuntime.SetEmergencyStopped(false);
        invoker.ExecuteNext();

        Assert.Equal(0, invoker.QueuedCommandCount);
        Assert.True(parent.ExecuteCalled, "parent should have executed");
        Assert.All(children, c => Assert.True(c.ExecuteCalled, $"{c.Cmd} should have executed"));
    }

    [Fact]
    public void EnqueueCommand_QueueDrained_AcceptsNewCommandsAgain() {
        CommandInvoker invoker = [];
        for (int i = 0; i < CommandInvoker.MaxQueueDepth; i++) {
            invoker.EnqueueCommand(new TestCommand() { Cmd = $"cmd{i}" });
        }
        Assert.Equal(CommandInvoker.MaxQueueDepth, invoker.QueuedCommandCount);

        AgentRuntime.SetEmergencyStopped(false);
        invoker.ExecuteNext();
        Assert.Equal(0, invoker.QueuedCommandCount);

        TestCommand after = new() { Cmd = "after" };
        invoker.EnqueueCommand(after);
        Assert.Equal(1, invoker.QueuedCommandCount);

        invoker.ExecuteNext();
        Assert.True(after.ExecuteCalled, "commands enqueued after a drain must execute normally");
    }

    [Fact]
    public void Enqueue_SingleCharDroppedWhenFull_LogsIdentifiableCommand() {
        MemoryAppender capture = AttachLogCapture();
        try {
            string tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            CommandInvoker invoker = CommandInvoker.Create(tempFile, "0.0.0.0", false);
            ((Command)invoker["chars:"]!).Enabled = true; // enable the single-char fast path

            for (int i = 0; i < CommandInvoker.MaxQueueDepth; i++) {
                invoker.EnqueueCommand(new TestCommand() { Cmd = $"cmd{i}" });
            }

            invoker.Enqueue(new TestReply(), "z");

            Assert.Equal(CommandInvoker.MaxQueueDepth, invoker.QueuedCommandCount);
            // The drop log must identify WHAT was dropped — the fast path builds a
            // SendInputCommand from the raw char, so the log line must carry it.
            Assert.Contains(capture.GetEvents(), e =>
                IsInvokerDropWarning(e) &&
                e.RenderedMessage!.EndsWith(": z", StringComparison.Ordinal));
        }
        finally {
            DetachLogCapture(capture);
        }
    }
}
