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
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl
{
	/// <summary>
	/// Summary description for ShutdownCommand.
	/// </summary>
	public class ShutdownCommand : Command
	{
		[XmlAttribute("Type")]
		public String Type;

		public ShutdownCommand()
		{
		}

		public ShutdownCommand(string type)
		{
			Type = type;
		}

		public override void Execute()
		{
			MainWindow.AddLogEntry("ShutdownCommand: " + Type );

			SystemControl sc = new SystemControl();
			switch (Type.ToLower())
			{
				case "shutdown":
					sc.Shutdown("MCE Controller Forced Shutdown", 30, true, false);
					break;

				case "restart":
					sc.Shutdown("MCE Controller Forced Restart", 30, true, true);
					break;

				case "standby":
					sc.Standby();
					break;

				case "hibernate":
					sc.Hibernate();
					break;

				case "abort":
					sc.Abort();
					break;
			}

			sc.Dispose(true);
		}
	}
}
