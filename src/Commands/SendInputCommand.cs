//-------------------------------------------------------------------
// Copyright © 2017 Kindel, LLC
// http://www.kindel.com
// 
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

namespace MCEControl; 
/// <summary>
/// Simulates a keystroke including shift, ctrl, alt, and windows key 
/// modifiers.
/// </summary>
[Serializable]
public class SendInputCommand : Command {
    private bool _alt;
    private bool _ctrl;
    private bool _shift;
    private bool _win;
    private string _vk = null!;

    [XmlAttribute("alt")] public bool Alt { get => _alt; set => _alt = value; }
    [XmlAttribute("ctrl")] public bool Ctrl { get => _ctrl; set => _ctrl = value; }
    [XmlAttribute("shift")] public bool Shift { get => _shift; set => _shift = value; }
    [XmlAttribute("win")] public bool Win { get => _win; set => _win = value; }
    [XmlAttribute("vk")] public string Vk { get => _vk; set => _vk = value; }
    private static List<Command> _builtins = [
            new SendInputCommand("shiftdown:", false, false, false, false),
            new SendInputCommand("shiftup:", false, false, false, false),
            new SendInputCommand() { Cmd = "atlesc", Vk = "VK_ESCAPE", Alt = true },
            new SendInputCommand() { Cmd="wintab", Vk="VK_TAB", Win=true },
            new SendInputCommand() { Cmd="close", Vk="VK_F4", Alt=true },
            new SendInputCommand() { Cmd="ctrl-F1", Vk="0x70", Ctrl=true },
            new SendInputCommand() { Cmd="winkey", Vk="VK_LWIN"},
            new SendInputCommand() { Cmd="desktop", Vk="VK_D", Win=true },
            new SendInputCommand() { Cmd="winsearch", Vk="VK_Q", Win=true},
            new SendInputCommand() { Cmd="Windows Explorer", Vk="VK_E", Shift=false, Ctrl=false, Alt=false, Win=true },
            new SendInputCommand() { Cmd="Windows Desktop", Vk="VK_D", Shift=false, Ctrl=false, Alt=false, Win=true },
            new SendInputCommand() { Cmd="run", Vk="VK_R", Shift=false, Ctrl=false, Alt=false, Win=true },
            new SendInputCommand() { Cmd="Tile Left", Vk="37", Shift=false, Ctrl=false, Alt=false, Win=true },
            new SendInputCommand() { Cmd="Tile Right", Vk="39", Shift=false, Ctrl=false, Alt=false, Win=true },
            new SendInputCommand() { Cmd="back", Vk="8", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="vol-", Vk="VK_VOLUME_DOWN", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="vol+", Vk="VK_VOLUME_UP", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="mute", Vk="VK_VOLUME_MUTE", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="pause", Vk="VK_MEDIA_PLAY_PAUSE", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="play", Vk="VK_MEDIA_PLAY_PAUSE", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="ctrl-x", Vk="VK_X", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ctrl-a", Vk="VK_A", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ctrl-c", Vk="VK_C", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ctrl-v", Vk="VK_V", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ctrl-z", Vk="VK_Z", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ctrl-s", Vk="VK_S", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="cc", Vk="67", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ch+", Vk="187", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="ch-", Vk="189", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="dvdaudio", Vk="65", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="dvdmenu", Vk="77", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="dvdsubtitle", Vk="85", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="execute", Vk="43", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="fwd", Vk="70", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="guide", Vk="71", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="prior", Vk="8", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="livetv", Vk="84", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="greenbutton", Vk="13", Shift=false, Ctrl=false, Alt=true, Win=true },
            new SendInputCommand() { Cmd="mymusic", Vk="77", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="mypictures", Vk="73", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="mytv", Vk="84", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="myvideos", Vk="69", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="record", Vk="82", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="recordedtv", Vk="79", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="rew", Vk="66", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="stop", Vk="83", Shift=true, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="skipback", Vk="66", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="skipfwd", Vk="70", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="enter", Vk="13", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="escape", Vk="27", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="delete", Vk="46", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="end", Vk="35", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="left", Vk="37", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="up", Vk="38", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="right", Vk="39", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="down", Vk="40", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="help", Vk="47", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="home", Vk="36", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="insert", Vk="45", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="select", Vk="41", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="moreinfo", Vk="68", Shift=false, Ctrl=true, Alt=false },
            new SendInputCommand() { Cmd="next", Vk="34", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="ok", Vk="13", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="print", Vk="42", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="tab", Vk="9", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="snapshot", Vk="44", Shift=false, Ctrl=false, Alt=false },
            new SendInputCommand() { Cmd="zoom", Vk="90", Shift=false, Ctrl=false, Alt=false },
        ];
    public static List<Command> BuiltInCommands {
        get => _builtins;
    }

    static SendInputCommand() {
        // Populate default VK_ codes
        foreach (VirtualKeyCode vk in Enum.GetValues<VirtualKeyCode>()) {
            string s;
            if (vk is > VirtualKeyCode.HELP and < VirtualKeyCode.LWIN) {
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

            VirtualKeyCode vkcode;
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
                         !ushort.TryParse(Vk.AsSpan(2), NumberStyles.HexNumber,
                                          CultureInfo.InvariantCulture.NumberFormat, out ushort num)) &&
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
            if (vkcode is > VirtualKeyCode.HELP and < VirtualKeyCode.LWIN) {
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

            KeyboardSimulator sim = new KeyboardSimulator();

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
            Logger.Instance.Log4.Error($"{this.GetType().Name}: failed. {e.Message}");
            return false;
        }
        return true;
    }

    public static void ShiftKey(String key, Boolean down) {
        Logger.Instance.Log4.Info($"ShiftKey: {key} {(down ? "down" : "up")}");

        InputSimulator sim = new InputSimulator();
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
                Logger.Instance.Log4.Info("ShiftKey: No shift key specified");
                break;
        }
    }
}
