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
        private String file;
        [XmlAttribute("File")] public string File { get => file; set => file = value; }
        private String arguments;
        [XmlAttribute("Arguments")] public string Arguments { get => arguments; set => arguments = value; }
        private String verb;
        [XmlAttribute("Verb")] public string Verb { get => verb; set => verb = value; }

        private Command nextCommand;
        [XmlElement("StartProcess", typeof(StartProcessCommand))]
        [XmlElement("SendInput", typeof(SendInputCommand))]
        [XmlElement("SendMessage", typeof(SendMessageCommand))]
        [XmlElement(typeof(Command))]
        public Command NextCommand { get => nextCommand; set => nextCommand = value; }
                    
        public StartProcessCommand() {
        }

        // TODO: This does not show embedded next commands
        public override string ToString() {
            return $"Cmd=\"{Key}\" File=\"{File}\" Arguments=\"{Arguments}\" Verb=\"{Verb}\"";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Process is long lived")]
        public override void Execute(string args, Reply reply) {
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
                NextCommand.Execute(args, reply);
        }
    }
}
