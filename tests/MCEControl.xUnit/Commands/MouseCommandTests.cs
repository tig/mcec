using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands
{
    public class MouseCommandTests
    {
        [Fact]
        public void Constructor_SetsDefaultProperties()
        {
            var cmd = new MouseCommand();
            Assert.NotNull(cmd);
            Assert.False(cmd.Enabled);
        }

        [Fact]
        public void BuiltInCommands_ContainsMouseCommands()
        {
            var builtIns = MouseCommand.BuiltInCommands;
            Assert.NotEmpty(builtIns);
            Assert.Contains(builtIns, c => c.Cmd == "mouse:");
        }

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new MouseCommand
            {
                Cmd = "mouse:",
                Args = "left",
                Enabled = true
            };

            var clone = (MouseCommand)original.Clone(null);

            Assert.Equal(original.Cmd, clone.Cmd);
            Assert.Equal(original.Args, clone.Args);
            Assert.Equal(original.Enabled, clone.Enabled);
            Assert.NotSame(original, clone);
        }

        [Fact]
        public void Execute_WhenDisabled_ReturnsFalse()
        {
            var cmd = new MouseCommand
            {
                Cmd = "mouse:",
                Args = "left",
                Enabled = false
            };

            bool result = cmd.Execute();
            Assert.False(result);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var cmd = new MouseCommand
            {
                Cmd = "mouse:"
            };

            string result = cmd.ToString();
            Assert.Contains("mouse:", result);
            // ToString uses base Command.ToString() which includes Cmd and Args
            // Args is empty by default, so just verify Cmd is present
        }
    }
}
