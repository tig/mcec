using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Integration;

/// <summary>
/// Integration tests for the command execution pipeline: they exercise <see cref="CommandInvoker.Enqueue"/>
/// parsing/routing and assert it does not throw.
///
/// SAFETY: every invoker here sets <see cref="CommandInvoker.SuppressDispatcherForTests"/> before the first
/// enqueue. Without it the invoker starts its real dispatcher thread and EXECUTES enabled commands; the
/// tests enable <c>chars:</c> and enqueue <c>"chars:hello"</c> and <c>"a"</c>, so a plain <c>dotnet test</c>
/// synthesized real keystrokes ("hello", "a") into whatever window had focus. Suppressing the dispatcher
/// keeps the parse/enqueue path (all these tests assert) while guaranteeing no SendInput reaches the desktop
/// (matches the pattern in <c>CommandInvokerQueueCapTests</c>).
/// </summary>
public class CommandExecutionPipelineTests
{
    [Fact]
    public void CommandInvoker_Enqueue_ParsesSimpleCommand()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
        invoker.SuppressDispatcherForTests = true; // never actuate the real desktop from a unit test

        // Enable the pause command for testing
        if (invoker["pause"] is PauseCommand pauseCmd)
        {
            pauseCmd.Enabled = true;
        }

        var reply = new TestReply();

        // Just test that enqueue doesn't throw
        var exception = Record.Exception(() => invoker.Enqueue(reply, "pause"));
        Assert.Null(exception);

        File.Delete(tempFile);
    }

    [Fact]
    public void CommandInvoker_WithColonCommand_ParsesCorrectly()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
        invoker.SuppressDispatcherForTests = true; // never synthesize "hello" into the focused window

        // Enable chars command
        if (invoker["chars:"] is CharsCommand charsCmd)
        {
            charsCmd.Enabled = true;
        }

        var reply = new TestReply();

        // This should parse "chars:" as command and "hello" as args
        // Just test that enqueue doesn't throw
        var exception = Record.Exception(() => invoker.Enqueue(reply, "chars:hello"));
        Assert.Null(exception);

        File.Delete(tempFile);
    }

    [Fact]
    public void UnknownCommand_IsHandledGracefully()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
        invoker.SuppressDispatcherForTests = true;
        var reply = new TestReply();

        // This should not throw
        var exception = Record.Exception(() =>
        {
            invoker.Enqueue(reply, "unknowncommand12345");
        });

        Assert.Null(exception);

        File.Delete(tempFile);
    }

    [Fact]
    public void SingleCharacter_TreatedAsCharsCommand()
    {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);

        var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
        invoker.SuppressDispatcherForTests = true; // never synthesize "a" into the focused window

        // Enable chars command
        if (invoker["chars:"] is CharsCommand charsCmd)
        {
            charsCmd.Enabled = true;
        }

        var reply = new TestReply();

        // Single character should be treated as chars: command
        var exception = Record.Exception(() =>
        {
            invoker.Enqueue(reply, "a");
        });

        Assert.Null(exception);

        File.Delete(tempFile);
    }
}
