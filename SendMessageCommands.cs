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
        [XmlAttribute("ClassName")]
        public String ClassName { get; set; }
        [XmlAttribute("WindowName")]
        public String WindowName { get; set; }

        public SendMessageCommand() {
        }

        public SendMessageCommand(String className, String windowName, DWORD msg, DWORD wParam, DWORD lParam) {
            ClassName = className;
            WindowName = windowName;
            Msg = (int)msg;
            WParam = (int)wParam;
            LParam = (int)lParam;
        }

        public override string ToString() {
            return $"Cmd=\"{Key}\" Msg=\"{Msg}\" lParam=\"{LParam}\" wParam=\"{WParam}\" ClassName=\"{ClassName}\" WindowName=\"{WindowName}\"";
        }
        public override void Execute(Reply reply) {
            try {
                if (ClassName != null) {
                    var procs = Process.GetProcessesByName(ClassName);
                    if (procs.Length > 0) {
                        var h = procs[0].MainWindowHandle;

                        Logger.Instance.Log4.Info($"Cmd: SendMessage {ToString()}");
                        Win32.SendMessage(h, (DWORD)Msg, (DWORD)WParam, (DWORD)LParam);
                    }
                    else {
                        Logger.Instance.Log4.Info("Cmd: GetProcessByName for " + ClassName + " failed");
                    }
                }
                else {
                    var h = Win32.GetForegroundWindow();
                    Logger.Instance.Log4.Info($"Cmd: SendMessage (forground window): {ToString()}");
                    Win32.SendMessage(h, (DWORD)Msg, (DWORD)WParam, (DWORD)LParam);
                }
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"Cmd: SendMessageCommand.Execute failed for {ClassName} with error: {e.Message}");
            }
        }
    }
}
