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
    using HWND = IntPtr;
    using DWORD = UInt32;

    /// <summary>
    /// Summary description for SetForegroundWindowCommand.
    /// </summary>
    public class SetForegroundWindowCommand : Command {
        private String className;
        [XmlAttribute("ClassName")] public string ClassName { get => className; set => className = value; }
        private String windowName;
        [XmlAttribute("WindowName")] public string WindowName { get => windowName; set => windowName = value; }

        public SetForegroundWindowCommand() {
        }

        public SetForegroundWindowCommand(String className, String windowName) {
            ClassName = className;
            WindowName = windowName;
        }

        public override void Execute(Reply reply)
        {
            try {
                if (ClassName != null) {
                    var procs = Process.GetProcessesByName(ClassName);
                    if (procs.Length > 0) {
                        var h = procs[0].MainWindowHandle;

                        Logger.Instance.Log4.Info("Cmd: SetForegroundWindow(\"" + ClassName + "\")");
                        Win32.SetForegroundWindow(h);
                    }
                    else {
                        Logger.Instance.Log4.Info("Cmd: GetProcessByName for " + ClassName + " failed");
                    }
                }
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info("Cmd: SetForegroundWindowCommand.Execute failed for " + ClassName + " with error: " +
                                       e.Message);
            }
        }
    }
}
