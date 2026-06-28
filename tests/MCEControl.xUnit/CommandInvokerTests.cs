using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MCEControl.xUnit;

public class CommandInvokerTests
{
    // NOTE: This test is disabled because it uses MSTest PrivateType which is not available in .NET Core
    // TODO: Refactor to use reflection or make the tested members internal with InternalsVisibleTo
    /*
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
    */

    [Fact]
    public void Create_Test()
    {
        string tempCommandsFile = Path.GetTempFileName();
        File.Delete(tempCommandsFile);

        CommandInvoker? commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);
        Assert.NotEmpty(commands);
    }

    [Fact]
    public void Create_NoneEnabled_Test()
    {
        string tempCommandsFile = Path.GetTempFileName();
        File.Delete(tempCommandsFile);

        CommandInvoker? commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);

        Assert.DoesNotContain(commands.Values.Cast<Command>(), cmd => cmd.Enabled);
    }

    /// <summary>
    /// Test create where there were user-defined new commands
    /// </summary>
    [Fact]
    public void Create_RetainUserNewCommand_Test()
    {
        string userCommandsFile = Path.GetTempFileName();
        File.Delete(userCommandsFile);
        SerializedCommands userCommands = new SerializedCommands()
        {
            // Version = ...,
            commandArray = new Command[]
            {
                new SendMessageCommand("class", "window", 0, 0, 0) { Cmd = "userCmd" }
            }
        };
        SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

        CommandInvoker? commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

        Assert.Contains(commands.Values.Cast<Command>(), cmd => cmd.Cmd.Equals("userCmd"));
    }

    /// <summary>
    /// Test create where there were user-defined changes to builtin commands
    /// </summary>
    [Fact]
    public void Create_RetainUserModifiedCommand_Test()
    {
        string userCommandsFile = Path.GetTempFileName();
        File.Delete(userCommandsFile);
        SerializedCommands userCommands = new SerializedCommands()
        {
            // Version = ...,
            commandArray = new Command[]
            {
              // Change both File (from "code" to "codez" and) and add Verb
              new StartProcessCommand() { Cmd = "code", File ="codez", Verb="print" },
            }
        };
        SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

        CommandInvoker? commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

        StartProcessCommand? codeCmd = commands["code"] as StartProcessCommand;
        Assert.NotNull(codeCmd);
        Assert.Equal("codez", codeCmd.File);
        Assert.Equal("print", codeCmd.Verb);
    }
}
