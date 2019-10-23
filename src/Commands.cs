//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

//#define SERIALIZE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using menelabs.core;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl {
    // Base class for all Command types
    public class Command {
        private String key;
        [XmlAttribute("Cmd")]
        public string Key { get => key; set => key = value; }

        public override string ToString() => $"Cmd=\"{Key}\"";
        /// <summary>
        /// Called to execute the command. 
        /// </summary>
        /// <param name="args">Any text to the right of the command.</param>
        /// <param name="reply">Reply context (so replies get sent to the right socket)</param>
        public virtual void Execute(string args, Reply reply) {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Needed for XmlArray")]

    // Singleton class holding all commands
    // Note, do not change the namespace or you will break existing installations
    [XmlType(Namespace = "http://www.kindel.com/products/mcecontroller", TypeName = "MCEController")]
    public class CommandTable : IDisposable {
        [XmlIgnore] private readonly Hashtable hashTable = new Hashtable();

        [XmlIgnore] private string userCommandsFile;
        [XmlIgnore] private bool ignoreInternalCommands = false;

        [XmlArray("Commands")]
        [XmlArrayItem("Chars", typeof(CharsCommand))]
        [XmlArrayItem("StartProcess", typeof(StartProcessCommand))]
        [XmlArrayItem("SendInput", typeof(SendInputCommand))]
        [XmlArrayItem("SendMessage", typeof(SendMessageCommand))]
        [XmlArrayItem("SetForegroundWindow", typeof(SetForegroundWindowCommand))]
        [XmlArrayItem("Shutdown", typeof(ShutdownCommand))]
        [XmlArrayItem(typeof(Command))]
        public Command[] Commands { get => commands; set => commands = value; }
        private Command[] commands;

        [XmlIgnore]
        public virtual Command this[string key] {
            get => (Command)hashTable[key];
        }

        [XmlIgnore] public ICollection Keys { get => hashTable.Keys; }
        [XmlIgnore] public ICollection Values { get => hashTable.Values; }

        public CommandTable() {
        }

        public event EventHandler ChangedEvent;
        /// <summary>
        /// OnChangeEvent is raised whenever the CommandTable is updated due to
        /// user commands file changes
        /// </summary>
        protected virtual void OnChangedEvent() {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler handler = ChangedEvent;

            // Event will be null if there are no subscribers
            if (handler != null) {
                handler(this, null);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
#pragma warning disable IDE0052 // Remove unread private members
        private FileSystemSafeWatcher fileWatcher;
#pragma warning restore IDE0052 // Remove unread private members

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (fileWatcher != null) {
                        fileWatcher.Changed -= OnChanged;
                        fileWatcher.Created -= OnChanged;
                        fileWatcher.Deleted -= OnChanged;
                        fileWatcher.Renamed -= OnRenamed;
                        fileWatcher = null;
                    }
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CommandTable()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
        [XmlIgnore]
        public int NumCommands {
            get { return hashTable.Count; }
        }

        public void Execute(Reply reply, String cmdString) {
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

            // TODO: Implement ignoreInternalCommands

            if (cmdString.Length == 1) {
                // It's a single character, just send it
                SendInputCommand keydownCmd = new SendInputCommand(cmdString, false, false, false, false);

                Logger.Instance.Log4.Info($"Cmd: Sending keydown for: {cmdString}");
                keydownCmd.Execute(args, reply);
            }
            else {
                // Command is in .commands
                if (this[cmd.ToUpperInvariant()] != null) {
                    this[cmd.ToUpperInvariant()].Execute(args, reply);
                }
                else Logger.Instance.Log4.Info("Cmd: Unknown command: " + cmdString);
            }
        }

        public static CommandTable Create(string userCommandsFile, bool disableInternalCommands) {
            CommandTable cmds;
            if (!disableInternalCommands)
                cmds = CommandTable.LoadBuiltInCommands();
            else
                cmds = new CommandTable();
            Logger.Instance.Log4.Info($"Commands: {cmds.NumCommands} built-in commands enabled.");

            cmds.userCommandsFile = userCommandsFile;
            cmds.fileWatcher = cmds.CreateFileWatcher(userCommandsFile);
            cmds.ignoreInternalCommands = disableInternalCommands;
            cmds.LoadUserCommands();
            return cmds;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "None")]
        private static CommandTable LoadBuiltInCommands() {
            CommandTable cmds;
            // Load the built-in commands from an assembly resource
            XmlReader reader;
            try {
                var serializer = new XmlSerializer(typeof(CommandTable));
                reader =
                    new XmlTextReader(
                        Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.Builtin.commands"));
                cmds = (CommandTable)serializer.Deserialize(reader);
                foreach (var cmd in cmds.Commands)
                    cmds.Add(cmd);
                reader.Dispose();
            }
            catch (Exception ex) {
                MessageBox.Show($"No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                Logger.Instance.Log4.Info($"Commands: No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                ExceptionUtils.DumpException(ex);
                return null;
            }

            // Add the built-ins
            foreach (Command cmd in McecCommand.Commands)
                cmds.Add(cmd);

            foreach (Command cmd in MouseCommand.Commands)
                cmds.Add(cmd);

            foreach (Command cmd in ShutdownCommand.Commands)
                cmds.Add(cmd);

            foreach (Command cmd in CharsCommand.Commands)
                cmds.Add(cmd);

            foreach (Command cmd in SendInputCommand.Commands)
                cmds.Add(cmd);

            return cmds;
        }

        // Load any over-rides from a text file
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "None")]
        private void LoadUserCommands() {
            FileStream fs = null;

            try {
                var serializer = new XmlSerializer(typeof(CommandTable));
                // A FileStream is needed to read the XML document.
                fs = new FileStream(userCommandsFile, FileMode.Open, FileAccess.Read);
                XmlReader reader = new XmlTextReader(fs);
                CommandTable userCmds = (CommandTable)serializer.Deserialize(reader);
                Logger.Instance.Log4.Info($"Commands: Loading {userCmds.Commands.Length} user commands from {userCommandsFile}.");
                foreach (var cmd in userCmds.Commands)
                    Add(cmd, true);
                reader.Close();
            }
            catch (FileNotFoundException) {
                Logger.Instance.Log4.Info($"Commands: No user defined commands loaded; {userCommandsFile} was not found.");

                // If the user .commands file is not found, create it
                Stream uc = Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.commands");
                FileStream ucFS = null;
                try {
                    ucFS = new FileStream(userCommandsFile, FileMode.Create, FileAccess.ReadWrite);
                    uc.CopyTo(ucFS);
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Info($"Commands: Could not create default user defined commands file {userCommandsFile}. {e.Message}");
                    ExceptionUtils.DumpException(e);
                }
                finally {
                    if (uc != null) uc.Close();
                    if (ucFS != null) ucFS.Close();
                }
            }
            catch (InvalidOperationException ex) {
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing {userCommandsFile}. {ex.Message} {ex.InnerException.Message}");
                ExceptionUtils.DumpException(ex);
            }
            catch (Exception ex) {
                MessageBox.Show($"No commands loaded. Error parsing {userCommandsFile}. {ex.Message}");
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing {userCommandsFile}. {ex.Message}");
                ExceptionUtils.DumpException(ex);
            }
            finally {
                if (fs != null)
                    fs.Close();
            }
            OnChangedEvent();
        }

        private void Add(Command cmd, bool log = false) {
            if (!string.IsNullOrEmpty(cmd.Key)) {
                if (this.hashTable.ContainsKey(cmd.Key.ToUpperInvariant())) {
                    this.hashTable.Remove(cmd.Key.ToUpperInvariant());
                }
                this.hashTable.Add(cmd.Key.ToUpperInvariant(), cmd);
                if (log) Logger.Instance.Log4.Info($"Commands: Command added: {cmd.Key}");
            }
            else Logger.Instance.Log4.Info($"Commands: Error parsing command: {cmd.ToString()}");
        }

        //private static void GenerateXSD() {
        //    var schemas = new XmlSchemas();
        //    var exporter = new XmlSchemaExporter(schemas);
        //    var mapping = new XmlReflectionImporter().ImportTypeMapping(typeof(CommandTable));
        //    exporter.ExportTypeMapping(mapping);
        //    var schemaWriter = new StringWriter();
        //    foreach (System.Xml.Schema.XmlSchema schema in schemas) {
        //        schema.Write(schemaWriter);
        //    }

        //    using (FileStream fs = File.Create("MCEControl.xsd")) {
        //        byte[] info = new System.Text.UTF8Encoding(true).GetBytes(schemaWriter.ToString());
        //        fs.Write(info, 0, info.Length);
        //    }
        //}

        private FileSystemSafeWatcher CreateFileWatcher(string path) {

            // Create a new FileSystemSafeWatcher and set its properties.
            var watcher = new FileSystemSafeWatcher();
            watcher.Path = Path.GetDirectoryName(path);
            /* Watch for changes in LastAccess and LastWrite times, and 
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = Path.GetFileName(path);

            // Add event handlers.
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            // Begin watching.
            watcher.EnableRaisingEvents = true;
            Logger.Instance.Log4.Info($"Commands: Watching {watcher.Path}\\{watcher.Filter} for changes.");
            return watcher;

        }

        private void OnChanged(object source, FileSystemEventArgs e) {
            Logger.Instance.Log4.Info($"Commands:{e.FullPath} changed.");
            OnChangedEvent();
        }

        private void OnRenamed(object source, RenamedEventArgs e) {
            // Specify what is done when a file is renamed.
            Logger.Instance.Log4.Info($"Commands:{e.OldFullPath} renamed to {e.FullPath}");
        }


    }
}
