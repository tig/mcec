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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MCEControl {
    
    /// <summary>
    /// Holds all active Commands. Uses a hash-table for lookup. 
    /// Is the Invoker in the Commands pattern.
    /// </summary>
    public class Commands : Hashtable {
        private ConcurrentQueue<ICommand> executeQueue = new ConcurrentQueue<ICommand>();

        public static Commands Create(string userCommandsFile, bool disableInternalCommands) {
            Commands commands = new Commands();
            SerializedCommands serializedCmds;

            // Start with the .commands file that's built in as an EXE resource
            if (!disableInternalCommands)
                serializedCmds = SerializedCommands.LoadBuiltInCommands();
            else
                serializedCmds = new SerializedCommands();

            foreach (Command cmd in serializedCmds.commandArray)
                commands.Add(cmd);
            Logger.Instance.Log4.Info($"{commands.GetType().Name}: {serializedCmds.Count} commands loaded from built-in .commands resource.");

            // Add the built-ins
            foreach (Command cmd in McecCommand.Commands)
                commands.Add(cmd);

            foreach (Command cmd in MouseCommand.Commands)
                commands.Add(cmd);

            foreach (Command cmd in ShutdownCommand.Commands)
                commands.Add(cmd);

            foreach (Command cmd in CharsCommand.Commands)
                commands.Add(cmd);

            foreach (Command cmd in SendInputCommand.Commands)
                commands.Add(cmd);

            foreach (Command cmd in PauseCommand.Commands)
                commands.Add(cmd);
            Logger.Instance.Log4.Info($"{commands.GetType().Name}: {commands.Count} built-in commands defined.");

            // Load external .commands file
            serializedCmds = SerializedCommands.LoadUserCommands(userCommandsFile);
            if (serializedCmds != null) {
                foreach (Command cmd in serializedCmds.commandArray)
                    commands.Add(cmd, true);
                Logger.Instance.Log4.Info($"{commands.GetType().Name}: {serializedCmds.Count} user-defined commands loaded.");
            }

            return commands;
        }

        // Adds a command to the hashtable. Optionally logs. Ensures case insenstiitivy. 
        private void Add(Command cmd, bool log = false) {
            if (!string.IsNullOrEmpty(cmd.Key)) {
                if (this.ContainsKey(cmd.Key.ToUpperInvariant())) {
                    this.Remove(cmd.Key.ToUpperInvariant());
                }
                this.Add(cmd.Key.ToUpperInvariant(), (ICommand)cmd);
                if (log) Logger.Instance.Log4.Info($"{this.GetType().Name}: Command added: {cmd.Key}");
            }
            else Logger.Instance.Log4.Info($"{this.GetType().Name}: Error parsing command: {cmd.ToString()}");
        }
        
        /// <summary>
        /// Decodes a commands tring and enqueues the associated Command for execution.
        /// </summary>
        /// <param name="reply">Reply context</param>
        /// <param name="cmdString">The command string that was received</param>
        public void Enqueue(Reply reply, String cmdString) {
            if (cmdString == null) throw new ArgumentNullException(nameof(cmdString));
            string cmd;
            string args = "";

            // parse cmd and args (eg. char vs "shutdown" vs "mouse:<action>[,<parameter>,<parameter>]"
            //"mouse:<action>[,<parameter>,<parameter>]"
            Match match = Regex.Match(cmdString, @"(\w+:)(.+)");
            if (match.Success) {
                cmd = match.Groups[1].Value;
                args = match.Groups[2].Value;
            }
            else {
                cmd = cmdString;
            }
            cmd = cmd.ToUpperInvariant();

            // TODO: Implement ignoreInternalCommands?

            if (cmdString.Length == 1) {
                // It's a single character, just send it by creating a SendInputCommand
                SendInputCommand keydownCmd = new SendInputCommand(cmdString, false, false, false, false);
                keydownCmd.Args = args;
                keydownCmd.Reply = reply;

                Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending keydown for: {cmdString}");
                executeQueue.Enqueue(keydownCmd);
            }
            else {
                // See if we know about this Command - case insensitive
                if (this[cmd.ToUpperInvariant()] != null) {
                    // Always create a clone for enqueing (so Reply context can be independent)
                    Command clone = (Command)((Command)this[cmd.ToUpperInvariant()]).Clone(reply);

                    // This supports commands of the form 'chars:args'; these
                    // commands do not need to originate in CommandTable
                    if (string.IsNullOrEmpty(clone.Args))
                        clone.Args = args;

                    EnqueueCommand(clone);
                }
                else
                    Logger.Instance.Log4.Info($"{this.GetType().Name}: Unknown command: {cmdString}");
            }
        }

        /// <summary>
        /// Enques a Command for execution. Recursively enques embedded commands.
        /// </summary>
        /// <param name="cmd">Command to enqueue</param>
        internal void EnqueueCommand(ICommand cmd) {
            executeQueue.Enqueue(cmd);
            if (((Command)cmd).EmbeddedCommands is null) return;
            foreach (var embedded in ((Command)cmd).EmbeddedCommands)
                EnqueueCommand(embedded);
        }


        /// <summary>
        /// Pulls the next Commeand off the queue and executes it
        /// </summary>
        internal void ExecuteNext() {
            ICommand icmd;
            // TODO: This is simple and just dequeues and executes anything on the queue
            // needs to be smarter? Will this block incoming?
            while (executeQueue.TryDequeue(out icmd)) {
                Command c = (Command)icmd;
                c.Execute();
                System.Threading.Thread.Sleep(MainWindow.Instance.Settings.CommandPacing);
            }
        }

    }
}
