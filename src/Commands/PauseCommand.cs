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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// <Pause Cmd="name" Time="<time in ms>" />
    /// or
    /// pause:5000
    /// </summary>
    public class PauseCommand : Command {
        public const string CmdPrefix = "pause:";

        public static List<PauseCommand> Commands { get => commands; }
        private static List<PauseCommand> commands = new List<PauseCommand>();

        static PauseCommand() {
            Commands.Add(new PauseCommand { Key = $"{CmdPrefix}" });
        }

        public PauseCommand() { }

        public override string ToString() {
            return $"Cmd=\"{Key}\" Args=\"{Args}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new PauseCommand());

        // ICommand:Execute
        public override void Execute() {
            base.Execute();

            int time;
            if (int.TryParse(Args, out time)) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Pausing {time}ms");
                // TODO: Is this the smartest way to do this?
                System.Threading.Thread.Sleep(time);
            }
        }
    }
}
