//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
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
        private int timeOut = 30;
        [XmlAttribute("TimeOut")] public int TimeOut { get => timeOut; set => timeOut = value; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Serialization")]
        public static ShutdownCommands[] Commands = new ShutdownCommands[] {
            new ShutdownCommands{ Key = $"shutdown", Type = $"shutdown" },
            new ShutdownCommands{ Key = $"restart", Type = $"restart" },
            new ShutdownCommands{ Key = $"standby", Type = $"standby" },
            new ShutdownCommands{ Key = $"hibernate", Type = $"hibernate"},
            new ShutdownCommands{ Key = $"abort", Type = $"abort" },
        };

        public ShutdownCommands() {
            // Serialzable, must have constructor
        }

        public override string ToString() {
            return $"Cmd=\"{Key}\" Type=\"{Type}\" TimeOut=\"{TimeOut}\"";
        }

        public override void Execute(Reply reply) {
            Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: Executing {ToString()}");
            switch (Type.ToUpperInvariant()) {
                case "SHUTDOWN":
                    SystemControl.Shutdown("MCE Controller Forced Shutdown", TimeOut, true, false);
                    break;

                case "RESTART":
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

                default:
                    Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: Invalid command: {ToString()}");
                    break;
            }
        }
    }
}
