//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System.Collections.Generic;

namespace MCEControl; 
/// <summary>
/// <Pause Cmd="name" Time="<time in ms>" />
/// or
/// pause:5000
/// </summary>
public class PauseCommand : Command {
    public const string CmdPrefix = "pause:";

    public static new List<Command> BuiltInCommands {
        get => [
            new PauseCommand { Cmd = $"{CmdPrefix}" } // Commands that use form of "cmd:" must define a blank version
        ];
    }

    public PauseCommand() { }

    /// <summary>
    /// A pause only sleeps — it can never synthesize input — so the dispatcher (#195) must not hold
    /// <see cref="AgentRuntime.InputGate"/> for its whole duration (a <c>pause:60000</c> would starve
    /// a concurrent agent <c>drag</c> for a minute).
    /// </summary>
    internal override bool SynthesizesInput => false;

    public override string ToString() {
        return $"Cmd=\"{Cmd}\" Args=\"{Args}\"";
    }

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (int.TryParse(Args, out int time)) {
            Logger.Instance.Log4.Info($"{this.GetType().Name}: Pausing {time}ms");
            // TODO: Is this the smartest way to do this?
            System.Threading.Thread.Sleep(time);
        }
        return true;
    }
}
