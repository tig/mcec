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
        [XmlAttribute("classname")] public string ClassName { get => className; set => className = value; }
        private String windowName;
        [XmlAttribute("windowname")] public string WindowName { get => windowName; set => windowName = value; }

        public SetForegroundWindowCommand() {
        }

        public SetForegroundWindowCommand(String className, String windowName) {
            ClassName = className;
            WindowName = windowName;
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new SetForegroundWindowCommand(ClassName, WindowName));

        // ICommand:Execute
        public override void Execute() {

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
                    }
                }
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Failed for {ClassName} with error: {e.Message}");
            }
        }
    }
}
