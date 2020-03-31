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
        bool Execute();

        Command Clone(Reply reply, Command clone);
    }

    /// <summary>
    /// Base class for all Command types
    /// IMPORANT: Be very careful changing this schema as it may break forward compat
    /// </summary>
    public abstract class Command : ICommand {
        private String cmd;

        protected Command() {
            Enabled = false; // SECURITY: Explicity
            UserDefined = false; // TELEMERTRY: Explicit
        }
        public static List<Command> BuiltInCommands { get => new List<Command>() { }; }

        [XmlAttribute("cmd")]
        public string Cmd { get => cmd; set => cmd = value; }
        [XmlElement("chars", typeof(CharsCommand))]
        [XmlElement("startprocess", typeof(StartProcessCommand))]
        [XmlElement("sendinput", typeof(SendInputCommand))]
        [XmlElement("sendmessage", typeof(SendMessageCommand))]
        [XmlElement("setforegroundwindow", typeof(SetForegroundWindowCommand))]
        [XmlElement("shutdown", typeof(ShutdownCommand))]
        [XmlElement("pause", typeof(PauseCommand))]
        [XmlElement("mouse", typeof(MouseCommand))]
        [XmlElement("mceccommand", typeof(McecCommand))]
        [XmlElement(typeof(Command))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Serializable")]
        public List<Command> EmbeddedCommands { get; set; }

        [XmlAttribute("args")]
        public virtual string Args { get; set; }

        [XmlAttribute("enabled")]
        public bool Enabled { get; set; }
        public virtual Reply Reply { get; set; }

        public override string ToString() => $"Cmd=\"{Cmd}\" Args=\"{Args}\"";

        // TELEMETRY:
        // Ensure only built-in command names are collected
        [XmlIgnore]
        public bool UserDefined { get; set; }

        public abstract ICommand Clone(Reply reply);

        public virtual Command Clone(Reply reply, Command clone) {
            if (clone is null)
                throw new ArgumentNullException(nameof(clone));

            clone.Reply = reply;

            clone.Cmd = this.Cmd;
            clone.Args = this.Args;
            if (this.EmbeddedCommands != null) {
                clone.EmbeddedCommands = new List<Command>();
                foreach (var next in this.EmbeddedCommands) {
                    var eClone = (Command)next.Clone(reply);
                    clone.Enabled = Enabled;
                    clone.EmbeddedCommands.Add(eClone);
                }
            }

            //TELEMETRY: Prevent info regarding user defined commands from being collected.
            clone.UserDefined = this.UserDefined;

            clone.Enabled = this.Enabled;

            return clone;
        }

        /// <summary>
        /// Execute command. Derived classes must call base before processing in order to ensure
        /// only enabled commands get run, and to collect telemetry.
        /// </summary>
        /// <returns></returns>
        public virtual bool Execute() {
            if (!Enabled) {
                Logger.Instance.Log4.Info($"Command: Attempt to execute a disabled command ({Cmd})");
                Logger.Instance.Log4.Info($"         As of MCE Controller v2.2.1 commands are disabled by default.");
                Logger.Instance.Log4.Info($"         Edit MCEControl.commands to enable commands (change `Enabled=\"false\"' to 'Enabled=\"true\"').");
                return false;
            }
            // TELEMETRY: 
            // what: the number of commands of each type (key) received and executed
            // why: to understand what commands are used and which are not
            // how is PII protected: the name of the command, key, is not user definable
            TelemetryService.Instance.TelemetryClient.GetMetric($"{(UserDefined ? "<userDefined>" : cmd)} Executed").TrackValue(1);
            return true;
        }
    }
}
