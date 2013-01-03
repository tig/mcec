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
using System.Globalization;
using System.Xml.Serialization;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl {
    /// <summary>
    /// Simulates a keystroke including shift, ctrl, alt, and windows key 
    /// modifiers.
    /// </summary>
    [Serializable]
    public class SendInputCommand : Command {
        [XmlAttribute("Alt")] public bool Alt;
        [XmlAttribute("Ctrl")] public bool Ctrl;
        [XmlAttribute("Shift")] public bool Shift;
        [XmlAttribute("Win")] public bool Win;
        [XmlAttribute("vk")] public string Vk;

        public SendInputCommand() {
        }

        public SendInputCommand(string vk, bool shift, bool ctrl, bool alt) {
            Vk = vk;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
            Win = false;
        }

        public SendInputCommand(string vk, bool shift, bool ctrl, bool alt, bool win) {
            Vk = vk;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
            Win = win;
        }

        public override void Execute(Reply reply)
        {
            try {
                VirtualKeyCode vkcode;
                if (!Vk.ToUpper().StartsWith("VK_") ||
                    (!Enum.TryParse(Vk.ToUpper(), true, out vkcode) &&
                     !Enum.TryParse(Vk.ToUpper().Substring(3), true, out vkcode))) {
                    // Not a VK_ string
                    // Hex?
                    ushort num;
                    if ((!Vk.ToUpper().StartsWith("0X") ||
                         !ushort.TryParse(Vk.Substring(2), NumberStyles.HexNumber,
                                          CultureInfo.InvariantCulture.NumberFormat, out num)) &&
                         !ushort.TryParse(Vk, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat,
                                         out num)) {
                        // bad format. barf.
                        MainWindow.AddLogEntry(String.Format("Cmd: Invalid VK: {0}", Vk));
                        return;
                    }
                    vkcode = (VirtualKeyCode) num;
                }

                string s;
                if (vkcode > VirtualKeyCode.HELP && vkcode < VirtualKeyCode.LWIN)
                    s = char.ToUpper((char)vkcode).ToString();
                else 
                    s = "VK_" + vkcode.ToString();
                if (Alt) s = "Alt-" + s;
                if (Ctrl) s = "Ctrl-" + s;
                if (Shift) s = "Shift-" + s;
                if (Win) s = "Win-" + s;

                MainWindow.AddLogEntry(String.Format("Cmd: Sending VK: '{0}' (0x{1:x2})", s, (ushort)vkcode));

                var sim = new KeyboardSimulator();

                if (Shift) {
                    sim.KeyDown(VirtualKeyCode.SHIFT);
                }
                if (Ctrl) {
                    sim.KeyDown(VirtualKeyCode.CONTROL);
                }
                if (Alt) {
                    sim.KeyDown(VirtualKeyCode.MENU);
                }
                if (Win) {
                    sim.KeyDown(VirtualKeyCode.LWIN);
                }

                sim.KeyPress(vkcode);

                // Key up shift, ctrl, and/or alt
                if (Shift) {
                    sim.KeyUp(VirtualKeyCode.SHIFT);
                }
                if (Ctrl) {
                    sim.KeyUp(VirtualKeyCode.CONTROL);
                }
                if (Alt) {
                    sim.KeyUp(VirtualKeyCode.MENU);
                }
                if (Win) {
                    sim.KeyUp(VirtualKeyCode.LWIN);
                }
            }
            catch (Exception e) {
                MainWindow.AddLogEntry("Cmd: SendInput failed:" + e.Message);
            }
        }

        public static void ShiftKey(String key, Boolean down) {
            MainWindow.AddLogEntry(String.Format("Cmd: {0} {1}", key, (down ? "down" : "up")));

            var sim = new InputSimulator();
            switch (key) {
                case "shift":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.SHIFT);
                    else sim.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
                    break;

                case "ctrl":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
                    else sim.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
                    break;

                case "alt":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.MENU);
                    else sim.Keyboard.KeyUp(VirtualKeyCode.MENU);
                    break;

                case "lwin":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.LWIN);
                    else sim.Keyboard.KeyUp(VirtualKeyCode.LWIN);
                    break;

                case "rwin":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.RWIN);
                    else sim.Keyboard.KeyUp(VirtualKeyCode.RWIN);
                    break;
            }
        }
    }
}