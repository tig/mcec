using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using mstest = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MCEControl.xUnit
{
    public class CommandInvokerTests
    {
        [Fact]
        public void CreateBuiltIns_Test()
        {
            // There are currently 9 classses derived from Command
            var cmdTypes = Command.GetDerivedClassesCollection();
            Assert.Equal(9, cmdTypes.Count);

            // Get # of built in commands defined
            var builtIns = new List<Command>();
            foreach (var cmdType in cmdTypes)
            {
                mstest.PrivateType cmdPt = new mstest.PrivateType(cmdType.GetType());
                List<Command> listOfType = (List<Command>)cmdPt.GetStaticProperty(nameof(Command.BuiltInCommands));
                foreach (var c in listOfType)
                    builtIns.Add(c);
            }

            // Ensure there are no duplicates
            var query = builtIns.GroupBy(x => x.Cmd)
              .Where(g => g.Count() > 1)
              .Select(y => y.Key)
              .ToList();
            Assert.Empty(query);

            // Invoke the CreateBuiltIns method and compare result to expected
            // https://stackoverflow.com/questions/9122708/unit-testing-private-methods-in-c-sharp
            CommandInvoker target = new CommandInvoker();
            mstest.PrivateType pt = new mstest.PrivateType(typeof(CommandInvoker));
            CommandInvoker returnedBuiltIns = (CommandInvoker)pt.InvokeStatic("CreateBuiltIns", false);
            Assert.NotEmpty(returnedBuiltIns);
            Assert.Equal(builtIns.Count, returnedBuiltIns.Count);

            // Now test with disableBuiltIns true
            returnedBuiltIns = (CommandInvoker)pt.InvokeStatic("CreateBuiltIns", true);
            Assert.Empty(returnedBuiltIns);
        }

        [Fact]
        public void Create_Test()
        {
            var tempCommandsFile = Path.GetTempFileName();
            File.Delete(tempCommandsFile);

            var commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);
            Assert.NotEmpty(commands);
        }

        [Fact]
        public void Create_NoneEnabled_Test()
        {
            var tempCommandsFile = Path.GetTempFileName();
            File.Delete(tempCommandsFile);

            var commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);

            Assert.Empty(commands.Values.Cast<Command>().Where(cmd => cmd.Enabled));
        }

        /// <summary>
        /// Test create where there were user-defined new commands
        /// </summary>
        [Fact]
        public void Create_RetainUserNewCommand_Test()
        {
            var userCommandsFile = Path.GetTempFileName();
            File.Delete(userCommandsFile);
            var userCommands = new SerializedCommands()
            {
                // Version = ...,
                commandArray = new Command[]
                {
                    new SendMessageCommand("class", "window", 0, 0, 0) { Cmd = "userCmd" }
                }
            };
            SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

            var commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

            Assert.NotEmpty(commands.Values.Cast<Command>().Where(cmd => cmd.Cmd.Equals("userCmd")));
        }

        /// <summary>
        /// Test create where there were user-defined changes to builtin commands
        /// </summary>
        [Fact]
        public void Create_RetainUserModifiedCommand_Test()
        {
            var userCommandsFile = Path.GetTempFileName();
            File.Delete(userCommandsFile);
            var userCommands = new SerializedCommands()
            {
                // Version = ...,
                commandArray = new Command[]
                {
                  // Change both File (from "code" to "codez" and) and add Verb
                  new StartProcessCommand() { Cmd = "code", File ="codez", Verb="print" },
                }
            };
            SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

            var commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

            var codeCmd = (StartProcessCommand)commands["code"];
            Assert.Equal("codez", codeCmd.File);
            Assert.Equal("print", codeCmd.Verb);
        }
    }
}
