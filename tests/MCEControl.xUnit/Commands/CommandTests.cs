using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands
{
    public class CommandTests
    {
        private class TestCommand : Command
        {
            public bool ExecuteCalled { get; private set; }

            public TestCommand()
            {
                Enabled = true; // Enable for testing
            }

            public override ICommand Clone(Reply? reply)
            {
                return base.Clone(reply, new TestCommand());
            }

            public override bool Execute()
            {
                // Don't call base.Execute() to avoid TelemetryService dependency
                if (!Enabled)
                    return false;

                ExecuteCalled = true;
                return true;
            }
        }

        [Fact]
        public void Constructor_DisabledByDefault()
        {
            var cmd = new TestCommand();
            // We override this in TestCommand, so test with a real command
            var pauseCmd = new PauseCommand();
            Assert.False(pauseCmd.Enabled);
        }

        [Fact]
        public void Constructor_UserDefinedIsFalse()
        {
            var cmd = new TestCommand();
            Assert.False(cmd.UserDefined);
        }

        [Fact]
        public void Execute_WhenDisabled_ReturnsFalse()
        {
            var cmd = new TestCommand { Enabled = false };
            bool result = cmd.Execute();
            Assert.False(result);
            Assert.False(cmd.ExecuteCalled);
        }

        [Fact]
        public void Execute_WhenEnabled_CallsExecuteLogic()
        {
            var cmd = new TestCommand { Enabled = true };
            bool result = cmd.Execute();
            Assert.True(result);
            Assert.True(cmd.ExecuteCalled);
        }

        [Fact]
        public void Clone_CopiesProperties()
        {
            var original = new TestCommand
            {
                Cmd = "test",
                Args = "testargs",
                Enabled = true,
                UserDefined = true
            };

            var clone = (TestCommand)original.Clone(null);

            Assert.Equal(original.Cmd, clone.Cmd);
            Assert.Equal(original.Args, clone.Args);
            Assert.Equal(original.Enabled, clone.Enabled);
            Assert.Equal(original.UserDefined, clone.UserDefined);
        }

        [Fact]
        public void Clone_WithReply_SetsReplyContext()
        {
            var original = new TestCommand();
            var mockReply = new TestReply();

            var clone = (TestCommand)original.Clone(mockReply);

            Assert.Same(mockReply, clone.Reply);
        }

        [Fact]
        public void Clone_WithEmbeddedCommands_ClonesEmbedded()
        {
            var original = new TestCommand
            {
                Cmd = "parent",
                EmbeddedCommands = new List<Command>
                {
                    new PauseCommand { Cmd = "pause", Args = "100", Enabled = true },
                    new TestCommand { Cmd = "child", Enabled = true }
                }
            };

            var clone = (TestCommand)original.Clone(null);

            Assert.NotNull(clone.EmbeddedCommands);
            Assert.Equal(2, clone.EmbeddedCommands.Count);
            Assert.Equal("pause", clone.EmbeddedCommands[0].Cmd);
            Assert.Equal("child", clone.EmbeddedCommands[1].Cmd);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var cmd = new TestCommand
            {
                Cmd = "testcmd",
                Args = "testargs"
            };

            string result = cmd.ToString();

            Assert.Contains("testcmd", result);
            Assert.Contains("testargs", result);
        }

        [Fact]
        public void GetDerivedClassesCollection_ReturnsAllCommandTypes()
        {
            var commandTypes = Command.GetDerivedClassesCollection();

            Assert.NotEmpty(commandTypes);
            
            // Should include all standard command types
            Assert.Contains(commandTypes, c => c is SendInputCommand);
            Assert.Contains(commandTypes, c => c is CharsCommand);
            Assert.Contains(commandTypes, c => c is MouseCommand);
            Assert.Contains(commandTypes, c => c is StartProcessCommand);
            Assert.Contains(commandTypes, c => c is PauseCommand);
            Assert.Contains(commandTypes, c => c is ShutdownCommand);
            Assert.Contains(commandTypes, c => c is SendMessageCommand);
            Assert.Contains(commandTypes, c => c is SetForegroundWindowCommand);
            Assert.Contains(commandTypes, c => c is McecCommand);
        }

        private class TestReply : Reply
        {
            public override void Write(string text)
            {
                // No-op for testing
            }
        }
    }
}
