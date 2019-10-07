//-------------------------------------------------------------------
// Copyright © 2017 Kindel Systems, LLC
//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
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
        [XmlAttribute("Arguments")] public String Arguments;
        [XmlAttribute("Verb")] public String Verb;

        [XmlElement("StartProcess", typeof(StartProcessCommand))]
        [XmlElement("SendInput", typeof(SendInputCommand))]
        [XmlElement("SendMessage", typeof(SendMessageCommand))]
        [XmlElement(typeof(Command))]
        public Command NextCommand;

        public StartProcessCommand() {
        }

        // TODO: This does not show embedded next commands
        public override string ToString() {
            return $"Cmd=\"{Key}\" File=\"{File}\" Arguments=\"{Arguments}\" Verb=\"{Verb}\"";
        }

        public override void Execute(Reply reply) {
            Logger.Instance.Log4.Info($"Cmd: Starting process: {ToString()}");
            if (File != null) {
                var p = new Process {
                    StartInfo = {
                        FileName = File,
                        Arguments = Arguments,
                        Verb = Verb,
                        UseShellExecute = true
                    },
                };
                //var v = p.StartInfo.Verbs;
                p.Start();
                if (NextCommand != null)
                    p.WaitForInputIdle(10000); // TODO: Make this settable
            }

            if (NextCommand != null)
                NextCommand.Execute(reply);
        }
    }
}
