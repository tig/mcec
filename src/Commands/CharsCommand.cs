//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System.Collections.Generic;
using System.Text.RegularExpressions;
using WindowsInput;

namespace MCEControl {
    /// <summary>
    /// Supports sending raw text.
    /// </summary>
    public class CharsCommand : Command {
        public const string CmdPrefix = "chars:";

        public static new List<Command> BuiltInCommands {
            get => new List<Command>() {
                new CharsCommand { Cmd = $"{CmdPrefix}" },
            };
        }

        public CharsCommand() { }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new CharsCommand());

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            string text;
            // if command came in as a literal "chars:foo" command use args
            // otherwise, use the Chars property
            if (!string.IsNullOrEmpty(Args)) {
                text = Regex.Unescape(Args);
            }
            else {
                text = "";
            }

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Typing {text.Length} chars: {text}");

            // TODO: Change this such that it treats each char of `text` as a keydown/keyup
            // pair vs how `TextEntry()` currently works. See Issue #14.
            // OR, implement a new command, keyboard: where Keyboard.KeyPress(vk) is used for
            // each character.
            new InputSimulator().Keyboard.TextEntry(text);
            return true;
        }
    }
}
