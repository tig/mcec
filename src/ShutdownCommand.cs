//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for ShutdownCommands.
    /// </summary>
    public class ShutdownCommand : Command {
        private String type;
        [XmlAttribute("Type")] public string Type { get => type; set => type = value; }
        private int timeOut = 30;
        [XmlAttribute("TimeOut")] public int TimeOut { get => timeOut; set => timeOut = value; }
        public static List<ShutdownCommand> Commands { get => commands; }

        private static List<ShutdownCommand> commands = new List<ShutdownCommand>() {
            new ShutdownCommand{ Key = $"shutdown", Type = $"shutdown" },
            new ShutdownCommand{ Key = $"restart", Type = $"restart" },
            new ShutdownCommand{ Key = $"restart-g", Type = $"restart-g" },
            new ShutdownCommand{ Key = $"standby", Type = $"standby" },
            new ShutdownCommand{ Key = $"hibernate", Type = $"hibernate"},
            new ShutdownCommand{ Key = $"abort", Type = $"abort" },
            new ShutdownCommand{ Key = $"poweroff", Type = $"poweroff" },
            new ShutdownCommand{ Key = $"logoff", Type = $"logoff" },
        };

        public ShutdownCommand() {
            // Serialzable, must have constructor
        }

        public override string ToString() {
            return $"Cmd=\"{Key}\" Type=\"{Type}\" TimeOut=\"{TimeOut}\"";
        }

        public override Command Clone(Reply reply, string args = null) => new ShutdownCommand() { Reply = reply, Args = args, Key = this.Key, Type = this.Type };

        // ICommand:Execute
        public override void Execute() {
            try {
                Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: Executing {ToString()}");
                switch (Type.ToUpperInvariant()) {
                    case "SHUTDOWN":
                        Shutdown($"/s /t {TimeOut} /f /c \"MCE Controller Forced Shutdown\"");
                        break;

                    case "RESTART":
                        Shutdown($"/r /t {TimeOut} /f /c \"MCE Controller Forced Restart\"");
                        break;

                    case "RESTART-G":
                        Shutdown($"/g /t {TimeOut} /f /c \"MCE Controller Forced Restart with re-Login\"");
                        break;


                    case "STANDBY":
                        Shutdown($"/s /hybrid /t {TimeOut} /c \"MCE Controller Forced Standby\"");
                        break;

                    case "HIBERNATE":
                        // Shutdown.exe does not suppport timeout on /h (apparently)
                        Shutdown($"/h /c \"MCE Controller Forced Hibernation\"");
                        break;

                    case "POWEROFF":
                        // Shutdown.exe does not suppport timeout on /p (apparently)
                        Shutdown($"/p /c \"MCE Controller Forced Power Off\"");
                        break;

                    case "LOGOFF":
                        // Shutdown.exe does not suppport timeout on /l (apparently)
                        Shutdown($"/l /c \"MCE Controller Forced Logoff\"");
                        break;

                    case "ABORT":
                        Shutdown($"/a");
                        break;

                    default:
                        Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: Invalid command: {ToString()}");
                        break;
                }
            }
            catch (System.ComponentModel.Win32Exception e) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: ({Key}) {e.Message}");
            }
        }

        public static void Shutdown(string shutdownArgs) {
            var proc = System.Diagnostics.Process.Start("ShutDown", shutdownArgs);
            proc.WaitForExit(1000);
            if (proc.ExitCode != 0x0)
                throw new System.ComponentModel.Win32Exception(proc.ExitCode);
        }
    }
}
