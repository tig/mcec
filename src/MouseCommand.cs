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
using System.Globalization;
using System.Xml.Serialization;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl {
    /// <summary>
    /// Simulates mouse movements.
    /// </summary>
    public class MouseCommand : Command {
        public const string CmdPrefix = "mouse:";

        private static List<MouseCommand> commands = new List<MouseCommand>() {
            new MouseCommand{ Key = $"{CmdPrefix }" }, // the rest are just for documentation in the Commmand Window
            new MouseCommand{ Key = $"{CmdPrefix }lbc" },
            new MouseCommand{ Key = $"{CmdPrefix }lbc" },
            new MouseCommand{ Key = $"{CmdPrefix }lbdc" },
            new MouseCommand{ Key = $"{CmdPrefix }lbd" },
            new MouseCommand{ Key = $"{CmdPrefix }lbu" },
            new MouseCommand{ Key = $"{CmdPrefix }rbc" },
            new MouseCommand{ Key = $"{CmdPrefix }rbdc" },
            new MouseCommand{ Key = $"{CmdPrefix }rbd" },
            new MouseCommand{ Key = $"{CmdPrefix }rbu" },
            new MouseCommand{ Key = $"{CmdPrefix }xbc,3" },
            new MouseCommand{ Key = $"{CmdPrefix }xbcd,3" },
            new MouseCommand{ Key = $"{CmdPrefix }xbd,3" },
            new MouseCommand{ Key = $"{CmdPrefix }xbu,3" },
            new MouseCommand{ Key = $"{CmdPrefix }mm,x,y" },
            new MouseCommand{ Key = $"{CmdPrefix }mt,x,y" },
            new MouseCommand{ Key = $"{CmdPrefix }hs,x" },
            new MouseCommand{ Key = $"{CmdPrefix }vs,y" },
        };

        public static List<MouseCommand> Commands { get => commands;  }

        public MouseCommand() { }

        public override string ToString() {
            return $"Cmd=\"{Key}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new MouseCommand());
     
        // ICommand:Execute
        public override void Execute() {

            if (this.Reply is null) throw new InvalidOperationException("Reply property cannot be null.");
            if (this.Args is null) throw new InvalidOperationException("Args property cannot be null.");

            var sim = new InputSimulator();
            // Format is "mouse:<action>[,<parameters>]
            string[] param = Args.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ;
            if (param.Length == 0) return;

            switch (param[0]) {
                case "lbc": sim.Mouse.LeftButtonClick(); break;
                case "lbdc": sim.Mouse.LeftButtonDoubleClick(); break;
                case "lbd": sim.Mouse.LeftButtonDown(); break;
                case "lbu": sim.Mouse.LeftButtonUp(); break;

                case "rbc": sim.Mouse.RightButtonClick(); break;
                case "rbdc": sim.Mouse.RightButtonDoubleClick(); break;
                case "rbd": sim.Mouse.RightButtonDown(); break;
                case "rbu": sim.Mouse.RightButtonUp(); break;

                // "mouse:xbc,3" - Mouse button 3 click
                case "xbc": sim.Mouse.XButtonClick(GetIntOrZero(param, 1)); break;
                case "xbdc": sim.Mouse.XButtonDoubleClick(GetIntOrZero(param, 1)); break;
                case "xbd": sim.Mouse.XButtonDown(GetIntOrZero(param, 1)); break;
                case "xbu": sim.Mouse.XButtonUp(GetIntOrZero(param, 1)); break;

                // "mouse:mb,15,20" - Move mouse 15 in X direction, and 20 in Y direction
                case "mm": sim.Mouse.MoveMouseBy(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                // "mouse:mt,812,562" - Move mouse to (812,562) on the screen
                case "mt": sim.Mouse.MoveMouseTo(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                // "mouse:mtv,812,562" - Move mouse to (812,562) on the virtual desktop screen
                case "mtv": sim.Mouse.MoveMouseToPositionOnVirtualDesktop(GetIntOrZero(param, 1), GetIntOrZero(param, 2)); break;

                case "hs": sim.Mouse.HorizontalScroll(GetIntOrZero(param, 1)); break;
                case "vs": sim.Mouse.VerticalScroll(GetIntOrZero(param, 1)); break;
            }
        }

        private static int GetIntOrZero(String[] s, int index) {
            int val = 0;
            if (index < s.Length) {
                if (!int.TryParse(s[index], out val))
                    return 0;
            }
            return val;
        }
    }
}
