﻿//-------------------------------------------------------------------
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

        public virtual void Execute(Reply reply) {
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
        [XmlArrayItem("StartProcess", typeof(StartProcessCommand))]
        [XmlArrayItem("SendInput", typeof(SendInputCommand))]
        [XmlArrayItem("SendMessage", typeof(SendMessageCommand))]
        [XmlArrayItem("SetForegroundWindow", typeof(SetForegroundWindowCommand))]
        [XmlArrayItem("Shutdown", typeof(ShutdownCommands))]
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
#pragma warning disable IDE0052 // Remove unread private members
        private FileSystemSafeWatcher fileWatcher;
#pragma warning restore IDE0052 // Remove unread private members

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    fileWatcher = null;
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

            if (!ignoreInternalCommands) {
                if (cmdString.StartsWith(McecCommand.CmdPrefix)) {
                    var cmd = new McecCommand(cmdString);
                    cmd.Execute(reply);
                    return;
                }

                if (cmdString.StartsWith("chars:")) {
                    // "chars:<chars>
                    String chars = Regex.Unescape(cmdString.Substring(6, cmdString.Length - 6));
                    Logger.Instance.Log4.Info($"Cmd: Sending {chars.Length} chars: {chars}");
                    var sim = new InputSimulator();
                    sim.Keyboard.TextEntry(chars);
                    return;
                }

                if (cmdString.StartsWith("api:")) {
                    // "api:API(params)
                    // TODO: Implement API stuff
                    return;
                }

                if (cmdString.StartsWith("shiftdown:")) {
                    // Modifyer key down
                    SendInputCommand.ShiftKey(cmdString.Substring(10, cmdString.Length - 10), true);
                    return;
                }

                if (cmdString.StartsWith("shiftup:")) {
                    // Modifyer key up
                    SendInputCommand.ShiftKey(cmdString.Substring(8, cmdString.Length - 8), false);
                    return;
                }

                if (cmdString.StartsWith(MouseCommands.CmdPrefix)) {
                    // mouse:<action>[,<parameter>,<parameter>]
                    var mouseCmd = new MouseCommands(cmdString);
                    mouseCmd.Execute(reply);
                    return;
                }

                if (cmdString.Length == 1) {
                    // It's a single character, just send it
                    // must be upper case (VirtualKeyCode codes are for upper case)
                    cmdString = cmdString.ToUpper(CultureInfo.InvariantCulture);
                    char c = cmdString.ToCharArray()[0];

                    var sim = new InputSimulator();

                    Logger.Instance.Log4.Info("Cmd: Sending keydown for: " + cmdString);
                    sim.Keyboard.KeyPress((VirtualKeyCode)c);
                    return;
                }
            }

            // Command is in .commands
            Command command = this[cmdString.ToUpper(CultureInfo.InvariantCulture)];
            if (command != null) {
                command.Execute(reply);
            }
            else {
                Logger.Instance.Log4.Info("Cmd: Unknown command: " + cmdString);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "None")]
        public static CommandTable Create(string userCommandsFile, bool disableInternalCommands) {
            CommandTable cmds;
            if (!disableInternalCommands) {
                // Load the built-in commands from an assembly resource
                XmlReader reader;
                try {
                    var serializer = new XmlSerializer(typeof(CommandTable));
                    reader =
                        new XmlTextReader(
                            Assembly.GetExecutingAssembly()
                                .GetManifestResourceStream("MCEControl.Resources.Builtin.commands"));
                    cmds = (CommandTable)serializer.Deserialize(reader);
                    foreach (var cmd in cmds.Commands) {
                        if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper(CultureInfo.InvariantCulture))) {
                            cmds.hashTable.Remove(cmd.Key.ToUpper(CultureInfo.InvariantCulture));
                        }
                        cmds.hashTable.Add(cmd.Key.ToUpper(CultureInfo.InvariantCulture), cmd);
                    }
                    reader.Dispose();
                }
                catch (Exception ex) {
                    MessageBox.Show($"No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                    Logger.Instance.Log4.Info($"Commands: No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                    ExceptionUtils.DumpException(ex);
                    return null;
                }

                // Add the built-ins
                foreach (Command cmd in McecCommand.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper(CultureInfo.InvariantCulture))) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper(CultureInfo.InvariantCulture));
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(CultureInfo.InvariantCulture), cmd);
                }

                foreach (Command cmd in MouseCommands.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper(CultureInfo.InvariantCulture))) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper(CultureInfo.InvariantCulture));
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(CultureInfo.InvariantCulture), cmd);
                }

                foreach (Command cmd in ShutdownCommands.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper(CultureInfo.InvariantCulture))) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper(CultureInfo.InvariantCulture));
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(CultureInfo.InvariantCulture), cmd);
                }

                // Populate default VK_ codes
                foreach (VirtualKeyCode vk in Enum.GetValues(typeof(VirtualKeyCode))) {
                    string s;
                    if (vk > VirtualKeyCode.HELP && vk < VirtualKeyCode.LWIN)
                        s = vk.ToString();  // already have VK_
                    else
                        s = "VK_" + vk.ToString();
                    var cmd = new SendInputCommand(s, false, false, false, false);
                    if (!cmds.hashTable.ContainsKey(s))
                        cmds.hashTable.Add(s, cmd);
                }
            }
            else {
                cmds = new CommandTable();
            }
            cmds.userCommandsFile = userCommandsFile;
            cmds.fileWatcher = cmds.CreateFileWatcher(userCommandsFile);
            cmds.ignoreInternalCommands = disableInternalCommands;
            Logger.Instance.Log4.Info($"Commands: {cmds.NumCommands} built-in commands enabled.");
            cmds.LoadUserCommands();
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
                foreach (var cmd in userCmds.Commands) {
                    if (hashTable.ContainsKey(cmd.Key.ToUpper(CultureInfo.InvariantCulture))) {
                        hashTable.Remove(cmd.Key.ToUpper(CultureInfo.InvariantCulture));
                    }
                    hashTable.Add(cmd.Key.ToUpper(CultureInfo.InvariantCulture), cmd);
                    Logger.Instance.Log4.Info($"Commands: User command added: {cmd.Key}");
                }
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
            Logger.Instance.Log4.Info($"Commands:{e.FullPath} changed. Reloading...");
            LoadUserCommands();
        }

        private void OnRenamed(object source, RenamedEventArgs e) {
            // Specify what is done when a file is renamed.
            Logger.Instance.Log4.Info($"Commands:{e.OldFullPath} renamed to {e.FullPath}");
        }


    }
}
