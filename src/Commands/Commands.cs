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
using System.Text.RegularExpressions;

namespace MCEControl {
    /// <summary>
    /// Holds all active Commands. Uses a hash-table for lookup. 
    /// Is the Invoker in the Commands pattern.
    /// </summary>
    public class Commands : Hashtable {
        private ConcurrentQueue<ICommand> executeQueue = new ConcurrentQueue<ICommand>();

        /// <summary>
        /// Creaates a `Commands` instance of default & built-in commands. 
        /// </summary>
        /// <returns></returns>
        private static Commands CreateBuiltIns() {
            var commands = new Commands();

            // Add the built-ins defined in the Command-derived classes
            foreach (Command cmd in McecCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in MouseCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in ShutdownCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in CharsCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in SendInputCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in PauseCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in StartProcessCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (var cmd in SetForegroundWindowCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            foreach (Command cmd in SendMessageCommand.BuiltInCommands) {
                commands.Add(cmd);
            }

            Logger.Instance.Log4.Info($"{commands.GetType().Name}: {commands.Count} built-in commands defined");

            // Load the .commands file that's built in as an EXE resource
            // SerializedCommands serializedCmds;
            //serializedCmds = SerializedCommands.LoadBuiltInCommands();
            //foreach (Command cmd in serializedCmds.commandArray)
            //    commands.Add(cmd);
            //Logger.Instance.Log4.Info($"{commands.GetType().Name}: {serializedCmds.Count} commands loaded from built-in .commands resource");

            return commands;
        }
        /// <summary>
        /// Creaates a `Commands` instance from a combination of an external .commands file and the built-in commands.
        /// </summary>
        /// <param name="userCommandsFile">Path to MCEControl.commands file.</param>
        /// <param name="disableInternalCommands">If true, internal commands will not be added to created instance.</param>
        /// <returns></returns>
        public static Commands Create(string userCommandsFile, bool disableInternalCommands) {
            var commands = new Commands();
            SerializedCommands serializedCmds;

            // Add the built-ins that are defiend in the `Command`-derived classes
            // SECURITY: `Enabled` is set to `false` for all of these.
            if (!disableInternalCommands) {
                foreach (var cmd in CreateBuiltIns().Values.Cast<Command>()) {
                    commands.Add(cmd);
                }
            }

            var nBuiltIn = commands.Count;

            // Load external .commands file. 
            serializedCmds = SerializedCommands.LoadCommands(userCommandsFile);
            if (serializedCmds != null && serializedCmds.commandArray != null) {
                foreach (var cmd in serializedCmds.commandArray) {
                    // TELEMETRY: Mark user defined commands as such so they don't get collected
                    if (!commands.ContainsKey(cmd.Cmd)) {
                        cmd.UserDefined = true;
                    }

                    commands.Add(cmd);
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
            Logger.Instance.Log4.Info($@"Commands: Saving {Program.ConfigPath}MCEControl.commands...");
            var sc = new SerializedCommands();

            var values = Values.Cast<Command>().ToArray();

            // Sort 
            sc.commandArray = values.OrderBy(c => c.Cmd).ToArray();

            SerializedCommands.SaveCommands(userCommandsFile, sc);
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
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Error parsing command: {cmd.ToString()}");
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
                var charsCmd = new CharsCommand() { Args = cmdString, Enabled = true, Reply = reply };
                executeQueue.Enqueue(charsCmd);
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
