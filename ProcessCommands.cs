//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for StartProcessCommands.
    /// </summary>
    public class StartProcessCommand : Command {
        [XmlAttribute("File")] public String File;

        [XmlElement("StartProcess", typeof (StartProcessCommand))] 
        [XmlElement("SendInput", typeof (SendInputCommand))] 
        [XmlElement("SendMessage", typeof (SendMessageCommand))] 
        [XmlElement(typeof (Command))] 
        public Command NextCommand;

        public StartProcessCommand() {
        }

        public StartProcessCommand(String file) {
            File = file;
        }

        public StartProcessCommand(String file, Command cmd) : this(file) {
            NextCommand = cmd;
        }

        public override void Execute(Reply reply) {
            MainWindow.AddLogEntry("Cmd: Starting process: " + File);
            if (File != null) {
                var p = new Process {StartInfo = {FileName = File}};
                p.Start();
                if (NextCommand != null)
                    p.WaitForInputIdle(10000); // TODO: Make this settable
            }

            if (NextCommand != null)
                NextCommand.Execute(reply);
        }
    }
}