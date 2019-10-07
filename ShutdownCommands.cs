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
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for ShutdownCommands.
    /// </summary>
    public class ShutdownCommands : Command {
        [XmlAttribute("Type")] public String Type;
        [XmlAttribute("TimeOut")] public uint TimeOut = 30;

        public static ShutdownCommands[] Commands = new ShutdownCommands[] {
            new ShutdownCommands{ Key = $"shutdown" },
            new ShutdownCommands{ Key = $"restrart" },
            new ShutdownCommands{ Key = $"standby" },
            new ShutdownCommands{ Key = $"hibernate" },
            new ShutdownCommands{ Key = $"abort" },
        };
        public ShutdownCommands() {
            // Serialzable, must have constructor
        }

        public ShutdownCommands(string type) {
            Type = type;
        }
        public override string ToString() {
            return $"Cmd=\"{Key}\" Type=\"{Type}\" TimeOut=\"{TimeOut}\"";
        }

        public override void Execute(Reply reply) {
            Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: {ToString()}");
            using (var sc = new SystemControl()) {
                switch (Type.ToLower()) {
                    case "shutdown":
                        sc.Shutdown("MCE Controller Forced Shutdown", TimeOut, true, false);
                        break;

                    case "restart":
                        sc.Shutdown("MCE Controller Forced Restart", TimeOut, true, true);
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
