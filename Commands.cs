//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
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
using System.IO;
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

        public virtual void Execute(Reply reply) {
        }
    }

    // Note, do not change the namespace or your will break existing installations
    [XmlType(Namespace = "http://www.kindel.com/products/mcecontroller", TypeName = "MCEController")]
    public class CommandTable {
        [XmlIgnore] private readonly Hashtable _hashTable = new Hashtable();

        [XmlArray("Commands")] 
        [XmlArrayItem("StartProcess", typeof (StartProcessCommand))] 
        [XmlArrayItem("SendInput", typeof (SendInputCommand))] 
        [XmlArrayItem("SendMessage", typeof (SendMessageCommand))] 
        [XmlArrayItem("SetForegroundWindow", typeof (SetForegroundWindowCommand))] 
        [XmlArrayItem("Shutdown", typeof (ShutdownCommand))] 
        [XmlArrayItem(typeof (Command))] 
        public Command[] List;

        public CommandTable() {
            if (_hashTable == null) 
                _hashTable = new Hashtable();

            // Populate default VK_ codes
            foreach (VirtualKeyCode vk in Enum.GetValues(typeof(VirtualKeyCode))) {
                string s;
                if (vk > VirtualKeyCode.HELP && vk < VirtualKeyCode.LWIN)
                    s = vk.ToString();  // already have VK_
                else
                    s = "VK_" + vk.ToString(); 
                var cmd = new SendInputCommand(s, false, false, false, false);
                if (!_hashTable.ContainsKey(s))
                    _hashTable.Add(s, cmd);
            }
        }

        public int NumCommands {
            get { return _hashTable.Count; }
        }

        public void Execute(Reply reply, String cmd) {
            if (cmd.StartsWith(McecCommand.CmdPrefix)) {
                var command = new McecCommand(cmd);
                command.Execute(reply);
            }
            else if (cmd.StartsWith("chars:")) {
                // "chars:<chars>
                String chars = Regex.Unescape(cmd.Substring(6, cmd.Length - 6));
                MainWindow.AddLogEntry(String.Format("Cmd: Sending {0} chars: {1}", chars.Length, chars));
                var sim = new InputSimulator();
                sim.Keyboard.TextEntry(chars);
            }
            else if (cmd.StartsWith("api:")) {
                // "api:API(params)
                // TODO: Implement API stuff
            }
            else if (cmd.StartsWith("shiftdown:")) {
                // Modifyer key down
                SendInputCommand.ShiftKey(cmd.Substring(10, cmd.Length - 10), true);
            }
            else if (cmd.StartsWith("shiftup:")) {
                // Modifyer key up
                SendInputCommand.ShiftKey(cmd.Substring(8, cmd.Length - 8), false);
            }
            else if (cmd.StartsWith(MouseCommand.CmdPrefix)) {
                // mouse:<action>[,<parameter>,<parameter>]
                var mouseCmd = new MouseCommand(cmd);
                mouseCmd.Execute(reply);
            }
            else if (_hashTable.ContainsKey(cmd.ToUpper())) {
                // Command in MCEControl.commands
                Command command = FindKey(cmd.ToUpper());
                command.Execute(reply);
            }
            else if (cmd.Length == 1) {
                // It's a single character, just send it
                // must be upper case (VirtualKeyCode codes are for upper case)
                cmd = cmd.ToUpper();
                char c = cmd.ToCharArray()[0];

                var sim = new InputSimulator();

                MainWindow.AddLogEntry("Cmd: Sending keydown for: " + cmd);
                sim.Keyboard.KeyPress((VirtualKeyCode) c);
            }
            else {
                MainWindow.AddLogEntry("Cmd: Unknown Cmd: " + cmd);
            }
        }

        private Command FindKey(String key) {
            if (_hashTable.ContainsKey(key))
                return (Command) _hashTable[key];
            return null;
        }

        public static CommandTable Deserialize() {
            CommandTable cmds = null;
            FileStream fs = null;

            try {
                var serializer = new XmlSerializer(typeof (CommandTable));
                // A FileStream is needed to read the XML document.
                fs = new FileStream("MCEControl.commands", FileMode.Open, FileAccess.Read);
                XmlReader reader = new XmlTextReader(fs);
                cmds = (CommandTable) serializer.Deserialize(reader);
                foreach (var cmd in cmds.List) {
                    if (cmds._hashTable.ContainsKey(cmd.Key.ToUpper())) {
                        cmds._hashTable.Remove(cmd.Key.ToUpper());
                    }
                    cmds._hashTable.Add(cmd.Key.ToUpper(), cmd);
                }
            }
            catch (FileNotFoundException ex) {
                MainWindow.AddLogEntry(
                    String.Format(
                        "MCEC: No commands loaded. Make sure MCEControl.commands is in the program directory and restart. {0}",
                        ex.Message));
                Util.DumpException(ex);
            }
            catch (InvalidOperationException ex) {
                MainWindow.AddLogEntry(
                    String.Format("MCEC: No commands loaded. Error parsing MCEControl.commands file. {0} {1}", ex.Message,
                                  ex.InnerException.Message));
                Util.DumpException(ex);
            }
            catch (Exception ex) {
                MessageBox.Show(String.Format("No commands loaded. Error parsing MCEControl.commands file. {0}",
                                              ex.Message));
                MainWindow.AddLogEntry(String.Format("MCEC: No commands loaded. Error parsing MCEControl.commands file. {0}",
                                                     ex.Message));
                Util.DumpException(ex);
            }
            finally {
                if (fs != null)
                    fs.Close();
            }

            return cmds;
        }
    }
}