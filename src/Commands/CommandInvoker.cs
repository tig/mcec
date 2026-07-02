//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Octokit;

namespace MCEControl; 
/// <summary>
/// Holds all active Commands. Uses a hash-table for lookup. 
/// Is the Invoker in the Commands pattern.
/// </summary>
#pragma warning disable CA1010 // Collections should implement generic interface
#pragma warning disable CA1710 // Identifiers should have correct suffix
public class CommandInvoker : Hashtable {
#pragma warning restore CA1710 // Identifiers should have correct suffix
#pragma warning restore CA1010 // Collections should implement generic interface
    private ConcurrentQueue<ICommand> executeQueue = new();

    /// <summary>
    /// SECURITY (#154): Maximum number of commands the execute queue will hold. The queue is drained
    /// synchronously on the UI thread with `CommandPacing` sleeps between items, so a remote client
    /// that enqueues faster than the paced drain would otherwise grow the queue without bound
    /// (memory DoS) while freezing the UI. A command whose tree does not fit in the remaining
    /// capacity is dropped WHOLE (all-or-nothing, see `EnqueueCommand`) and logged.
    /// </summary>
    internal const int MaxQueueDepth = 200;

    /// <summary>
    /// SECURITY (#154): Maximum size of a single command's whole tree — the command itself plus all
    /// recursively embedded commands. A single received command string can otherwise amplify ~10x or
    /// more (see #145), letting one packet flood the queue. A tree over this bound is dropped WHOLE
    /// (all-or-nothing, see `EnqueueCommand`) and logged. 50 leaves generous headroom for authored
    /// macros (the largest shipped built-in, `type_into_notepad`, expands to 10) while still
    /// bounding the amplification.
    /// </summary>
    internal const int MaxEmbeddedExpansion = 50;

    /// <summary>
    /// Current number of commands waiting to be executed. Exposed for tests.
    /// </summary>
    internal int QueuedCommandCount => executeQueue.Count;

    /// <summary>
    /// Creaates a `Commands` instance of default & built-in commands. 
    /// </summary>
    /// <returns></returns>
    private static CommandInvoker CreateBuiltIns(bool disableInternalCommands = false) {
        CommandInvoker commands = [];

        // Add the built-ins that are defiend in the `Command`-derived classes
        // SECURITY: Note, by default `Enabled` is set to `false` for all of these.
        if (disableInternalCommands)
            return commands;

        // Add the built-ins defined in the Command-derived classes
        foreach (Command cmdType in Command.GetDerivedClassesCollection()) {
            PropertyInfo? propertyInfo = cmdType.GetType().GetProperty("BuiltInCommands", BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null) {
                // Use the PropertyInfo to retrieve the value from the type by not passing in an instance
                foreach (Command builtinCmd in (List<Command>)propertyInfo.GetValue(null, null)!) {
                    commands.Add(builtinCmd);
                }
            }
        }
        Logger.Instance.Log4.Info($"{commands.GetType().Name}: {commands.Count} built-in commands defined");
        return commands;
    }
    /// <summary>
    /// Creaates a `Commands` instance from a combination of an external .commands file and the built-in commands.
    /// </summary>
    /// <param name="userCommandsFile">Path to mcec.commands file.</param>
    /// <param name="disableInternalCommands">If true, internal commands will not be added to created instance.</param>
    /// <returns></returns>
    public static CommandInvoker Create(string userCommandsFile, string currentVersion, bool disableInternalCommands) {
        CommandInvoker commands = null!;
        SerializedCommands serializedCmds;

        commands = CreateBuiltIns(disableInternalCommands);
        int nBuiltIn = commands.Count;

        // Load external .commands file. 
        serializedCmds = SerializedCommands.LoadCommands(userCommandsFile, currentVersion);
        if (serializedCmds != null && serializedCmds.commandArray != null) {
            foreach (Command cmd in serializedCmds.commandArray) {
                if (!string.IsNullOrWhiteSpace(cmd.Cmd)) {
                    // TELEMETRY: Mark user defined commands as such so they don't get collected
                    if (!commands.ContainsKey(cmd.Cmd)) {
                        cmd.UserDefined = true;
                    }

                    if (cmd.Enabled) {
                        if (cmd.UserDefined) {
#if DEBUG
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: User defined command enabled: '{cmd}'");
#else
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: User defined command enabled: '****'");
#endif
                        }
                        else {
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: Builtin command enabled: '{cmd}'");
                        }
                    }
                    commands.Add(cmd);
                }
                else {
                    Logger.Instance.Log4.Error($"{commands.GetType().Name}: Cmd name can't be blank or whitespace ({cmd})");
                }
            }
            Logger.Instance.Log4.Info($"{commands.GetType().Name}: {serializedCmds.Count} commands loaded");
        }

        // TELEMETRY: 
        // what: Collect number of user defined commands created. 
        // why: To determine how often users actaully create user commands, if at all.
        TelemetryService.Instance.TrackEvent("User Commands Created",
            properties: new Dictionary<string, string> {
                {"userCommands", $"{commands.Values.Cast<Command>().GroupBy(o => o.UserDefined)}" }
                });

        return commands;
    }

