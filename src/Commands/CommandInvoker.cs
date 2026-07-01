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
    /// (memory DoS) while freezing the UI. Commands enqueued beyond this depth are dropped and logged.
    /// </summary>
    internal const int MaxQueueDepth = 200;

    /// <summary>
    /// SECURITY (#154): Maximum number of commands a single enqueue may expand to via
    /// `EmbeddedCommands` fan-out. A single received command string can otherwise amplify ~10x or
    /// more (see #145), letting one packet flood the queue. Expansion beyond this bound is
    /// truncated and logged. The largest shipped built-in (`type_into_notepad`) expands to 10.
    /// </summary>
    internal const int MaxEmbeddedExpansion = 20;

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
            SendInputCommand siCmd = new SendInputCommand() { Vk = cmdString, Enabled = true, Reply = reply };
            TryEnqueue(siCmd);
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
    /// SECURITY (#154): the total expansion (command plus all recursively embedded commands) is
    /// bounded by `MaxEmbeddedExpansion` and the overall queue depth by `MaxQueueDepth`; anything
    /// beyond either bound is dropped and logged.
    /// </summary>
    /// <param name="cmd">Command to enqueue</param>
    internal void EnqueueCommand(ICommand cmd) {
        int enqueued = 0;
        bool truncationLogged = false;
        EnqueueCommandTree(cmd, ref enqueued, ref truncationLogged);
    }

    // Recursively enqueues `cmd` and its EmbeddedCommands, counting every item enqueued from the
    // single root passed to EnqueueCommand so the fan-out bound applies to the whole tree.
    private void EnqueueCommandTree(ICommand cmd, ref int enqueued, ref bool truncationLogged) {
        if (enqueued >= MaxEmbeddedExpansion) {
            if (!truncationLogged) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: embedded command expansion exceeded {MaxEmbeddedExpansion}; remaining embedded commands truncated (dropped).");
                truncationLogged = true;
            }
            return;
        }

        if (!TryEnqueue(cmd)) {
            // Queue is full; don't descend into embedded commands (they'd be dropped anyway).
            return;
        }
        enqueued++;

        if (((Command)cmd).EmbeddedCommands is null) {
            return;
        }

        foreach (Command embedded in ((Command)cmd).EmbeddedCommands) {
            EnqueueCommandTree(embedded, ref enqueued, ref truncationLogged);
        }
    }

    // SECURITY (#154): single choke-point for adding to the execute queue. Drops (and logs) the
    // command if the queue is at `MaxQueueDepth` — the queue drains pacing-limited on the UI
    // thread, so without a cap a remote sender can grow it without bound (memory/CPU DoS).
    private bool TryEnqueue(ICommand cmd) {
        if (executeQueue.Count >= MaxQueueDepth) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: execute queue is full ({MaxQueueDepth}); command dropped: {((Command)cmd).Cmd}");
            return false;
        }

        executeQueue.Enqueue(cmd);
        return true;
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
