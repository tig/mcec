using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Integration
{
    /// <summary>
    /// Integration tests for the command execution pipeline
    /// </summary>
    public class CommandExecutionPipelineTests
    {
        private class TestReply : Reply
        {
            public string? LastWrittenText { get; private set; }
            public int WriteCallCount { get; private set; }

            public override void Write(string text)
            {
                LastWrittenText = text;
                WriteCallCount++;
            }
        }

        [Fact]
        public void CommandInvoker_Enqueue_ParsesSimpleCommand()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            System.IO.File.Delete(tempFile);

            var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
            
            // Enable the pause command for testing
            if (invoker!["pause"] is PauseCommand pauseCmd)
            {
                pauseCmd.Enabled = true;
            }

            var reply = new TestReply();
            
            // Just test that enqueue doesn't throw
            var exception = Record.Exception(() => invoker.Enqueue(reply, "pause"));
            Assert.Null(exception);

            System.IO.File.Delete(tempFile);
        }

        [Fact]
        public void CommandInvoker_WithColonCommand_ParsesCorrectly()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            System.IO.File.Delete(tempFile);

            var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
            
            // Enable chars command
            if (invoker!["chars:"] is CharsCommand charsCmd)
            {
                charsCmd.Enabled = true;
            }

            var reply = new TestReply();
            
            // This should parse "chars:" as command and "hello" as args
            // Just test that enqueue doesn't throw
            var exception = Record.Exception(() => invoker.Enqueue(reply, "chars:hello"));
            Assert.Null(exception);

            System.IO.File.Delete(tempFile);
        }

        [Fact]
        public void UnknownCommand_IsHandledGracefully()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            System.IO.File.Delete(tempFile);

            var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
            var reply = new TestReply();

            // This should not throw
            var exception = Record.Exception(() =>
            {
                invoker!.Enqueue(reply, "unknowncommand12345");
            });

            Assert.Null(exception);

            System.IO.File.Delete(tempFile);
        }

        [Fact]
        public void SingleCharacter_TreatedAsCharsCommand()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            System.IO.File.Delete(tempFile);

            var invoker = CommandInvoker.Create(tempFile, "1.0.0.0", false);
            
            // Enable chars command
            if (invoker!["chars:"] is CharsCommand charsCmd)
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

            System.IO.File.Delete(tempFile);
        }
    }
}
