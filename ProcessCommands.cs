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
    /// Summary description for StartProcessCommands.
    /// </summary>
    public class StartProcessCommand : Command
    {

        [XmlAttribute("File")]
        public String File;

        [XmlElement("StartProcess", typeof(StartProcessCommand))]
        [XmlElement("SendInput", typeof(SendInputCommand))]
        [XmlElement("SendMessage", typeof(SendMessageCommand))]
        [XmlElement(typeof(Command))]
        public Command nextCommand = null;

        public StartProcessCommand() {}

        public StartProcessCommand(String File)
        {
            this.File = File;
        }

        public StartProcessCommand(String File, Command cmd): this(File)
        {
           this.nextCommand = cmd;
        }

        public override void Execute()
        {
            MainWindow.AddLogEntry("Starting process: " + File );
            Process p = new Process();
            p.StartInfo.FileName = File;
            p.Start();
            if (nextCommand != null)
                p.WaitForInputIdle(10000);

            if (nextCommand != null)
                nextCommand.Execute();
        }
    }
}
