//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
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
	/// Summary description for SetForegroundWindowCommand.
	/// </summary>
	public class SetForegroundWindowCommand : Command
	{
		[XmlAttribute("ClassName")]
		public String ClassName;
		[XmlAttribute("WindowName")]
		public String WindowName;

		public SetForegroundWindowCommand()
		{
		}

		public SetForegroundWindowCommand(String ClassName, String WindowName)
		{
			this.ClassName = ClassName;
			this.WindowName = WindowName;
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

							MainWindow.AddLogEntry("SetForegroundWindow(\"" + ClassName + "\")");
							Win32.SetForegroundWindow(h);
						}
						else
						{
							MainWindow.AddLogEntry("GetProcessByName for " + ClassName + " failed");
						}
					}
				}
			}
			catch(Exception e)
			{
				MainWindow.AddLogEntry("SetForegroundWindowCommand.Execute failed for " + ClassName + " with error: " + e.Message);
			}
		}
	}
}
