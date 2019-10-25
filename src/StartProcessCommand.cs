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
        [XmlAttribute("file")] public string File { get => file; set => file = value; }
        private String arguments;
        [XmlAttribute("arguments")] public string Arguments { get => arguments; set => arguments = value; }
        private String verb;
        [XmlAttribute("verb")] public string Verb { get => verb; set => verb = value; }

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

        public override ICommand Clone(Reply reply) => base.Clone(reply, new StartProcessCommand() {
            File = this.File,
            Arguments = this.Arguments,
            Verb = this.Verb
        });

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Process is long lived")]
        // ICommand:Execute
        public override void Execute() {
            if (this.Reply is null) throw new InvalidOperationException("Reply property cannot be null.");

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Starting process: {ToString()}");
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
                        p.WaitForInputIdle(1000); // TODO: Make this settable
                    }
                    catch (System.InvalidOperationException e) {
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: {e.Message}");
                    }
            }
        }
    }
}
