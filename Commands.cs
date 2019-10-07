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
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl {
    // Base class for all Command types
    public class Command {
        [XmlAttribute("Cmd")] public String Key;

        public virtual string ToString() {
            return $"Cmd=\"{Key}\"";
        }
        public virtual void Execute(Reply reply) {
        }
    }

    // Singleton class holding all commands
    // Note, do not change the namespace or you will break existing installations
    [XmlType(Namespace = "http://www.kindel.com/products/mcecontroller", TypeName = "MCEController")]
    public class CommandTable {
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
        public Command[] Commands;

        public CommandTable() {
        }

        [XmlIgnore] public int NumCommands {
            get { return hashTable.Count; }
        }

        [XmlIgnore] public ICollection Keys { get => hashTable.Keys; }
        [XmlIgnore] public ICollection Values { get => hashTable.Values; }
        [XmlIgnore] public virtual Command this[string key] {
            get => (Command)hashTable[key];
        }


        public void Execute(Reply reply, String cmdString) {
            if (!ignoreInternalCommands) {
                if (cmdString.StartsWith(McecCommand.CmdPrefix)) {
                    var cmd = new McecCommand(cmdString);
                    cmd.Execute(reply);
                    return;
                }

                if (cmdString.StartsWith("chars:")) {
                    // "chars:<chars>
                    String chars = Regex.Unescape(cmdString.Substring(6, cmdString.Length - 6));
                    Logger.Instance.Log4.Info(String.Format("Cmd: Sending {0} chars: {1}", chars.Length, chars));
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
                    cmdString = cmdString.ToUpper();
                    char c = cmdString.ToCharArray()[0];

                    var sim = new InputSimulator();

                    Logger.Instance.Log4.Info("Cmd: Sending keydown for: " + cmdString);
                    sim.Keyboard.KeyPress((VirtualKeyCode)c);
                    return;
                }
            }

            // Command is in .commands
            Command command = this[cmdString.ToUpper()];
            if (command != null) {
                command.Execute(reply);
            }
            else {
                Logger.Instance.Log4.Info("Cmd: Unknown command: " + cmdString);
            }
        }

        public static CommandTable Create(string userCommandsFile, bool disableInternalCommands) {
            CommandTable cmds = null;

            if (!disableInternalCommands) {
                // Load the built-in commands from an assembly resource
                try {
                    var serializer = new XmlSerializer(typeof(CommandTable));
                    XmlReader reader =
                        new XmlTextReader(
                            Assembly.GetExecutingAssembly()
                                .GetManifestResourceStream("MCEControl.Resources.Builtin.commands"));
                    cmds = (CommandTable)serializer.Deserialize(reader);
                    foreach (var cmd in cmds.Commands) {
                        if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper())) {
                            cmds.hashTable.Remove(cmd.Key.ToUpper());
                        }
                        cmds.hashTable.Add(cmd.Key.ToUpper(), cmd);
                    }
                }
                catch (Exception ex) {
                    MessageBox.Show($"No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                    Logger.Instance.Log4.Info($"Commands: No built-in commands loaded. Error parsing built-in commands. {ex.Message}");
                    Util.DumpException(ex);
                    return null;
                }

                // Add the built-ins
                foreach (Command cmd in McecCommand.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper())) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper());
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(), cmd);
                }

                foreach (Command cmd in MouseCommands.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper())) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper());
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(), cmd);
                }

                foreach (Command cmd in ShutdownCommands.Commands) {
                    if (cmds.hashTable.ContainsKey(cmd.Key.ToUpper())) {
                        cmds.hashTable.Remove(cmd.Key.ToUpper());
                    }
                    cmds.hashTable.Add(cmd.Key.ToUpper(), cmd);
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
            cmds.ignoreInternalCommands = disableInternalCommands;
            Logger.Instance.Log4.Info($"Commands: {cmds.NumCommands} built-in commands enabled.");

            cmds.CreateFileWatcher(userCommandsFile);
            cmds.LoadUserCommands();
            return cmds;
        }

        // Load any over-rides from a text file
        private void LoadUserCommands() {
            FileStream fs = null;
            XmlReader reader = null;
            var serializer = new XmlSerializer(typeof(CommandTable));

            try {
                
                // A FileStream is needed to read the XML document.
                fs = new FileStream(userCommandsFile, FileMode.Open, FileAccess.Read);
                reader = new XmlTextReader(fs);
                CommandTable userCmds = (CommandTable)serializer.Deserialize(reader);
                Logger.Instance.Log4.Info($"Commands: Loading user commands from {userCommandsFile}.");
                foreach (var cmd in userCmds.Commands) {
                    if (hashTable.ContainsKey(cmd.Key.ToUpper())) {
                        hashTable.Remove(cmd.Key.ToUpper());
                    }
                    hashTable.Add(cmd.Key.ToUpper(), cmd);
                    Logger.Instance.Log4.Info($"Commands: User command added: {cmd.Key}");
                }
            }
            catch (FileNotFoundException ex) {
                Logger.Instance.Log4.Info($"Commands: No user defined commands loaded; {userCommandsFile} was not found.");

                // If the user .commands file is not found, create it
                Stream uc = Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.commands");
                FileStream ucFS = null;
                try {
                    ucFS = new FileStream(userCommandsFile, FileMode.Create, FileAccess.ReadWrite);
                    uc.CopyTo(ucFS);
                    uc.Close();
                    ucFS.Close();
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Info($"Commands: Could not create default user defined commands file {userCommandsFile}. {e.Message}");
                    Util.DumpException(e);
                }
                finally {
                    if (uc != null) uc.Close();
                    if (ucFS != null) ucFS.Close();
                }
            }
            catch (InvalidOperationException ex) {
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing {userCommandsFile}. {ex.Message} {ex.InnerException.Message}");
                Util.DumpException(ex);
            }
            catch (Exception ex) {
                MessageBox.Show($"No commands loaded. Error parsing {userCommandsFile}. {ex.Message}");
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing {userCommandsFile}. {ex.Message}");
                Util.DumpException(ex);
            }
            finally {
                if (serializer != null)
                    serializer = null;
                if (reader != null)
                    reader.Close();
                if (fs != null) 
                    fs.Close();
            }
        }

        private void CreateFileWatcher(string path) {
            // Create a new FileSystemSafeWatcher and set its properties.
            menelabs.core.FileSystemSafeWatcher watcher = new menelabs.core.FileSystemSafeWatcher();
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
        }

        // https://stackoverflow.com/questions/1764809/filesystemwatcher-changed-event-is-raised-twice
        [XmlIgnore] private List<string> _changedFiles = new List<string>();
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
