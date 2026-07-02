//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MCEControl;
/// <summary>
/// Summary description for SetForegroundWindowCommand.
/// </summary>
public class SetForegroundWindowCommand : Command {
    [XmlAttribute("classname")]
    public string ClassName { get => AppName; set => AppName = value; }
    [XmlAttribute("appname")]
    public string AppName { get; set; } = null!;

    public static List<Command> BuiltInCommands {
        get => [
              new SetForegroundWindowCommand() { Cmd = "activatecode", AppName ="code" },
              new SetForegroundWindowCommand() { Cmd = "activatenotepad", AppName="Notepad"  },
              new SetForegroundWindowCommand() { Cmd = "activatemcec", AppName="MCEControl"  },
        ];
    }
    public SetForegroundWindowCommand() {
    }

    public SetForegroundWindowCommand(String className, String appName) {
        ClassName = className;
        AppName = appName;
    }

    public override string ToString() {
        return $"Cmd=\"{Cmd}\" AppName=\"{AppName}\"";
    }

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        try {
            if (!string.IsNullOrEmpty(AppName)) {
                Process[] procs = Process.GetProcessesByName(AppName);
                if (procs.Length > 0) {
                    Process? process = procs.Where(p => p.MainWindowHandle != IntPtr.Zero).FirstOrDefault();

                    Logger.Instance.Log4.Info($"{this.GetType().Name}: SetForegroundWindow({ClassName})");
                    Win32NativeMethods.SetForegroundWindow(process!.MainWindowHandle);
                }
                else {
                    Logger.Instance.Log4.Info($"{this.GetType().Name}: GetProcessByName for {ClassName} failed");
                    return false;
                }
                return true;
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{this.GetType().Name}: Failed for {ClassName} with error: {e.Message}");
            return false;
        }
        return true;
    }
}
