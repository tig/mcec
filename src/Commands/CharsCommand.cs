//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System.Collections.Generic;
using WindowsInput;

namespace MCEControl;
/// <summary>
/// Supports sending raw text.
/// </summary>
public class CharsCommand : Command {
    private const string CmdPrefix = "chars:";

    public static List<Command> BuiltInCommands {
        get => [
            new CharsCommand { Cmd = $"{CmdPrefix}" },
        ];
    }

    /// <summary>
    /// The LITERAL text <c>chars:</c> types: <paramref name="args"/> verbatim (empty when null/blank).
    /// It deliberately does NO escape processing (#269). It used to run <c>Regex.Unescape</c>, which
    /// silently mangled any argument containing backslashes; a Windows path like
    /// <c>C:\Users\tig\file.txt</c> had its <c>\t</c> turned into a TAB and other <c>\x</c> sequences
    /// eaten, so agents had to double every backslash. <c>chars:</c> is text entry, not an escape-coded
    /// string; typing the argument as-is is the least-surprising behavior. Pure; unit-testable.
    /// </summary>
    internal static string PrepareText(string? args) => args ?? "";

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        string text = PrepareText(Args);

        Logger.Instance.Log4.Info($"{this.GetType().Name}: Typing {text.Length} chars: {text}");

        // TODO: Change this such that it treats each char of `text` as a keydown/keyup
        // pair vs how `TextEntry()` currently works. See Issue #14.
        // OR, implement a new command, keyboard: where Keyboard.KeyPress(vk) is used for
        // each character.
        new InputSimulator().Keyboard.TextEntry(text);
        return true;
    }
}
