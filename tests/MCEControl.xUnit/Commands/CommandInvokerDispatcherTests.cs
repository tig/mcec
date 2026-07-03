// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Verifies the #195 single-dispatcher contract: one long-running thread owned by the
/// <see cref="CommandInvoker"/> is the ONLY consumer of the execute queue. Commands run in order on
/// that thread (not the producer's); a throwing command doesn't strand the queue; the agent's
/// <c>send_command</c> returns only after its command actually executed (reading real Reply output);
/// the emergency stop drops the queue and fails pending completions; and concurrent producers each
/// get exactly-once execution. Uses the serial collection because it touches AgentRuntime statics
/// (the emergency-stop latch, Settings, Invoker).
/// </summary>
[Collection("AgentSerial")]
public class CommandInvokerDispatcherTests {
    /// <summary>Generous bound for awaiting the dispatcher; a healthy drain takes milliseconds.</summary>
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Dispatcher_ExecutesInOrder_OnItsOwnSingleThread() {
        CommandInvoker invoker = [];
        try {
            List<string> order = [];
            ConcurrentBag<int> threadIds = [];
            ConcurrentBag<string?> threadNames = [];
            DelegateTestCommand Make(string name) => new() {
                Cmd = name,
                OnExecute = c => {
                    order.Add(c.Cmd); // dispatcher is single-threaded, so no lock needed
                    threadIds.Add(Environment.CurrentManagedThreadId);
                    threadNames.Add(Thread.CurrentThread.Name);
                },
            };

            invoker.EnqueueCommand(Make("first"));
            invoker.EnqueueCommand(Make("second"));
            invoker.EnqueueCommand(Make("third"));

            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(_wait));

            Assert.Equal(["first", "second", "third"], order);
            int dispatcherThreadId = Assert.Single(threadIds.Distinct());
            Assert.NotEqual(Environment.CurrentManagedThreadId, dispatcherThreadId);
            Assert.Equal("MCEC-CommandDispatcher", Assert.Single(threadNames.Distinct()));
        }
        finally {
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Dispatcher_ThrowingCommand_DoesNotStopSubsequentCommands() {
        CommandInvoker invoker = [];
        try {
            DelegateTestCommand thrower = new() {
                Cmd = "thrower",
                OnExecute = _ => throw new InvalidOperationException("boom (deliberate test fault)"),
            };
            DelegateTestCommand after = new() { Cmd = "after" };

            invoker.EnqueueCommand(thrower);
            invoker.EnqueueCommand(after);

            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(_wait),
                "dispatcher did not survive the throwing command");

            Assert.Equal(1, thrower.ExecuteCount);
            Assert.Equal(1, after.ExecuteCount);
        }
        finally {
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task SendCommand_ReturnsOnlyAfterExecution_WithRealCapturedOutput() {
        // The pre-#195 race: send_command read reply.Captured while another thread was still (or not
        // yet) executing the command, reporting "ok" for a command that never ran. Now the tool
        // enqueues and awaits a completion the dispatcher signals AFTER Execute; so the response
        // must carry the output the command actually wrote to its Reply.
        AgentTestSupport.EnsureTelemetry();
        const string expectedOutput = "echoed-by-dispatcher";
        CommandInvoker invoker = [];
        DelegateTestCommand echoPrototype = new() {
            Cmd = "echo",
            OnExecute = c => {
                // The dispatcher clones the prototype with a live reply context; assert that
                // contract instead of assuming it.
                Assert.NotNull(c.Reply);
                c.Reply.WriteLine(expectedOutput);
            },
        };
        invoker.Add("echo", echoPrototype);

        AgentRuntime.Settings = new AppSettings(); // CommandPacing = 0; stdio send_command is ungated
        AgentRuntime.Invoker = invoker;
        try {
            // AgentServer.Dispatch is synchronous and blocks on the completion internally (bounded
            // by SendCommandCompletionTimeoutMs); run it off the xUnit worker like an MCP host would.
            JsonObject resp = await Task.Run(() => AgentServer.Dispatch(new JsonObject {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JsonObject {
                    ["name"] = "send_command",
                    ["arguments"] = new JsonObject { ["command"] = "echo" },
                },
            })!).WaitAsync(_wait);

            JsonObject toolResult = resp["result"]!.AsObject();
            Assert.False(toolResult["isError"]!.GetValue<bool>());
            JsonObject env = ParseEnvelope(toolResult);
            Assert.True(env["ok"]!.GetValue<bool>());
            Assert.Equal(expectedOutput, env["result"]!.AsObject()["output"]!.GetValue<string>());

            // The table prototype is never executed; Enqueue clones it (with the capturing reply).
            Assert.Equal(0, echoPrototype.ExecuteCount);
        }
        finally {
            AgentRuntime.Invoker = null;
            AgentRuntime.Settings = null;
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Dispatcher_EmergencyStop_DropsQueue_AndFailsPendingCompletions() {
        CommandInvoker invoker = [];
        using ManualResetEventSlim firstStarted = new(false);
        using ManualResetEventSlim releaseFirst = new(false);
        try {
            DelegateTestCommand first = new() {
                Cmd = "blocker",
                OnExecute = _ => {
                    firstStarted.Set();
                    Assert.True(releaseFirst.Wait(_wait), "test never released the blocking command");
                },
            };
            DelegateTestCommand second = new() { Cmd = "second" };

            invoker.EnqueueCommand(first);
            invoker.EnqueueCommand(second);
            Task<bool> drained = invoker.SignalWhenQueueDrained();

            // Engage the stop while the first command is mid-Execute; when it finishes, the
            // dispatcher must drop everything still queued (the second command AND the pending
            // completion marker; the awaiter fails instead of timing out).
            Assert.True(firstStarted.Wait(_wait), "dispatcher never started the first command");
            AgentRuntime.SetEmergencyStopped(true);
            releaseFirst.Set();

            Assert.False(await drained.WaitAsync(_wait),
                "the pending completion must be signalled as dropped, not executed");
            Assert.Equal(1, first.ExecuteCount);
            Assert.Equal(0, second.ExecuteCount);
        }
        finally {
            AgentRuntime.SetEmergencyStopped(false);
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Dispatcher_ConcurrentEnqueues_AllExecuteExactlyOnce() {
        CommandInvoker invoker = [];
        try {
            const int producers = 4;
            const int perProducer = 25; // 100 total; under the 200 queue cap even with no draining
            List<DelegateTestCommand> commands = [];
            for (int i = 0; i < producers * perProducer; i++) {
                commands.Add(new DelegateTestCommand { Cmd = $"cmd{i}" });
            }

            Thread[] threads = [.. Enumerable.Range(0, producers).Select(p => new Thread(() => {
                for (int i = 0; i < perProducer; i++) {
                    invoker.EnqueueCommand(commands[(p * perProducer) + i]);
                }
            }))];
            foreach (Thread t in threads) {
                t.Start();
            }
            foreach (Thread t in threads) {
                Assert.True(t.Join(_wait), "producer thread did not finish");
            }

            // The marker is enqueued after every producer finished, so it completes only after all
            // 100 commands have executed.
            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(_wait));

            Assert.All(commands, c => Assert.Equal(1, c.ExecuteCount));
        }
        finally {
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Shutdown_DropsQueuedCommands_FailsNewCompletions_AndReleasesHeldInput() {
        // Dropping queued commands at shutdown can sever a partially executed tree (shiftdown: ran,
        // shiftup: dropped), so the drop must run the same held-input release the emergency stop
        // uses. Swap the release seam so the test observes the call without injecting real input.
        Action originalRelease = CommandInvoker.ReleaseHeldInputOnDrop;
        int releaseCalls = 0;
        CommandInvoker.ReleaseHeldInputOnDrop = () => Interlocked.Increment(ref releaseCalls);
        try {
            CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
            DelegateTestCommand queued = new() { Cmd = "queued" };
            invoker.EnqueueCommand(queued);

            invoker.Shutdown();

            Assert.Equal(1, releaseCalls); // a real command was dropped → input released, exactly once

            // A completion requested after shutdown must fail fast, not hang its awaiter.
            Assert.False(await invoker.SignalWhenQueueDrained().WaitAsync(_wait));

            // Nothing queued before shutdown executes, even if someone pumps.
            invoker.PumpQueueForTests();
            Assert.Equal(0, queued.ExecuteCount);
        }
        finally {
            CommandInvoker.ReleaseHeldInputOnDrop = originalRelease;
        }
    }

    [Fact]
    public async Task Shutdown_WithLiveDispatcher_DropsTail_ReleasesInput_AndFailsPendingCompletionFast() {
        Action originalRelease = CommandInvoker.ReleaseHeldInputOnDrop;
        int releaseCalls = 0;
        CommandInvoker.ReleaseHeldInputOnDrop = () => Interlocked.Increment(ref releaseCalls);
        CommandInvoker invoker = [];
        using ManualResetEventSlim firstStarted = new(false);
        using ManualResetEventSlim releaseFirst = new(false);
        try {
            // First command blocks mid-Execute (a long-running command); the second is the queued
            // "tail" (think shiftup: after shiftdown:) that Shutdown will drop.
            DelegateTestCommand first = new() {
                Cmd = "long-runner",
                OnExecute = _ => {
                    firstStarted.Set();
                    Assert.True(releaseFirst.Wait(_wait), "test never released the blocking command");
                },
            };
            DelegateTestCommand tail = new() { Cmd = "tail" };
            invoker.EnqueueCommand(first);
            invoker.EnqueueCommand(tail);
            Task<bool> pending = invoker.SignalWhenQueueDrained();

            Assert.True(firstStarted.Wait(_wait), "dispatcher never started the first command");
            invoker.Shutdown(); // no join: must not block on the still-running command

            // #195 review: the pending completion is failed IMMEDIATELY from Shutdown; it must not
            // wait behind the in-flight command (which is still blocked right now).
            Assert.False(await pending.WaitAsync(_wait));

            releaseFirst.Set(); // let the in-flight command finish; the dispatcher then drops the tail

            SpinWait.SpinUntil(() => Volatile.Read(ref releaseCalls) > 0, _wait);
            Assert.Equal(1, releaseCalls); // the dropped tail triggered exactly one input release
            Assert.Equal(1, first.ExecuteCount);
            Assert.Equal(0, tail.ExecuteCount);
        }
        finally {
            releaseFirst.Set();
            CommandInvoker.ReleaseHeldInputOnDrop = originalRelease;
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Dispatcher_HoldsInputGate_OnlyForInputSynthesizingCommands() {
        CommandInvoker invoker = [];
        using ManualResetEventSlim commandStarted = new(false);
        using ManualResetEventSlim releaseCommand = new(false);
        try {
            // A non-input command (SynthesizesInput=false, like pause) blocks mid-Execute: the gate
            // must be FREE; a pause:60000 in a macro must not starve a concurrent agent drag.
            DelegateTestCommand pauseLike = new() {
                Cmd = "pause-like",
                SynthesizesInputForTest = false,
                OnExecute = _ => {
                    commandStarted.Set();
                    Assert.True(releaseCommand.Wait(_wait), "test never released the pause-like command");
                },
            };
            invoker.EnqueueCommand(pauseLike);
            Assert.True(commandStarted.Wait(_wait), "dispatcher never started the pause-like command");
            Assert.True(TryProbeInputGate(), "InputGate must NOT be held while a non-input command executes");
            releaseCommand.Set();
            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(_wait));

            // An input-synthesizing command (the conservative default) blocking mid-Execute: the
            // gate must be HELD; that is the #113 no-interleaving invariant.
            commandStarted.Reset();
            releaseCommand.Reset();
            DelegateTestCommand inputLike = new() {
                Cmd = "input-like",
                OnExecute = _ => {
                    commandStarted.Set();
                    Assert.True(releaseCommand.Wait(_wait), "test never released the input-like command");
                },
            };
            invoker.EnqueueCommand(inputLike);
            Assert.True(commandStarted.Wait(_wait), "dispatcher never started the input-like command");
            Assert.False(TryProbeInputGate(), "InputGate MUST be held while an input-synthesizing command executes");
            releaseCommand.Set();
            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(_wait));
        }
        finally {
            releaseCommand.Set();
            invoker.Shutdown();
        }
    }

    /// <summary>Tries to take <see cref="AgentRuntime.InputGate"/> without blocking; true = it was free.</summary>
    private static bool TryProbeInputGate() {
        if (!Monitor.TryEnter(AgentRuntime.InputGate, 0)) {
            return false;
        }
        Monitor.Exit(AgentRuntime.InputGate);
        return true;
    }

    [Fact]
    public void SynthesizesInput_Classification_IsConservative() {
        // pause and mcec: provably emit no input and may run outside the gate; everything else
        // stays true by default (the conservative posture; a wrong false re-opens #113).
        Assert.False(new PauseCommand().SynthesizesInput);
        Assert.False(new McecCommand().SynthesizesInput);
        Assert.True(new SendInputCommand().SynthesizesInput);
        Assert.True(new CharsCommand().SynthesizesInput);
        Assert.True(new MouseCommand().SynthesizesInput);
        Assert.True(new StartProcessCommand().SynthesizesInput); // WaitForInputIdle precedes embedded input
    }

    [Fact]
    public async Task SendCommand_UnknownCommand_ReturnsUnknownCommandError() {
        // #195 review: send_command must not report ok for a command that never entered the queue.
        AgentTestSupport.EnsureTelemetry();
        CommandInvoker invoker = [];
        AgentRuntime.Settings = new AppSettings();
        AgentRuntime.Invoker = invoker;
        try {
            JsonObject resp = await Task.Run(() => AgentServer.Dispatch(new JsonObject {
                ["jsonrpc"] = "2.0",
                ["id"] = 7,
                ["method"] = "tools/call",
                ["params"] = new JsonObject {
                    ["name"] = "send_command",
                    ["arguments"] = new JsonObject { ["command"] = "no-such-command" },
                },
            })!).WaitAsync(_wait);

            JsonObject toolResult = resp["result"]!.AsObject();
            Assert.True(toolResult["isError"]!.GetValue<bool>());
            JsonObject env = ParseEnvelope(toolResult);
            Assert.False(env["ok"]!.GetValue<bool>());
            Assert.Equal("unknown-command", env["error"]!.AsObject()["code"]!.GetValue<string>());
        }
        finally {
            AgentRuntime.Invoker = null;
            AgentRuntime.Settings = null;
            invoker.Shutdown();
        }
    }

    [Fact]
    public void TryEnqueueWithCompletion_BoundsDrop_ReportsDropped_NotEnqueued() {
        // #195 review: a known command whose tree is refused by the #154 queue-depth cap must
        // surface as Dropped (send_command turns that into a command-dropped error), never as a
        // silent success. Suppressed dispatcher keeps the queue full for the whole test.
        CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
        DelegateTestCommand echo = new() { Cmd = "echo" };
        invoker.Add("echo", echo);
        for (int i = 0; i < CommandInvoker.MaxQueueDepth; i++) {
            invoker.EnqueueCommand(new DelegateTestCommand { Cmd = $"fill{i}" });
        }

        CommandEnqueueResult result = invoker.TryEnqueueWithCompletion(new TestReply(), "echo", out Task<bool>? completion);

        Assert.Equal(CommandEnqueueResult.Dropped, result);
        Assert.Null(completion);
        Assert.Equal(CommandInvoker.MaxQueueDepth, invoker.QueuedCommandCount); // nothing added

        // And the unknown path reports UnknownCommand, distinctly.
        Assert.Equal(CommandEnqueueResult.UnknownCommand,
            invoker.TryEnqueueWithCompletion(new TestReply(), "nope", out Task<bool>? none));
        Assert.Null(none);
    }

    private static JsonObject ParseEnvelope(JsonObject toolResult) {
        foreach (JsonNode? block in toolResult["content"]!.AsArray()) {
            if (block?["type"]?.GetValue<string>() == "text") {
                return JsonNode.Parse(block["text"]!.GetValue<string>())!.AsObject();
            }
        }
        Assert.Fail("no text content block in tool result");
        return [];
    }
}
