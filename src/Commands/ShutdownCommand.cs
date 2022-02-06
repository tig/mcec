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
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for ShutdownCommands.
    /// </summary>
    public class ShutdownCommand : Command {
        public static new List<Command> BuiltInCommands {
            get => new List<Command>() {
                new ShutdownCommand{ Cmd = $"shutdown", Type = $"shutdown" },
                new ShutdownCommand{ Cmd = $"shutdown-hybrid", Type = $"shutdown-hybrid" },
                new ShutdownCommand{ Cmd = $"restart", Type = $"restart" },
                new ShutdownCommand{ Cmd = $"restart-g", Type = $"restart-g" },
                new ShutdownCommand{ Cmd = $"standby", Type = $"standby" },
                new ShutdownCommand{ Cmd = $"hibernate", Type = $"hibernate"},
                new ShutdownCommand{ Cmd = $"abort", Type = $"abort" },
                new ShutdownCommand{ Cmd = $"poweroff", Type = $"poweroff" },
                new ShutdownCommand{ Cmd = $"logoff", Type = $"logoff" },
            };
        }

        private String type;
        [XmlAttribute("type")] public string Type { get => type; set => type = value; }
        private int timeOut = 30;
        [XmlAttribute("timeout")] public int TimeOut { get => timeOut; set => timeOut = value; }

        public ShutdownCommand() {
            // Serialzable, must have constructor
        }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\" Type=\"{Type}\" TimeOut=\"{TimeOut}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new ShutdownCommand() { Type = this.Type });

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            try {
                Logger.Instance.Log4.Info($"Cmd: ShutdownCommands: Executing {ToString()}");
                switch (Type.ToLowerInvariant()) {
                    case "shutdown":
                        Shutdown($"/s /t {TimeOut} /f /c \"MCE Controller Forced Shutdown\"");
                        break;

                    case "restart":
                        Shutdown($"/r /t {TimeOut} /f /c \"MCE Controller Forced Restart\"");
                        break;

                    case "restart-g":
                        Shutdown($"/g /t {TimeOut} /f /c \"MCE Controller Forced Restart with re-Login\"");
                        break;

                    case "standby":
                        Application.SetSuspendState(PowerState.Suspend, true, false);
                        break;

                    case "hibernate":
                        Application.SetSuspendState(PowerState.Hibernate, false, false);
                        break;

                    case "shutdown-hybrid":
                        // Shutdown.exe does not suppport timeout on /h (apparently)
                        Shutdown($"/h /c \"MCE Controller Forced Hybrid Shutdown\"");
                        break;

                    case "poweroff":
                        // Shutdown.exe does not suppport timeout on /p (apparently)
                        Shutdown($"/p /c \"MCE Controller Forced Power Off\"");
                        break;

                    case "logoff":
                        // Shutdown.exe does not suppport timeout on /l (apparently)
                        Shutdown($"/l /c \"MCE Controller Forced Logoff\"");
                        break;

                    case "abort":
                        Shutdown($"/a");
                        break;

                    default:
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: Invalid command: {ToString()}");
                        break;
                }
            }
            catch (System.ComponentModel.Win32Exception e) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: ({Cmd}) {e.Message}");
                return false;
            }
            return true;
        }

        public static void Shutdown(string shutdownArgs) {
            Logger.Instance.Log4.Debug($"ShutdownCommand: Invoking 'shutdown.exe {shutdownArgs}'");
            var proc = System.Diagnostics.Process.Start("shutdown", shutdownArgs);
            proc.WaitForExit(1000);
            if (proc.ExitCode != 0x0) {
                Logger.Instance.Log4.Error($"ShutdownCommand: 'shutdown.exe {shutdownArgs}' failed ({proc.ExitCode:X}). Forcing Win32Exception...");
                throw new System.ComponentModel.Win32Exception(proc.ExitCode);
            }
        }
    }
}
