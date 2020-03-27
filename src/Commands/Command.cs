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
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl {
    // Implements Command Pattern
    public interface ICommand {
        /// <summary>
        /// Called to execute the command. 
        /// </summary>
        void Execute();

        Command Clone(Reply reply, Command clone);
    }

    /// <summary>
    /// Base class for all Command types
    /// IMPORANT: Be very careful changing this schema as it may break forward compat
    /// </summary>
    public abstract class Command : ICommand {
        private String cmd;
        [XmlAttribute("cmd")]
        public string Cmd { get => cmd; set => cmd = value; }

        private List<Command> embeddedCommands;
        [XmlElement("chars", typeof(CharsCommand))]
        [XmlElement("startprocess", typeof(StartProcessCommand))]
        [XmlElement("sendinput", typeof(SendInputCommand))]
        [XmlElement("sendmessage", typeof(SendMessageCommand))]
        [XmlElement("setforegroundwindow", typeof(SetForegroundWindowCommand))]
        [XmlElement("shutdown", typeof(ShutdownCommand))]
        [XmlElement("pause", typeof(PauseCommand))]
        [XmlElement("mouse", typeof(MouseCommand))]
        [XmlElement(typeof(Command))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Serializable")]
        public List<Command> EmbeddedCommands { get => embeddedCommands; set => embeddedCommands = value; }

        [XmlAttribute("args")]
        public virtual string Args { get => args; set => args = value; }
        public virtual Reply Reply { get => reply; set => reply = value; }

        public override string ToString() => $"Cmd=\"{Cmd}\" Args=\"{Args}\"";

        private string args = "";

        private Reply reply;

        // TELEMETRY:
        // Ensure only built-in command names are collected
        public bool UserDefined { get => userDefined; set => userDefined = value; }
        private bool userDefined = false;

        public abstract ICommand Clone(Reply reply);

        public virtual Command Clone(Reply reply, Command clone) {
            if (clone is null) 
                throw new ArgumentNullException(nameof(clone));

            clone.Reply = reply;

            clone.Cmd = this.Cmd;
            clone.Args = this.Args;
            if (this.EmbeddedCommands != null) {
                clone.EmbeddedCommands = new List<Command>();
                foreach (Command next in this.EmbeddedCommands) {
                    Command eClone = (Command)next.Clone(reply);
                    clone.EmbeddedCommands.Add(eClone);
                }
            }

            //TELEMETRY: Prevent info regarding user defined commands from being collected.
            clone.UserDefined = this.UserDefined;

            return clone;
        }

        public virtual void Execute() {
            // TELEMETRY: 
            // what: the number of commands of each type (key) received and executed
            // why: to understand what commands are used and which are not
            // how is PII protected: the name of the command, key, is not user definable
            TelemetryService.Instance.GetTelemetryClient().GetMetric($"{(UserDefined ? "<userDefined>" : cmd)} Executed").TrackValue(1);
        }
    }
}
