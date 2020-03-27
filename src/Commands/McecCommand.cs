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
            new McecCommand{ Cmd = $"{CmdPrefix}" },   // The rest are for documentation
            new McecCommand{ Cmd = $"{CmdPrefix }ver" },
            new McecCommand{ Cmd = $"{CmdPrefix }exit" },
            new McecCommand{ Cmd = $"{CmdPrefix }cmds" },
            new McecCommand{ Cmd = $"{CmdPrefix }time" },
        };

        public McecCommand() {
        }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new McecCommand());

        // ICommand:Execute
        public override void Execute() {
            base.Execute();

            if (this.Reply is null) throw new InvalidOperationException("Reply property cannot be null.");
            if (this.Args is null) throw new InvalidOperationException("Args property cannot be null.");

            var replyBuilder = new StringBuilder();
            switch (Args.ToUpperInvariant()) {
                // MCE Controller version
                case "VER":
                    replyBuilder.Append(Application.ProductVersion);
                    break;

                // Cause MCE Controller to exit
                case "EXIT":
                    Reply.WriteLine("exiting");
                    MainWindow.Instance.ShutDown();
                    return;

                // Return a list of supported commands (really just for testing)
                case "CMDS":
                    Command cmd = this ;
                    Match match = null;
                    try {
                        replyBuilder.Append(Environment.NewLine);
                        var orderedKeys = MainWindow.Instance.Invoker.Keys.Cast<string>().OrderBy(c => c);
                        foreach (string key in orderedKeys) {
                            cmd = (Command)MainWindow.Instance.Invoker[key];
                            var item = new ListViewItem(cmd.Cmd);
                            match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                            replyBuilder.Append($"<{match.Groups[1].Value} {cmd.ToString()} />{Environment.NewLine}");
                        }
                    }
                    catch (Exception e) {
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: ({Cmd}:{Args}) <{match.Groups[1].Value} {cmd.ToString()}/> - {e.Message}");
                    }
                    break;

                // Return the current date/time of the PC
                case "TIME":
                    DateTime dt = DateTime.Now;
                    replyBuilder.AppendFormat("{0}", DateTime.Now);
                    break;
            }

            // Reply.  
            replyBuilder.Insert(0, $"{Args}=");
            Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending reply: {replyBuilder.ToString()}");
            Reply.WriteLine(replyBuilder.ToString());
        }
    }
}
