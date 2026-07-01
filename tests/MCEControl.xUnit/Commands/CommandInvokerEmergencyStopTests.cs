// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Verifies the emergency stop (#135) drops the command queue: when the latch is engaged,
/// <see cref="CommandInvoker.ExecuteNext"/> must stop dequeuing/executing so a paced or embedded
/// sequence can't keep actuating after the operator halted. Uses the serial collection because it
/// toggles the process-global latch.
/// </summary>
[Collection("AgentSerial")]
public class CommandInvokerEmergencyStopTests {
    [Fact]
    public void ExecuteNext_DropsQueuedCommands_WhenEmergencyStopped() {
        CommandInvoker invoker = [];
        TestCommand first = new() { Cmd = "a" };
        TestCommand second = new() { Cmd = "b" };
        invoker.EnqueueCommand(first);
        invoker.EnqueueCommand(second);

        AgentRuntime.SetEmergencyStopped(true);
        try {
            invoker.ExecuteNext();

            Assert.False(first.ExecuteCalled, "no command should run once the stop is latched");
            Assert.False(second.ExecuteCalled);
        }
        finally {
            AgentRuntime.SetEmergencyStopped(false);
        }
    }

    [Fact]
    public void ExecuteNext_RunsQueuedCommands_WhenNotStopped() {
        CommandInvoker invoker = [];
        TestCommand cmd = new() { Cmd = "a" };
        invoker.EnqueueCommand(cmd);

        AgentRuntime.SetEmergencyStopped(false);
        invoker.ExecuteNext();

        Assert.True(cmd.ExecuteCalled);
    }
}
