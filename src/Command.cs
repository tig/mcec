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
using System.Collections.Concurrent;
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
    // Implements Command Pattern
    public interface ICommand {
        /// <summary>
        /// Called to execute the command. 
        /// </summary>
        void Execute();

        Command Clone(Reply reply, Command icmd);
    }

    // Base class for all Command types
    public abstract class Command : ICommand {
        private String key;
        [XmlAttribute("cmd")]
        public string Key { get => key; set => key = value; }

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

        public override string ToString() => $"Cmd=\"{Key}\" Args=\"{Args}\"";

        private string args = "";

        private Reply reply;

        public abstract ICommand Clone(Reply reply);

        public virtual Command Clone(Reply reply, Command clone) {
            if (clone is null) 
                throw new ArgumentNullException(nameof(clone));

            clone.Reply = reply;

            clone.Key = this.Key;
            clone.Args = this.Args;
            if (this.EmbeddedCommands != null) {
                clone.EmbeddedCommands = new List<Command>();
                foreach (Command next in this.EmbeddedCommands) {
                    Command eClone = (Command)next.Clone(reply);
                    clone.EmbeddedCommands.Add(eClone);
                }
            }
            return clone;
        }

        public abstract void Execute();
    }
}
