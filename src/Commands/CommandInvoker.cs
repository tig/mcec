//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
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

namespace MCEControl {
    /// <summary>
    /// Holds all active Commands. Uses a hash-table for lookup. 
    /// Is the Invoker in the Commands pattern.
    /// </summary>
#pragma warning disable CA1010 // Collections should implement generic interface
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public class CommandInvoker : Hashtable {
#pragma warning restore CA1710 // Identifiers should have correct suffix
#pragma warning restore CA1010 // Collections should implement generic interface
        private ConcurrentQueue<ICommand> executeQueue = new ConcurrentQueue<ICommand>();

        /// <summary>
        /// Creaates a `Commands` instance of default & built-in commands. 
        /// </summary>
        /// <returns></returns>
        private static CommandInvoker CreateBuiltIns(bool disableInternalCommands = false) {
            var commands = new CommandInvoker();

            // Add the built-ins that are defiend in the `Command`-derived classes
            // SECURITY: Note, by default `Enabled` is set to `false` for all of these.
            if (disableInternalCommands)
                return commands;

            // Add the built-ins defined in the Command-derived classes
            foreach (var cmdType in Command.GetDerivedClassesCollection()) {
                var propertyInfo = cmdType.GetType().GetProperty("BuiltInCommands", BindingFlags.Public | BindingFlags.Static);
                if (propertyInfo != null) {
                    // Use the PropertyInfo to retrieve the value from the type by not passing in an instance
                    foreach (var builtinCmd in (List<Command>)propertyInfo.GetValue(null, null)) {
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
        /// <param name="userCommandsFile">Path to MCEControl.commands file.</param>
        /// <param name="disableInternalCommands">If true, internal commands will not be added to created instance.</param>
        /// <returns></returns>
        public static CommandInvoker Create(string userCommandsFile, string currentVersion, bool disableInternalCommands) {
            CommandInvoker commands = null ;
            SerializedCommands serializedCmds;

            commands = CreateBuiltIns(disableInternalCommands);
            var nBuiltIn = commands.Count;

            // Load external .commands file. 
            serializedCmds = SerializedCommands.LoadCommands(userCommandsFile, currentVersion);
            if (serializedCmds != null && serializedCmds.commandArray != null) {
                foreach (var cmd in serializedCmds.commandArray) {
                    if (!string.IsNullOrWhiteSpace(cmd.Cmd)) {
                        // TELEMETRY: Mark user defined commands as such so they don't get collected
                        if (!commands.ContainsKey(cmd.Cmd)) {
                            cmd.UserDefined = true;
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
            Logger.Instance.Log4.Info($@"{GetType().Name}: Saving {Program.ConfigPath}MCEControl.commands...");
            var sc = new SerializedCommands();

            var values = Values.Cast<Command>().ToArray();

            // Sort 
            sc.commandArray = values.OrderBy(c => c.Cmd).ToArray();

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
            var args = "";

            // parse cmd and args (eg. char vs "shutdown" vs "mouse:<action>[,<parameter>,<parameter>]"
            // and "mouse:<action>[,<parameter>,<parameter>]"
            // These commands are handled internally as Cmd="cmd:" Args="<args>"
            var match = Regex.Match(cmdString, @"(\w+:)(.+)");
            if (match.Success) {
                cmd = match.Groups[1].Value;
                args = match.Groups[2].Value;
            }
            else {
                cmd = cmdString;
            }
            cmd = cmd.ToLowerInvariant();

            // TODO: Implement ignoreInternalCommands?

            if (cmdString.Length == 1 && ((Command)this["chars:"]).Enabled) {
                // Sending a single character is equivalent to a single key press of a key on the keyboard. 
                // For example sending a will result in the A key being pressed. 
                // 1 will result in the 1 key being pressed. There is no difference between sending a and A. 
                // Use shiftdown:/shiftup: to simulate the pressing of the shift, control, alt, and windows keys.
                var siCmd = new SendInputCommand() { Vk = cmdString, Enabled = true, Reply = reply };
                executeQueue.Enqueue(siCmd);
            }
            else {
                // See if we know about this Command - case insensitive
                if (this[cmd.ToLowerInvariant()] != null) {
                    // Always create a clone for enqueing (so Reply context can be independent)
                    var clone = (Command)((Command)this[cmd.ToLowerInvariant()]).Clone(reply);

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
        /// </summary>
        /// <param name="cmd">Command to enqueue</param>
        internal void EnqueueCommand(ICommand cmd) {
            executeQueue.Enqueue(cmd);
            if (((Command)cmd).EmbeddedCommands is null) {
                return;
            }

            foreach (var embedded in ((Command)cmd).EmbeddedCommands) {
                EnqueueCommand(embedded);
            }
        }


        /// <summary>
        /// Pulls the next Commeand off the queue and executes it
        /// </summary>
        internal void ExecuteNext() {
            // TODO: This is simple and just dequeues and executes anything on the queue
            // needs to be smarter? Will this block incoming?
            while (executeQueue.TryDequeue(out var icmd)) {
                ((Command)icmd).Execute();
                System.Threading.Thread.Sleep(MainWindow.Instance.Settings.CommandPacing);
            }
        }
    }
}
