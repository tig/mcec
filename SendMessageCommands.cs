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
    /// Summary description for SendMessageCommand.
    /// </summary>
    public class SendMessageCommand : Command {
        [XmlAttribute("Msg")] public int Msg;

        // This is int so that -1 can be specified in the XML
        [XmlAttribute("lParam")] public int LParam;
        [XmlAttribute("wParam")] public int WParam;

        public SendMessageCommand() {
        }

        public SendMessageCommand(String className, String windowName, DWORD msg, DWORD wParam, DWORD lParam) {
            ClassName = className;
            WindowName = windowName;
            Msg = (int) msg;
            WParam = (int) wParam;
            LParam = (int) lParam;
        }

        [XmlAttribute("ClassName")]
        public String ClassName { get; set; }

        [XmlAttribute("WindowName")]
        public String WindowName { get; set; }

        public override void Execute(Reply reply)
        {
            try {
                if (ClassName != null) {
                    var procs = Process.GetProcessesByName(ClassName);
                    if (procs.Length > 0) {
                        var h = procs[0].MainWindowHandle;

                        MainWindow.AddLogEntry(String.Format("Cmd: SendMessage ({0}): {1} {2} {3}", ClassName, Msg, WParam,
                                                             LParam));
                        Win32.SendMessage(h, (DWORD) Msg, (DWORD) WParam, (DWORD) LParam);
                    }
                    else {
                        MainWindow.AddLogEntry("Cmd: GetProcessByName for " + ClassName + " failed");
                    }
                }
                else {
                    var h = Win32.GetForegroundWindow();
                    MainWindow.AddLogEntry(String.Format("Cmd: SendMessage (forground window): {0} {1} {2}", Msg, WParam, LParam));
                    Win32.SendMessage(h, (DWORD) Msg, (DWORD) WParam, (DWORD) LParam);
                }
            }
            catch (Exception e) {
                MainWindow.AddLogEntry("Cmd: SendMessageCommand.Execute failed for " + ClassName + " with error: " +
                                       e.Message);
            }
        }
    }
}