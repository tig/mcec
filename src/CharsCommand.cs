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

namespace MCEControl {
    /// <summary>
    /// Supports sending raw text.
    /// </summary>
    public class CharsCommand : Command {
        public const string CmdPrefix = "chars:";
        private string chars;
        [XmlAttribute("Chars")] public string Chars { get => chars; set => chars = value; }
        public static List<CharsCommand> Commands { get => commands; }

        private static List<CharsCommand> commands = new List<CharsCommand>();
        static CharsCommand() {
            Commands.Add(new CharsCommand { Key = $"{CmdPrefix}" });
        }

        public CharsCommand() { }

        public override string ToString() {
            return $"Cmd=\"{Key}\" Chars=\"{Chars}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new CharsCommand() { Chars = this.Chars });

        // ICommand:Execute
        public override void Execute() {
            string text;
            // if command came in as a literal "chars:foo" command use args
            // otherwise, use the Chars property
            if (!string.IsNullOrEmpty(Args))
                text = Regex.Unescape(Args);
            else if (!string.IsNullOrEmpty(Chars))
                text = Regex.Unescape(Chars);
            else
                text = "";

            Logger.Instance.Log4.Info($"Cmd: Sending {text.Length} chars: {text}");
            new InputSimulator().Keyboard.TextEntry(text);
        }
    }
}
