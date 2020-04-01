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

namespace MCEControl {
    /// <summary>
    /// <Pause Cmd="name" Time="<time in ms>" />
    /// or
    /// pause:5000
    /// </summary>
    public class PauseCommand : Command {
        public const string CmdPrefix = "pause:";

        public static new List<Command> BuiltInCommands {
            get => new List<Command>() {
                new PauseCommand { Cmd = $"{CmdPrefix}" } // Commands that use form of "cmd:" must define a blank version
            };
        }

        public PauseCommand() { }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\" Args=\"{Args}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new PauseCommand());

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            if (int.TryParse(Args, out var time)) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Pausing {time}ms");
                // TODO: Is this the smartest way to do this?
                System.Threading.Thread.Sleep(time);
            }
            return true;
        }
    }
}
