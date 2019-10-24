//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
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

        private List<Command> embeddedCommands;
        [XmlElement("Chars", typeof(CharsCommand))]
        [XmlElement("StartProcess", typeof(StartProcessCommand))]
        [XmlElement("SendInput", typeof(SendInputCommand))]
        [XmlElement("SendMessage", typeof(SendMessageCommand))]
        [XmlElement("SetForegroundWindow", typeof(SetForegroundWindowCommand))]
        [XmlElement("Shutdown", typeof(ShutdownCommand))]
        [XmlElement(typeof(Command))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Serializable")]
        public List<Command> EmbeddedCommands { get => embeddedCommands; set => embeddedCommands = value; }

        public StartProcessCommand() {
        }

        //// Deal with ensuring NextCommand has the right reply context
        //public override Reply Reply {
        //    get => base.Reply; set {
        //        if (NextCommand != null)
        //            NextCommand.Reply = value;
        //        this.Reply = value;
        //    }
        //}

        // TODO: This does not show embedded next commands
        public override string ToString() {
            return $"Cmd=\"{Key}\" File=\"{File}\" Arguments=\"{Arguments}\" Verb=\"{Verb}\"";
        }

        public override Command Clone(Reply reply, string args = null) {
            StartProcessCommand cmd = new StartProcessCommand() {
                Reply = reply,
                Args = args,
                File = this.File,
                Arguments = this.Arguments,
                Verb = this.Verb
            };
            if (this.EmbeddedCommands != null) {
                cmd.EmbeddedCommands = new List<Command>();
                foreach (var next in this.EmbeddedCommands)
                    cmd.EmbeddedCommands.Add(next.Clone(reply, args));
            }
            return cmd;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Process is long lived")]
        // ICommand:Execute
        public override void Execute() {

            if (this.Reply is null) throw new InvalidOperationException("Reply property cannot be null.");
            if (this.Args is null) throw new InvalidOperationException("Args property cannot be null.");

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

                // TODO: Make delay smarter
                p.Start();
                if (EmbeddedCommands != null && EmbeddedCommands.Count > 0)
                    try {
                        p.WaitForInputIdle(50000); // TODO: Make this settable
                    }
                    catch (System.InvalidOperationException e) {
                        System.Threading.Thread.Sleep(5000);
                    }
            }

            if (EmbeddedCommands != null) {
                foreach (var cmd in EmbeddedCommands) cmd.Execute();
            }
        }
    }
}
