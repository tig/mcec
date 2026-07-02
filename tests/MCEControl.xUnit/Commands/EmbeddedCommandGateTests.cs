// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Security regression tests for issue #145: embedded (nested) commands must not bypass the
/// per-command <see cref="Command.Enabled"/> gate. A disabled parent must suppress its children;
/// flattening enabled children into the queue as independent siblings would let an unauthenticated
/// remote client fire them from a single command string against the secure default install.
/// Uses the serial collection because the invoker's drain (pumped synchronously here, #195) reads the global
/// emergency-stop latch.
/// </summary>
[Collection("AgentSerial")]
public class EmbeddedCommandGateTests {
    [Fact]
    public void DisabledParent_DoesNotRunEnabledChildren() {
        // The #145 shape: a disabled parent (StartProcess gated off) carrying Enabled=true children.
        var childA = new TestCommand { Cmd = "childA" };
        var childB = new TestCommand { Cmd = "childB" };
        var parent = new TestCommand {
            Cmd = "parent",
            Enabled = false,
            EmbeddedCommands = [childA, childB]
        };

        CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
        invoker.EnqueueCommand(parent);
        invoker.PumpQueueForTests();

        Assert.False(parent.ExecuteCalled, "the disabled parent must not run");
        Assert.False(childA.ExecuteCalled, "an enabled child of a disabled parent must NOT run (#145)");
        Assert.False(childB.ExecuteCalled, "an enabled child of a disabled parent must NOT run (#145)");
    }

    [Fact]
    public void EnabledParent_RunsEnabledChildren() {
        var child = new TestCommand { Cmd = "child" };
        var parent = new TestCommand {
            Cmd = "parent",
            Enabled = true,
            EmbeddedCommands = [child]
        };

        CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
        invoker.EnqueueCommand(parent);
        invoker.PumpQueueForTests();

        Assert.True(parent.ExecuteCalled);
        Assert.True(child.ExecuteCalled, "an enabled child of an enabled parent should run");
    }

    [Fact]
    public void EnabledParent_DisabledChild_SuppressesGrandchildren() {
        // A disabled command anywhere in the chain must stop its subtree from executing,
        // even if the grandchildren are individually Enabled=true.
        var grandchild = new TestCommand { Cmd = "grandchild" };
        var disabledChild = new TestCommand {
            Cmd = "child",
            Enabled = false,
            EmbeddedCommands = [grandchild]
        };
        var parent = new TestCommand {
            Cmd = "parent",
            Enabled = true,
            EmbeddedCommands = [disabledChild]
        };

        CommandInvoker invoker = new() { SuppressDispatcherForTests = true };
        invoker.EnqueueCommand(parent);
        invoker.PumpQueueForTests();

        Assert.True(parent.ExecuteCalled);
        Assert.False(disabledChild.ExecuteCalled, "the disabled child must not run");
        Assert.False(grandchild.ExecuteCalled, "a grandchild under a disabled child must NOT run (#145)");
    }

    [Fact]
    public void ShippedTypeIntoNotepad_HasNoEnabledDescendants() {
        // Defense in depth: the shipped demo (disabled parent, previously Enabled=true children)
        // must be inert until the user deliberately enables it.
        var typeIntoNotepad = StartProcessCommand.BuiltInCommands
            .Find(c => c.Cmd == "type_into_notepad");
        Assert.NotNull(typeIntoNotepad);
        Assert.False(typeIntoNotepad!.Enabled, "the shipped parent is disabled by default");
        AssertNoEnabledDescendants(typeIntoNotepad);
    }

    private static void AssertNoEnabledDescendants(Command command) {
        if (command.EmbeddedCommands is null) {
            return;
        }
        foreach (Command child in command.EmbeddedCommands) {
            Assert.False(child.Enabled, $"shipped embedded command '{child.Cmd}' must be disabled by default (#145)");
            AssertNoEnabledDescendants(child);
        }
    }
}
