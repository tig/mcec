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

        Command Clone(Reply reply, string args = null);
    }

    // Base class for all Command types
    public abstract class Command : ICommand {
        private String key;
        [XmlAttribute("Cmd")]
        public string Key { get => key; set => key = value; }
        public virtual string Args { get => args; set => args = value; }
        public virtual Reply Reply { get => reply; set => reply = value; }

        public override string ToString() => $"Cmd=\"{Key}\"";

        private string args;

        private Reply reply;

        public abstract Command Clone(Reply reply, string args = null);

        public abstract void Execute();
    }
}
