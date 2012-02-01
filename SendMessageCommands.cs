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
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32.Security;

namespace MCEControl
{
	using Microsoft.Win32.Security.Win32Structs;
	using HWND = System.IntPtr;
	using DWORD = System.UInt32;

	/// <summary>
	/// Summary description for SendMessageCommand.
	/// </summary>
	public class SendMessageCommand : Command
	{
        [XmlAttribute("ClassName")]
        public String ClassName { get; set; }

        [XmlAttribute("WindowName")]
        public String WindowName { get; set; }

        [XmlAttribute("Msg")]
        public int Msg;

		[XmlAttribute("wParam")]
		public int wParam;

        // This is int so that -1 can be specified in the XML
        [XmlAttribute("lParam")]
        public int lParam;

		public SendMessageCommand()
		{
		}

		public SendMessageCommand(String ClassName, String WindowName, DWORD Msg, DWORD wParam, DWORD lParam)
		{
			this.ClassName = ClassName;
			this.WindowName = WindowName;
			this.Msg = (int)Msg;
			this.wParam = (int)wParam;
			this.lParam = (int)lParam;
		}

		public override void Execute()
		{
			try
			{
				unsafe
				{
					if (ClassName != null)
					{
						Process[] procs = Process.GetProcessesByName(ClassName);
						if (procs.Length > 0)
						{
							HWND h = procs[0].MainWindowHandle;

                            MainWindow.AddLogEntry(String.Format("SendMessage ({0}): {1} {2} {3}", ClassName, Msg, wParam, lParam));
                            Win32.SendMessage(h, (DWORD)Msg, (DWORD)wParam, (DWORD)lParam);
						}
						else
						{
							MainWindow.AddLogEntry("GetProcessByName for " + ClassName + " failed");
						}
					}
					else
					{
						HWND h = Win32.GetForegroundWindow();
						MainWindow.AddLogEntry(String.Format("SendMessage (forground window): {0} {1} {2}", Msg, wParam, lParam));
                        Win32.SendMessage(h, (DWORD)Msg, (DWORD)wParam, (DWORD)lParam);
                    }
				}
			}
			catch(Exception e)
			{
				MainWindow.AddLogEntry("SendMessageCommand.Execute failed for " + ClassName + " with error: " + e.Message);
			}
		}
	}
}
