//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MCEControl.Properties;
using WindowsInput.Native;

namespace MCEControl
{
    /// <summary>
    /// Commands that control MCE Controller, or get information about it
    /// </summary>
    class McecCommand : Command {
        public static readonly string CmdPrefix = "mcec:";

        public McecCommand(String cmd) {
            Key = cmd.Substring(CmdPrefix.Length, cmd.Length - CmdPrefix.Length);
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
                    MainWindow.MainWnd.BeginInvoke((Action)(() => MainWindow.MainWnd.ShutDown()));
                    return;

                // Return a list of supported commands (really just for testing)
                case "cmds":
                    foreach (Command cmd in MainWindow.MainWnd.CmdTable.List) {
                        Match match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                        replyBuilder.AppendFormat("{0}={1}{2}", cmd.Key, match.Groups[1].Value, Environment.NewLine);
                    }

                    // Now add VK_ commands
                    foreach (VirtualKeyCode vk in Enum.GetValues(typeof(VirtualKeyCode))) {
                        string s;
                        if (vk > VirtualKeyCode.HELP && vk < VirtualKeyCode.LWIN)
                            s = vk.ToString();  // already have VK_
                        else
                            s = "VK_" + vk.ToString(); 
                        replyBuilder.AppendFormat("{0}={1}{2}", s, "SendInput", Environment.NewLine);
                    }
                    break;

                // Return the current date/time of the PC
                case "time":
                    DateTime dt = DateTime.Now;
                    replyBuilder.AppendFormat("{0}", DateTime.Now);
                    break;

                // These two are for testing. They cause a loop between two
                // instances of MCE Controller
                case "foo":
                    reply.WriteLine("mcec:bar");
                    return;
                case "bar":
                    reply.WriteLine("mcec:foo");
                    return;

            }

            // Reply.  
            replyBuilder.Insert(0, String.Format("{0}=", Key));
            Reply(reply, replyBuilder.ToString());
        }

        private void Reply(Reply reply, String msg) {
            MainWindow.AddLogEntry("Cmd: Sending reply: " + msg);
            reply.WriteLine(msg);
        }
    }
}
