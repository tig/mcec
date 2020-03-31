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
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;
using Microsoft.Win32.Security;

namespace MCEControl {
    using DWORD = UInt32;

    /// <summary>
    /// Summary description for SendMessageCommand.
    /// </summary>
    public class SendMessageCommand : Command {
        private int msg;
        [XmlAttribute("msg")] public int Msg { get => msg; set => msg = value; }

        // This is int so that -1 can be specified in the XML
        private int lParam;
        [XmlAttribute("lparam")] public int LParam { get => lParam; set => lParam = value; }

        private int wParam;
        [XmlAttribute("wparam")] public int WParam { get => wParam; set => wParam = value; }

        [XmlAttribute("className")]
        public String ClassName { get; set; }
        [XmlAttribute("windowname")]
        public String WindowName { get; set; }

        public static new List<SendMessageCommand> BuiltInCommands {
            get => new List<SendMessageCommand>() {
                  new SendMessageCommand() { Cmd = "maximize", Msg=274, wParam=61488, lParam=0 },
                  new SendMessageCommand() { Cmd = "screensaver", Msg=274, wParam=61760, lParam=0 },
                  new SendMessageCommand() { Cmd = "monitoroff", Msg=274, wParam=61808, lParam=2 },
                  new SendMessageCommand() { Cmd = "monitoron", Msg=274, wParam=61808, lParam=-1 }
            };
        }

        public SendMessageCommand() {
        }

        public SendMessageCommand(String className, String windowName, int msg, int wParam, int lParam) {
            ClassName = className;
            WindowName = windowName;
            Msg = (int)msg;
            WParam = (int)wParam;
            LParam = (int)lParam;
        }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\" Msg=\"{Msg}\" lParam=\"{LParam}\" wParam=\"{WParam}\" ClassName=\"{ClassName}\" WindowName=\"{WindowName}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new SendMessageCommand(ClassName, WindowName, Msg, WParam, LParam));

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

                        Logger.Instance.Log4.Info($"{this.GetType().Name}: SendMessage {ToString()}");
                        Win32.SendMessage(h, (DWORD)Msg, (DWORD)WParam, (DWORD)LParam);
                    }
                    else {
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: GetProcessByName for {ClassName} failed");
                    }
                }
                else {
                    var h = Win32.GetForegroundWindow();
                    Logger.Instance.Log4.Info($"{this.GetType().Name}: SendMessage (forground window): {ToString()}");
                    Win32.SendMessage(h, (DWORD)Msg, (DWORD)WParam, (DWORD)LParam);
                }
            }
            catch (Exception e) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Failed for {ClassName} with error: {e.Message}");
                return true;
            }
            return false;
        }
    }
}
