//-------------------------------------------------------------------
// Copyright © 2017 Kindel, LLC
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
using System.Linq;
using System.Xml.Serialization;

namespace MCEControl;

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

    private String className = null!;
    [XmlAttribute("classname")] public String ClassName { get => className; set => className = value; }

    private String windowName = null!;
    [XmlAttribute("windowname")] public String WindowName { get => windowName; set => windowName = value; }

    public static List<Command> BuiltInCommands {
        get => [
              new SendMessageCommand() { Cmd = "maximize", Msg=274, wParam=61488, lParam=0 },
              new SendMessageCommand() { Cmd = "screensaver", Msg=274, wParam=61760, lParam=0 },
              new SendMessageCommand() { Cmd = "monitoroff", Msg=274, wParam=61808, lParam=2 },
              new SendMessageCommand() { Cmd = "monitoron", Msg=274, wParam=61808, lParam=-1 }
        ];
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

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        try {
            if (!string.IsNullOrWhiteSpace(ClassName)) {
                Process[] procs = Process.GetProcessesByName(ClassName);
                if (procs.Length > 0) {
                    Process? win = procs[0];

                    if (!string.IsNullOrWhiteSpace(WindowName)) {
                        // Find MainWindowTitle matching WindowName
                        win = procs.FirstOrDefault(w => w.MainWindowTitle.Equals(WindowName, StringComparison.Ordinal));
                    }
                    if (win == null) {
                        Logger.Instance.Log4.Error($"{this.GetType().Name}: Could not find a window of class '{ClassName}' captioned with '{WindowName}'");
                        return false;
                    }
                    else {
                        Logger.Instance.Log4.Info($"{this.GetType().Name}: SendMessage(\"{win.MainWindowTitle}\", {Msg}, {WParam}, {LParam}) - {ToString()}");
                        // #203: (nint) sign-extends the stored int (lParam=-1 stays -1 on x64).
                        Win32NativeMethods.SendMessage(win.MainWindowHandle, (uint)Msg, (nint)WParam, (nint)LParam);
                    }
                }
                else {
                    Logger.Instance.Log4.Error($"{this.GetType().Name}: GetProcessByName for class '{ClassName}' failed");
                    return false;
                }
            }
            else {
                // #210: GetForegroundWindow is declared once, in AgentNativeMethods (same import,
                // shared rather than duplicated here).
                IntPtr h = AgentNativeMethods.GetForegroundWindow();
                Logger.Instance.Log4.Info($"{this.GetType().Name}: SendMessage(<forground window>, {Msg}, {WParam}, {LParam}) - {ToString()}");
                Win32NativeMethods.SendMessage(h, (uint)Msg, (nint)WParam, (nint)LParam);
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{this.GetType().Name}: Failed for '{ClassName}' with error: {e.Message}");
            return false;
        }
        return true;
    }
}
