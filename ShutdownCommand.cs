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
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for ShutdownCommand.
    /// </summary>
    public class ShutdownCommand : Command {
        [XmlAttribute("Type")] public String Type;

        public ShutdownCommand() {
            // Serialzable, must have constructor
        }

        public ShutdownCommand(string type) {
            Type = type;
        }

        public override void Execute(Reply reply) {
            MainWindow.AddLogEntry("Cmd: ShutdownCommand: " + Type);
            using (var sc = new SystemControl()) {
                switch (Type.ToLower()) {
                    case "shutdown":
                        sc.Shutdown("MCE Controller Forced Shutdown", 30, true, false);
                        break;

                    case "restart":
                        sc.Shutdown("MCE Controller Forced Restart", 30, true, true);
                        break;

                    case "standby":
                        sc.Standby();
                        break;

                    case "hibernate":
                        sc.Hibernate();
                        break;

                    case "abort":
                        sc.Abort();
                        break;
                }
            }
        }
    }
}