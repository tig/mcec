//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Xml.Serialization;
using Microsoft.Win32.Security;

namespace MCEControl {
    /// <summary>
    /// Summary description for SetForegroundWindowCommand.
    /// </summary>
    public class SetForegroundWindowCommand : Command {
        [XmlAttribute("classname")]
        public string ClassName { get; set; }
        [XmlAttribute("windowname")]
        public string WindowName { get; set; }

        public SetForegroundWindowCommand() {
        }

        public SetForegroundWindowCommand(String className, String windowName) {
            ClassName = className;
            WindowName = windowName;
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new SetForegroundWindowCommand(ClassName, WindowName));

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            try {
                if (ClassName != null) {
                    var procs = Process.GetProcessesByName(ClassName);
                    if (procs.Length > 0) {
                        var h = procs[0].MainWindowHandle;

                        Logger.Instance.Log4.Info($"{this.GetType().Name}: SetForegroundWindow({ClassName})");
                        Win32.SetForegroundWindow(h);
                    }
                    else {
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: GetProcessByName for {ClassName} failed");
                        return false;
                    }
                }
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Failed for {ClassName} with error: {e.Message}");
                return false;
            }
            return true;
        }
    }
}
