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
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl {
    /// <summary>
    /// Simulates a keystroke including shift, ctrl, alt, and windows key 
    /// modifiers.
    /// </summary>
    [Serializable]
    public class SendInputCommand : Command, ICommand {
        private bool alt;
        private bool ctrl;
        private bool shift;
        private bool win;
        private string vk;

        [XmlAttribute("alt")] public bool Alt { get => alt; set => alt = value; }
        [XmlAttribute("ctrl")] public bool Ctrl { get => ctrl; set => ctrl = value; }
        [XmlAttribute("shift")] public bool Shift { get => shift; set => shift = value; }
        [XmlAttribute("win")] public bool Win { get => win; set => win = value; }
        [XmlAttribute("vk")] public string Vk { get => vk; set => vk = value; }
        private static List<Command> _builtins = new List<Command>() {
                new SendInputCommand($"shiftdown:", false, false, false, false),
                new SendInputCommand($"shiftup:", false, false, false, false),
                new SendInputCommand() { Cmd = "atlesc", vk = "VK_ESCAPE", Alt = true },
                new SendInputCommand() { Cmd="wintab", vk="VK_TAB", Win=true },
                new SendInputCommand() { Cmd="close", vk="VK_F4", Alt=true },
                new SendInputCommand() { Cmd="ctrl-F1", vk="0x70", Ctrl=true },
                new SendInputCommand() { Cmd="winkey", vk="VK_LWIN"},
                new SendInputCommand() { Cmd="desktop", vk="VK_D", Win=true },
                new SendInputCommand() { Cmd="winsearch", vk="VK_Q", Win=true},
                new SendInputCommand() { Cmd="Windows Explorer", vk="VK_E", Shift=false, Ctrl=false, Alt=false, Win=true },
                new SendInputCommand() { Cmd="Windows Desktop", vk="VK_D", Shift=false, Ctrl=false, Alt=false, Win=true },
                new SendInputCommand() { Cmd="run", vk="VK_R", Shift=false, Ctrl=false, Alt=false, Win=true },
                new SendInputCommand() { Cmd="Tile Left", vk="37", Shift=false, Ctrl=false, Alt=false, Win=true },
                new SendInputCommand() { Cmd="Tile Right", vk="39", Shift=false, Ctrl=false, Alt=false, Win=true },
                new SendInputCommand() { Cmd="back", vk="8", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="vol-", vk="VK_VOLUME_DOWN", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="vol+", vk="VK_VOLUME_UP", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="mute", vk="VK_VOLUME_MUTE", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="pause", vk="VK_MEDIA_PLAY_PAUSE", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="play", vk="VK_MEDIA_PLAY_PAUSE", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="ctrl-x", vk="VK_X", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="cc", vk="67", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="ch+", vk="187", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="ch-", vk="189", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="dvdaudio", vk="65", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="dvdmenu", vk="77", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="dvdsubtitle", vk="85", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="execute", vk="43", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="fwd", vk="70", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="guide", vk="71", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="prior", vk="8", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="livetv", vk="84", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="greenbutton", vk="13", Shift=false, Ctrl=false, Alt=true, Win=true },
                new SendInputCommand() { Cmd="mymusic", vk="77", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="mypictures", vk="73", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="mytv", vk="84", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="myvideos", vk="69", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="record", vk="82", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="recordedtv", vk="79", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="rew", vk="66", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="stop", vk="83", Shift=true, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="skipback", vk="66", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="skipfwd", vk="70", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="enter", vk="13", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="escape", vk="27", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="delete", vk="46", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="end", vk="35", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="left", vk="37", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="up", vk="38", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="right", vk="39", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="down", vk="40", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="help", vk="47", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="home", vk="36", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="insert", vk="45", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="select", vk="41", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="moreinfo", vk="68", Shift=false, Ctrl=true, Alt=false },
                new SendInputCommand() { Cmd="next", vk="34", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="ok", vk="13", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="print", vk="42", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="tab", vk="9", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="snapshot", vk="44", Shift=false, Ctrl=false, Alt=false },
                new SendInputCommand() { Cmd="zoom", vk="90", Shift=false, Ctrl=false, Alt=false },
            };
        public static new List<Command> BuiltInCommands {
            get => _builtins;
        }

        static SendInputCommand() {
            // Populate default VK_ codes
            foreach (VirtualKeyCode vk in Enum.GetValues(typeof(VirtualKeyCode))) {
                string s;
                if (vk > VirtualKeyCode.HELP && vk < VirtualKeyCode.LWIN) {
                    s = vk.ToString();  // already have VK_
                }
                else {
                    s = "VK_" + vk.ToString();
                }
                _builtins.Add(new SendInputCommand(s, false, false, false, false));
            }
        }

        public SendInputCommand() { }

        public SendInputCommand(string vk, bool shift, bool ctrl, bool alt) {
            Cmd = Vk = vk;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
            Win = false;
        }

        public SendInputCommand(string vk, bool shift, bool ctrl, bool alt, bool win) {
            Cmd = Vk = vk;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
            Win = win;
        }

        public override string ToString() {
            return $"Cmd=\"{Cmd}\" Args=\"{Args}\" Vk=\"{Vk}\" Shift=\"{Shift}\" Ctrl=\"{Ctrl}\" Alt=\"{Alt}\" Win=\"{Win}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new SendInputCommand(vk, shift, ctrl, alt, win));

        private bool ExecuteShiftCmd(string cmd) {
            // TODO: Break this out to a separate command
            if (!string.IsNullOrEmpty(cmd)) {
                switch (cmd.ToLowerInvariant()) {
                    case "shiftdown:":
                        // Modifyer key down
                        SendInputCommand.ShiftKey(Args, true);
                        return true;

                    case "shiftup:":
                        // Modifyer key down
                        SendInputCommand.ShiftKey(Args, false);
                        return true;
                }
            }
            return false;
        }

        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            // Forms:
            // Vk = "VK_..." - Simulates keypress of VK_...
            // Vk = "0X_..." - Simulates keypress of keycode 0X..."
            // Vk = "<char>" - Simulates keypress of keycode for <char>

            try {
                // Deal with shiftdown/up: commands
                if (ExecuteShiftCmd(Cmd)) {
                    return true;
                }

                VirtualKeyCode vkcode = 0;
                if (Vk.Length == 1) {
                    // Deal with <SendInput Vk="x"/> - Vk includes an explicit 'x' char.
                    // ASCII of x 0x78. ASCII of X is 0x58. VK_X = 0x58m. Thus convert to
                    // upper-case.
                    vkcode = (VirtualKeyCode)Vk.ToUpperInvariant().ToCharArray()[0];
                }
                else {
                    if (!Vk.StartsWith("vk_", StringComparison.InvariantCultureIgnoreCase) ||
                        (!Enum.TryParse(Vk.ToLowerInvariant(), true, out vkcode) &&
                         !Enum.TryParse(Vk.ToLowerInvariant().Substring(3), true, out vkcode))) {
                        // It's not a VK_ string. Is it Hex?
                        if ((!Vk.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) ||
                             !ushort.TryParse(Vk.Substring(2), NumberStyles.HexNumber,
                                              CultureInfo.InvariantCulture.NumberFormat, out var num)) &&
                             !ushort.TryParse(Vk, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat,
                                             out num)) {

                            // Not Hex. How about a Unicode escape? If this regex fails to match, the first 
                            // char of Vk will be used. 
                            Regex rx = new Regex(@"\\[uU]([0-9A-F]{4})");
                            num = rx.Replace(Vk, match => ((char)Int32.Parse(match.Value.Substring(2), NumberStyles.HexNumber)).ToString()).ToUpperInvariant().ToCharArray()[0];
                        }
                        vkcode = (VirtualKeyCode)num;
                    }
                }

                string s;
                // it's a single char; convert to upper case
                if (vkcode > VirtualKeyCode.HELP && vkcode < VirtualKeyCode.LWIN) {
                    s = $"{Char.ToUpper((char)vkcode, CultureInfo.InvariantCulture)}";
                }
                else {
                    s = "VK_" + vkcode.ToString();
                }

                if (Alt) {
                    s = "Alt-" + s;
                }

                if (Ctrl) {
                    s = "Ctrl-" + s;
                }

                if (Shift) {
                    s = "Shift-" + s;
                }

                if (Win) {
                    s = "Win-" + s;
                }

                Logger.Instance.Log4.Info($"{this.GetType().Name} {ToString()} ({s}) (0x{(ushort)vkcode:x2})");

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
                Logger.Instance.Log4.Info($"{this.GetType().Name}: failed. {e.Message}");
                return false;
            }
            return true;
        }

        public static void ShiftKey(String key, Boolean down) {
            Logger.Instance.Log4.Info($"ShiftKey: {key} {(down ? "down" : "up")}");

            var sim = new InputSimulator();
            switch (key) {
                case "shift":
                    if (down) {
                        sim.Keyboard.KeyDown(VirtualKeyCode.SHIFT);
                    }
                    else {
                        sim.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
                    }

                    break;

                case "ctrl":
                    if (down) {
                        sim.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
                    }
                    else {
                        sim.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
                    }

                    break;

                case "alt":
                    if (down) {
                        sim.Keyboard.KeyDown(VirtualKeyCode.MENU);
                    }
                    else {
                        sim.Keyboard.KeyUp(VirtualKeyCode.MENU);
                    }

                    break;

                case "lwin":
                    if (down) {
                        sim.Keyboard.KeyDown(VirtualKeyCode.LWIN);
                    }
                    else {
                        sim.Keyboard.KeyUp(VirtualKeyCode.LWIN);
                    }

                    break;

                case "rwin":
                    if (down) {
                        sim.Keyboard.KeyDown(VirtualKeyCode.RWIN);
                    }
                    else {
                        sim.Keyboard.KeyUp(VirtualKeyCode.RWIN);
                    }

                    break;

                default:
                    Logger.Instance.Log4.Info($"ShiftKey: No shift key specified");
                    break;
            }
        }
    }
}
