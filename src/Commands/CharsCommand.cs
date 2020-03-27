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

        public static List<CharsCommand> Commands { get => commands; }

        private static List<CharsCommand> commands = new List<CharsCommand>();
        static CharsCommand() {
            Commands.Add(new CharsCommand { Cmd = $"{CmdPrefix}" });
        }

        public CharsCommand() { }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new CharsCommand());

        // ICommand:Execute
        public override void Execute() {
            base.Execute();
            string text;
            // if command came in as a literal "chars:foo" command use args
            // otherwise, use the Chars property
            if (!string.IsNullOrEmpty(Args))
                text = Regex.Unescape(Args);
            else
                text = "";

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending {text.Length} chars: {text}");
            new InputSimulator().Keyboard.TextEntry(text);
        }
    }
}
