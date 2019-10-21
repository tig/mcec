//-------------------------------------------------------------------
// Copyright © 2017 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MCEControl.Properties;
using WindowsInput.Native;

namespace MCEControl {
    /// <summary>
    /// Commands that control MCE Controller, or get information about it
    /// </summary>
    class McecCommand : Command {
        public const string CmdPrefix = "mcec:";

        public static McecCommand[] Commands = new McecCommand[] {
            new McecCommand{ Key = $"{CmdPrefix }ver" },
            new McecCommand{ Key = $"{CmdPrefix }exit" },
            new McecCommand{ Key = $"{CmdPrefix }cmds" },
            new McecCommand{ Key = $"{CmdPrefix }time" },
        };

        public McecCommand() {
        }

        public McecCommand(String cmd) {
            Key = cmd.Substring(CmdPrefix.Length, cmd.Length - CmdPrefix.Length);
        }

        public override string ToString() {
            return $"Cmd=\"{Key}\"";
        }

        public override void Execute(Reply reply) {
            if (reply == null)
                return;

            var replyBuilder = new StringBuilder();
            switch (Key) {
                // MCE Controller version
                case "ver":
                    replyBuilder.Append(Application.ProductVersion);
                    break;

                // Cause MCE Controller to exit
                case "exit":
                    reply.WriteLine("exiting");
                    MainWindow.Instance.ShutDown();
                    return;

                // Return a list of supported commands (really just for testing)
                case "cmds":
                    replyBuilder.Append(Environment.NewLine);
                    var orderedKeys = MainWindow.Instance.CmdTable.Keys.Cast<string>().OrderBy(c => c);
                    foreach (string key in orderedKeys) {
                        Command cmd = MainWindow.Instance.CmdTable[key];
                        var item = new ListViewItem(cmd.Key);
                        Match match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                        replyBuilder.AppendFormat($"<{match.Groups[1].Value} {cmd.ToString()} />{Environment.NewLine}" );
                    }
                    break;

                // Return the current date/time of the PC
                case "time":
                    DateTime dt = DateTime.Now;
                    replyBuilder.AppendFormat("{0}", DateTime.Now);
                    break;
            }

            // Reply.  
            replyBuilder.Insert(0, $"{Key}=");
            Reply(reply, replyBuilder.ToString());
        }

        private void Reply(Reply reply, String msg) {
            Logger.Instance.Log4.Info("Cmd: Sending reply: " + msg);
            reply.WriteLine(msg);
        }
    }
}