    internal void Save(string userCommandsFile) {
        Logger.Instance.Log4.Info($@"{GetType().Name}: Saving {Program.ConfigPath}mcec.commands...");
        SerializedCommands sc = new SerializedCommands();

        Command[] values = [.. Values.Cast<Command>()];

        // Sort 
        sc.commandArray = [.. values.OrderBy(c => c.Cmd)];

        SerializedCommands.SaveCommands(userCommandsFile, sc, System.Windows.Forms.Application.ProductVersion);
    }

    // Adds a command to the hashtable. Optionally logs. Ensures case insenstiitivy. 
    private void Add(Command cmd, bool log = false) {
        if (!string.IsNullOrEmpty(cmd.Cmd)) {
            if (this.ContainsKey(cmd.Cmd.ToLowerInvariant())) {
                this.Remove(cmd.Cmd.ToLowerInvariant());
            }
            this.Add(cmd.Cmd.ToLowerInvariant(), (ICommand)cmd);
            if (log) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Command added: {cmd.Cmd}");
            }
        }
        else {
            Logger.Instance.Log4.Error($"{this.GetType().Name}: Error parsing command: {cmd}");
        }
    }

    /// <summary>
    /// Decodes a commands tring and enqueues the associated Command for execution.
    /// </summary>
    /// <param name="reply">Reply context</param>
    /// <param name="cmdString">The command string that was received</param>
    public void Enqueue(Reply reply, String cmdString) {
        if (cmdString == null) {
            throw new ArgumentNullException(nameof(cmdString));
        }

        string cmd;
        string args = "";

        // parse cmd and args (eg. char vs "shutdown" vs "mouse:<action>[,<parameter>,<parameter>]"
        // and "mouse:<action>[,<parameter>,<parameter>]"
        // These commands are handled internally as Cmd="cmd:" Args="<args>"
        Match match = Regex.Match(cmdString, @"(\w+:)(.+)");
        if (match.Success) {
            cmd = match.Groups[1].Value;
            args = match.Groups[2].Value;
        }
        else {
            cmd = cmdString;
        }
        cmd = cmd.ToLowerInvariant();

        // TODO: Implement ignoreInternalCommands?

        if (cmdString.Length == 1 && ((Command)this["chars:"]!).Enabled) {
            // Sending a single character is equivalent to a single key press of a key on the keyboard. 
            // For example sending a will result in the A key being pressed. 
            // 1 will result in the 1 key being pressed. There is no difference between sending a and A. 
            // Use shiftdown:/shiftup: to simulate the pressing of the shift, control, alt, and windows keys.
            // Cmd is set so a drop of this command (queue full, #154) logs something identifiable.
            SendInputCommand siCmd = new SendInputCommand() { Cmd = cmdString, Vk = cmdString, Enabled = true, Reply = reply };
            EnqueueCommand(siCmd);
        }
        else {
            // See if we know about this Command - case insensitive
            if (this[cmd.ToLowerInvariant()] != null) {
                // Always create a clone for enqueing (so Reply context can be independent)
                Command clone = (Command)((Command)this[cmd.ToLowerInvariant()]!).Clone(reply);

                // This supports commands of the form 'chars:args'; these
                // commands do not need to originate in CommandTable
                if (string.IsNullOrEmpty(clone.Args)) {
                    clone.Args = args;
                }

                EnqueueCommand(clone);
            }
            else {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Unknown command: {cmdString}");
            }
        }
    }

    /// <summary>
    /// Enques a Command for execution. Recursively enques embedded commands.
    /// SECURITY (#154): enqueue is ALL-OR-NOTHING per command tree. The whole tree (this command
    /// plus all recursively embedded commands) is counted first; if it exceeds
    /// `MaxEmbeddedExpansion` or does not fit in the queue's remaining capacity (`MaxQueueDepth`),
    /// the ENTIRE tree is dropped with a warning and nothing is enqueued. A partial enqueue must
    /// never happen: it could split paired input commands (e.g. shiftdown:/shiftup:) and leave a
    /// modifier key latched host-wide.
    /// </summary>
    /// <param name="cmd">Command to enqueue</param>
    internal void EnqueueCommand(ICommand cmd) {
        int treeSize = CountCommandTree(cmd);

        if (treeSize > MaxEmbeddedExpansion) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: command expands to {treeSize} commands, over the {MaxEmbeddedExpansion} bound; whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
            return;
        }

        if (executeQueue.Count + treeSize > MaxQueueDepth) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: execute queue cannot hold {treeSize} more command(s) ({executeQueue.Count}/{MaxQueueDepth} queued); whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
            return;
        }

        EnqueueCommandTree(cmd);
    }

    // Counts a command's whole tree: itself plus all recursively embedded commands.
    private static int CountCommandTree(ICommand cmd) {
        int count = 1;
        if (((Command)cmd).EmbeddedCommands is null) {
            return count;
        }

        foreach (Command embedded in ((Command)cmd).EmbeddedCommands) {
            count += CountCommandTree(embedded);
        }
        return count;
    }

    // Recursively enqueues `cmd` and its EmbeddedCommands. Only called after EnqueueCommand has
    // verified the whole tree fits both bounds (#154) — never call this directly.
    private void EnqueueCommandTree(ICommand cmd) {
        executeQueue.Enqueue(cmd);
        Command command = (Command)cmd;
        if (command.EmbeddedCommands is null) {
            return;
        }

        // SECURITY (#145): a disabled parent must suppress its entire embedded subtree. Embedded
        // commands are flattened into the execute queue as independent siblings and each is gated
        // only on its OWN Enabled flag, so descending into a disabled parent would let its
        // Enabled=true children run — bypassing the per-command gate that "disabled by default"
        // depends on. Stop descending unless this command is itself enabled. (An enabled parent
        // with a disabled child still stops at that child, because the recursion re-checks here.)
        if (!command.Enabled) {
            return;
        }

        foreach (Command embedded in command.EmbeddedCommands) {
            EnqueueCommandTree(embedded);
        }
    }


    /// <summary>
    /// Pulls the next Commeand off the queue and executes it
    /// </summary>
    internal void ExecuteNext() {
        // TODO: This is simple and just dequeues and executes anything on the queue
        // needs to be smarter? Will this block incoming?
        while (executeQueue.TryDequeue(out ICommand? icmd)) {
            // Emergency stop (#135): if the operator engaged the panic hotkey, drop the rest of the queue
            // instead of actuating it. A paced/embedded command sequence (a macro, or commands after a
            // `pause`) must not keep firing after the stop — checking the latch BETWEEN commands is what
            // makes "the queue is dropped" true rather than only latching future tool calls.
            if (AgentRuntime.EmergencyStopped) {
                int dropped = 1; // the command we just dequeued and are NOT running
                while (executeQueue.TryDequeue(out _)) {
                    dropped++;
                }
                Logger.Instance.Log4.Warn($"{GetType().Name}: emergency stop engaged — dropped {dropped} queued command(s) without executing.");
                break;
            }

            ((Command)icmd).Execute();
            // Read pacing via the UI-agnostic AgentRuntime seam so the engine works headless
            // (--mcp) where there is no MainWindow. In GUI mode this is the same settings object.
            System.Threading.Thread.Sleep(AgentRuntime.Settings?.CommandPacing ?? 0);
        }
    }
}
