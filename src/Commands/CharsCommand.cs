//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using WindowsInput;
using WindowsInput.Native;
using MCEControl;

namespace MCEControl {
    /// <summary>
    /// Supports sending raw text.
    /// </summary>
    public class CharsCommand : Command {
        public const string CmdPrefix = "chars:";

        public static new List<CharsCommand> BuiltInCommands {
            get => new List<CharsCommand>() {
                new CharsCommand { Cmd = $"{CmdPrefix}" } 
            };
        }

        public CharsCommand() { }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new CharsCommand());

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) return false;

            string text;
            // if command came in as a literal "chars:foo" command use args
            // otherwise, use the Chars property
            if (!string.IsNullOrEmpty(Args))
                text = Regex.Unescape(Args);
            else
                text = "";

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Typing {text.Length} chars: {text}");
            new InputSimulator().Keyboard.TextEntry(text);
            return true;
        }
    }
}
