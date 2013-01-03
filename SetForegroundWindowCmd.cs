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
        [XmlAttribute("ClassName")] public String ClassName;
        [XmlAttribute("WindowName")] public String WindowName;

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

                        MainWindow.AddLogEntry("Cmd: SetForegroundWindow(\"" + ClassName + "\")");
                        Win32.SetForegroundWindow(h);
                    }
                    else {
                        MainWindow.AddLogEntry("Cmd: GetProcessByName for " + ClassName + " failed");
                    }
                }
            }
            catch (Exception e) {
                MainWindow.AddLogEntry("Cmd: SetForegroundWindowCommand.Execute failed for " + ClassName + " with error: " +
                                       e.Message);
            }
        }
    }
}