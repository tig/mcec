// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Verifies the command execution queue cap and embedded-expansion fan-out bound (#154).
/// The queue is drained synchronously on the UI thread with a paced sleep between items, so an
/// unbounded queue lets a remote client cause memory growth and a UI freeze. Enqueues beyond
/// <see cref="CommandInvoker.MaxQueueDepth"/> must be dropped and logged; a single command's
/// embedded fan-out must be truncated at <see cref="CommandInvoker.MaxEmbeddedExpansion"/>.
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

            LoggingEvent[] events = capture.GetEvents();
            Assert.Contains(events, e =>
                e.Level >= Level.Warn &&
                e.RenderedMessage!.Contains("CommandInvoker", StringComparison.Ordinal) &&
                e.RenderedMessage!.Contains("dropp", StringComparison.OrdinalIgnoreCase));
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Fact]
    public void EnqueueCommand_EmbeddedExpansionBeyondBound_TruncatedAndLogged() {
        MemoryAppender capture = AttachLogCapture();
        try {
            CommandInvoker invoker = [];
            TestCommand parent = new() { Cmd = "parent", EmbeddedCommands = [] };
            for (int i = 0; i < CommandInvoker.MaxEmbeddedExpansion + 10; i++) {
                parent.EmbeddedCommands.Add(new TestCommand() { Cmd = $"child{i}" });
            }

            invoker.EnqueueCommand(parent);

            Assert.Equal(CommandInvoker.MaxEmbeddedExpansion, invoker.QueuedCommandCount);

            LoggingEvent[] events = capture.GetEvents();
            Assert.Contains(events, e =>
                e.Level >= Level.Warn &&
                e.RenderedMessage!.Contains("CommandInvoker", StringComparison.Ordinal) &&
                e.RenderedMessage!.Contains("truncat", StringComparison.OrdinalIgnoreCase));
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Fact]
    public void EnqueueCommand_NestedEmbeddedExpansion_CountsWholeTreeTowardBound() {
        CommandInvoker invoker = [];

        // A chain nested deeper than the bound: parent -> child -> grandchild -> ...
        TestCommand root = new() { Cmd = "root" };
        TestCommand current = root;
        for (int i = 0; i < CommandInvoker.MaxEmbeddedExpansion + 5; i++) {
            TestCommand next = new() { Cmd = $"nested{i}" };
            current.EmbeddedCommands = [next];
            current = next;
        }

        invoker.EnqueueCommand(root);

        Assert.Equal(CommandInvoker.MaxEmbeddedExpansion, invoker.QueuedCommandCount);
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
}
