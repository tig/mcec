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
    /// <summary>Generous bound for awaiting the dispatcher — a healthy drain takes milliseconds.</summary>
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

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

            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(Wait));

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

            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(Wait),
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
        // enqueues and awaits a completion the dispatcher signals AFTER Execute — so the response
        // must carry the output the command actually wrote to its Reply.
        AgentTestSupport.EnsureTelemetry();
        const string expectedOutput = "echoed-by-dispatcher";
        CommandInvoker invoker = [];
        DelegateTestCommand echoPrototype = new() {
            Cmd = "echo",
            OnExecute = c => c.Reply.WriteLine(expectedOutput),
        };
        invoker.Add("echo", (ICommand)echoPrototype);

        AgentRuntime.Settings = new AppSettings(); // CommandPacing = 0; stdio send_command is ungated
        AgentRuntime.Invoker = invoker;
        try {
            // AgentServer.Dispatch is synchronous and blocks on the completion internally (bounded
            // by SendCommandCompletionTimeoutMs) — run it off the xUnit worker like an MCP host would.
            JsonObject resp = await Task.Run(() => AgentServer.Dispatch(new JsonObject {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JsonObject {
                    ["name"] = "send_command",
                    ["arguments"] = new JsonObject { ["command"] = "echo" },
                },
            })!).WaitAsync(Wait);

            JsonObject toolResult = resp["result"]!.AsObject();
            Assert.False(toolResult["isError"]!.GetValue<bool>());
            JsonObject env = ParseEnvelope(toolResult);
            Assert.True(env["ok"]!.GetValue<bool>());
            Assert.Equal(expectedOutput, env["result"]!.AsObject()["output"]!.GetValue<string>());

            // The table prototype is never executed — Enqueue clones it (with the capturing reply).
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
                    Assert.True(releaseFirst.Wait(Wait), "test never released the blocking command");
                },
            };
            DelegateTestCommand second = new() { Cmd = "second" };

            invoker.EnqueueCommand(first);
            invoker.EnqueueCommand(second);
            Task<bool> drained = invoker.SignalWhenQueueDrained();

            // Engage the stop while the first command is mid-Execute; when it finishes, the
            // dispatcher must drop everything still queued (the second command AND the pending
            // completion marker — the awaiter fails instead of timing out).
            Assert.True(firstStarted.Wait(Wait), "dispatcher never started the first command");
            AgentRuntime.SetEmergencyStopped(true);
            releaseFirst.Set();

            Assert.False(await drained.WaitAsync(Wait),
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
            const int perProducer = 25; // 100 total — under the 200 queue cap even with no draining
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
                Assert.True(t.Join(Wait), "producer thread did not finish");
            }

            // The marker is enqueued after every producer finished, so it completes only after all
            // 100 commands have executed.
            Assert.True(await invoker.SignalWhenQueueDrained().WaitAsync(Wait));

            Assert.All(commands, c => Assert.Equal(1, c.ExecuteCount));
        }
        finally {
            invoker.Shutdown();
        }
    }

    [Fact]
    public async Task Shutdown_DropsQueuedCommands_AndFailsNewCompletions() {
        CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
        DelegateTestCommand queued = new() { Cmd = "queued" };
        invoker.EnqueueCommand(queued);

        invoker.Shutdown();

        // A completion requested after shutdown must fail fast, not hang its awaiter.
        Assert.False(await invoker.SignalWhenQueueDrained().WaitAsync(Wait));

        // Nothing queued before shutdown executes, even if someone pumps.
        invoker.PumpQueueForTests();
        Assert.Equal(0, queued.ExecuteCount);
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
