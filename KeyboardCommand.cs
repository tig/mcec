//-------------------------------------------------------------------
// By Charlie Kindel
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the BSD License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
//using Microsoft.Win32.Security;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl
{
    /// <summary>
    /// Simulates a keystroke including shift, ctrl, alt, and windows key 
    /// modifiers.
    /// </summary>
    [Serializable()]
    public class SendInputCommand : Command
    {
        [XmlAttribute("vk")]
        public ushort		vk = 0;
        [XmlAttribute("Shift")]
        public bool			Shift = false;
        [XmlAttribute("Ctrl")]
        public bool			Ctrl = false;
        [XmlAttribute("Alt")]
        public bool			Alt = false;
        [XmlAttribute("Win")]
        public bool			Win = false;

        public SendInputCommand()
        {
        }

        public SendInputCommand(ushort vk, bool Shift, bool Ctrl, bool Alt)
        {
            this.vk = vk;
            this.Shift = Shift;
            this.Ctrl = Ctrl;
            this.Alt = Alt;
            this.Win = false;
        }

        public SendInputCommand(ushort vk, bool Shift, bool Ctrl, bool Alt, bool Win) 
        {
            this.vk = vk;
            this.Shift = Shift;
            this.Ctrl = Ctrl;
            this.Alt = Alt;
            this.Win = Win;
        }

        unsafe public override void Execute()
        {
            try
            {
                String s =  Convert.ToChar(vk).ToString();
                if (Alt) s = "Alt-" + s;
                if (Ctrl) s = "Ctrl-" + s;
                if (Shift) s = "Shift-" + s;
                if (Win) s = "Win-" + s;
                MainWindow.AddLogEntry("Sending keystroke: " + s );

                var sim = new KeyboardSimulator();

                if (Shift)
                {
                    sim.KeyDown(VirtualKeyCode.SHIFT);
                }
                if (Ctrl)
                {
                    sim.KeyDown(VirtualKeyCode.CONTROL);
                }
                if (Alt)
                {
                    sim.KeyDown(VirtualKeyCode.MENU);
                }
                if (Win)
                {
                    sim.KeyDown(VirtualKeyCode.LWIN);
                }

                sim.KeyPress((VirtualKeyCode)vk);

                // Key up shift, ctrl, and/or alt
                if (Shift)
                {
                    sim.KeyUp(VirtualKeyCode.SHIFT);
                }
                if (Ctrl)
                {
                    sim.KeyUp(VirtualKeyCode.CONTROL);
                }
                if (Alt)
                {
                    sim.KeyUp(VirtualKeyCode.MENU);
                }
                if (Win)
                {
                    sim.KeyUp(VirtualKeyCode.LWIN);
                }
            }
            catch (Exception e)
            {
                MainWindow.AddLogEntry("SendInput failed:" + e.Message );
            }
        }

        static public void ShiftKey(String key, Boolean down)
        {
            MainWindow.AddLogEntry(String.Format("{0} {1}", key, (down ? "down" : "up")));

            var sim = new InputSimulator();
            switch (key)
            {
                case "shift":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.SHIFT); else sim.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
                    break;

                case "ctrl":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.CONTROL); else sim.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
                    break;

                case "alt":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.MENU); else sim.Keyboard.KeyUp(VirtualKeyCode.MENU);
                    break;

                case "lwin":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.LWIN); else sim.Keyboard.KeyUp(VirtualKeyCode.LWIN);
                    break;

                case "rwin":
                    if (down) sim.Keyboard.KeyDown(VirtualKeyCode.RWIN); else sim.Keyboard.KeyUp(VirtualKeyCode.RWIN);
                    break;
            }
        }


    }
}
