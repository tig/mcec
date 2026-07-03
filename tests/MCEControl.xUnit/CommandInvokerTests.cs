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
    // NOTE (#204): a long-dead commented-out CreateBuiltIns_Test lived here, written against the
    // old reflection dance (Command.GetDerivedClassesCollection + magic BuiltInCommands statics),
    // both now deleted. CommandRegistryTests covers built-in registration against the explicit
    // CommandRegistry.

    [Fact]
    public void Create_Test()
    {
        string tempCommandsFile = Path.GetTempFileName();
        File.Delete(tempCommandsFile);

        CommandInvoker commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);
        Assert.NotEmpty(commands);
    }

    [Fact]
    public void Create_NoneEnabled_Test()
    {
        string tempCommandsFile = Path.GetTempFileName();
        File.Delete(tempCommandsFile);

        CommandInvoker commands = CommandInvoker.Create(tempCommandsFile, "0.0.0.0", false);

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
            commandArray =
            [
                new SendMessageCommand("class", "window", 0, 0, 0) { Cmd = "userCmd" }
            ]
        };
        SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

        CommandInvoker commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

        Assert.Contains(commands.Values.Cast<Command>(), cmd => cmd.Cmd.Equals("userCmd"));
    }

    /// <summary>
    /// #203: a single-character payload used to NRE when the registry had no "chars:"
    /// prototype (DisableInternalCommands, or a user .commands file without it). It must
    /// fall through to normal unknown-command handling instead of throwing.
    /// </summary>
    [Fact]
    public void Enqueue_SingleChar_NoCharsCommand_DoesNotThrow()
    {
        CommandInvoker invoker = [];
        Assert.Null(invoker["chars:"]);

        invoker.Enqueue(new Commands.TestReply(), "a");

        Assert.Equal(0, invoker.QueuedCommandCount);
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
            commandArray =
            [
              // Change both File (from "code" to "codez" and) and add Verb
              new StartProcessCommand() { Cmd = "code", File ="codez", Verb="print" },
            ]
        };
        SerializedCommands.SaveCommands(userCommandsFile, userCommands, "0.0.0.0");

        CommandInvoker commands = CommandInvoker.Create(userCommandsFile, "0.0.0.0", false);

        StartProcessCommand? codeCmd = commands["code"] as StartProcessCommand;
        Assert.NotNull(codeCmd);
        Assert.Equal("codez", codeCmd.File);
        Assert.Equal("print", codeCmd.Verb);
    }
}
