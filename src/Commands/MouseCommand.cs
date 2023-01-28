//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
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
using WindowsInput;

namespace MCEControl {
    /// <summary>
    /// Simulates mouse movements.
    /// </summary>
    public class MouseCommand : Command {
        public const string CmdPrefix = "mouse:";

        public static new List<Command> BuiltInCommands {
            get => new List<Command>() {
                new MouseCommand{ Cmd = $"{CmdPrefix }" },  // Commands that use form of "cmd:" must define a blank version
                new MouseCommand{ Cmd = $"{CmdPrefix }lbc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }lbdc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }lbd" },
                new MouseCommand{ Cmd = $"{CmdPrefix }lbu" },
                new MouseCommand{ Cmd = $"{CmdPrefix }rbc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }rbdc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }rbd" },
                new MouseCommand{ Cmd = $"{CmdPrefix }rbu" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mbc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mbdc" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mbd" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mbu" },
                new MouseCommand{ Cmd = $"{CmdPrefix }xbc,n" },
                new MouseCommand{ Cmd = $"{CmdPrefix }xbcd,n" },
                new MouseCommand{ Cmd = $"{CmdPrefix }xbd,n" },
                new MouseCommand{ Cmd = $"{CmdPrefix }xbu,n" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mm,x,y" },
                new MouseCommand{ Cmd = $"{CmdPrefix }mt,x,y" },
                new MouseCommand{ Cmd = $"{CmdPrefix }hs,x" },
                new MouseCommand{ Cmd = $"{CmdPrefix }vs,y" },
            };
        }

        public MouseCommand() { }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new MouseCommand());

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            var sim = new InputSimulator();
            // Format is "mouse:<action>[,<parameters>]
            var param = Args.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (param.Length == 0) {
                return true;
            }

            int mb = 0;

            switch (param[0]) {
                case "lbc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Click");
                    sim.Mouse.LeftButtonClick();
                    break;

                case "lbdc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Double Click");
                    sim.Mouse.LeftButtonDoubleClick();
                    break;

                case "lbd":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Down");
                    sim.Mouse.LeftButtonDown();
                    break;

                case "lbu":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Left Button Up");
                    sim.Mouse.LeftButtonUp();
                    break;

                case "rbc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Click");
                    sim.Mouse.RightButtonClick();
                    break;

                case "rbdc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Double Click");
                    sim.Mouse.RightButtonDoubleClick();
                    break;

                case "rbd":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Down");
                    sim.Mouse.RightButtonDown();
                    break;

                case "rbu":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Right Button Up");
                    sim.Mouse.RightButtonUp();
                    break;

                case "mbc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Click");
                    sim.Mouse.MiddleButtonClick();
                    break;

                case "mbdc":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Double Click");
                    sim.Mouse.MiddleButtonDoubleClick();
                    break;

                case "mbd":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Down");
                    sim.Mouse.MiddleButtonDown();
                    break;

                case "mbu":
                    Logger.Instance.Log4.Info($"{GetType().Name}: Middle Button Up");
                    sim.Mouse.MiddleButtonUp();
                    break;

                // "mouse:xbc,3" - Mouse X button 3 click
                case "xbc":
                    mb = GetIntOrZero(param, 1);
                    Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} click");
                    sim.Mouse.XButtonClick(mb);
                    break;

                case "xbdc":
                    mb = GetIntOrZero(param, 1);
                    Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} doubleclick");
                    sim.Mouse.XButtonDoubleClick(mb);
                    break;

                case "xbd":
                    mb = GetIntOrZero(param, 1);
                    Logger.Instance.Log4.Info($"{GetType().Name}: XButton {mb} down");
                    sim.Mouse.XButtonDown(mb);
                    break;

                case "xbu":
                    mb = GetIntOrZero(param, 1);
                    Logger.Instance.Log4.Info($"{GetType().Name}: Xbutton {mb} up");
                    sim.Mouse.XButtonUp(mb);
                    break;

                // "mouse:mb,15,20" - Move mouse 15 in X direction, and 20 in Y direction
                case "mm": sim.Mouse.MoveMouseBy(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                // "mouse:mt,812,562" - Move mouse to (812,562) on the screen
                case "mt": sim.Mouse.MoveMouseTo(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                // "mouse:mtv,812,562" - Move mouse to (812,562) on the virtual desktop screen
                case "mtv": sim.Mouse.MoveMouseToPositionOnVirtualDesktop(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                case "hs": sim.Mouse.HorizontalScroll(GetIntOrZero(param, 1)); break;
                case "vs": sim.Mouse.VerticalScroll(GetIntOrZero(param, 1)); break;
            }
            return true;
        }

        private static int GetIntOrZero(String[] s, int index) {
            var val = 0;
            if (index < s.Length) {
                if (!int.TryParse(s[index], out val)) {
                    return 0;
                }
            }
            return val;
        }
    }
}
