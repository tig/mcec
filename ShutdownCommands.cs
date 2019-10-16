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
        private String type;
        [XmlAttribute("Type")] public string Type { get => type; set => type = value; }
        private uint timeOut = 30;
        [XmlAttribute("TimeOut")] public uint TimeOut { get => timeOut; set => timeOut = value; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Serialization")]
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
                switch (Type.ToUpperInvariant()) {
                    case "SHUTDOWN":
                        SystemControl.Shutdown("MCE Controller Forced Shutdown", TimeOut, true, false);
                        break;

                    case "RESART":
                        SystemControl.Shutdown("MCE Controller Forced Restart", TimeOut, true, true);
                        break;

                    case "STANDBY":
                        SystemControl.Standby();
                        break;

                    case "HIBERNATE":
                        SystemControl.Hibernate();
                        break;

                    case "ABORT":
                        SystemControl.Abort();
                        break;
                }
            }
        }
    }
}
