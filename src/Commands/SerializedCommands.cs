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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;

namespace MCEControl {

    /// <summary>
    /// Serialzes to/from XML (.commands files)
    /// IMPORTANT! Do not change the namespace or you will break existing installations 
    /// </summary>
    [XmlType(Namespace = "http://www.kindel.com/products/mcecontroller", TypeName = "mcecontroller")]
    public class SerializedCommands {
#pragma warning disable CA3075 // Insecure DTD processing in XML
        // XmlComments - https://stackoverflow.com/a/46497304/297526
        [XmlAnyElement(Name = "XmlComment", Namespace ="mcecontroller", Order = 0)]
        public XmlComment XmlComment { get => new XmlDocument().CreateComment(Properties.Resources.CommandsFileXmlComments); set { } }

#pragma warning disable CA1051 // Do not declare visible instance fields
        [XmlAttribute("version")]
        public string Version;

        [XmlArray("commands", Order = 1)]
        [XmlArrayItem("chars", typeof(CharsCommand))]
        [XmlArrayItem("startprocess", typeof(StartProcessCommand))]
        [XmlArrayItem("sendinput", typeof(SendInputCommand))]
        [XmlArrayItem("sendmessage", typeof(SendMessageCommand))]
        [XmlArrayItem("setforegroundwindow", typeof(SetForegroundWindowCommand))]
        [XmlArrayItem("shutdown", typeof(ShutdownCommand))]
        [XmlArrayItem("pause", typeof(PauseCommand))]
        [XmlArrayItem("mouse", typeof(MouseCommand))]
        [XmlArrayItem("mceccommand", typeof(McecCommand))]
        [XmlArrayItem(typeof(Command))]

        // XmlSerialization does not work with List<>. Must use an array.
        // Must be public for serialization to work
        public Command[] commandArray;

        [XmlIgnore] public int Count { get => (commandArray == null ? 0 : commandArray.Length); }

        public SerializedCommands() {
        }

        /// <summary>
        /// Load any over-rides from the MCECommands.commands file.
        /// </summary>
        static public SerializedCommands LoadCommands(string userCommandsFile) {
            SerializedCommands cmds = null;
            FileStream fs = null;
            try {
                Logger.Instance.Log4.Info($"Commands: Loading user-defined commands from {userCommandsFile}");
                fs = new FileStream(userCommandsFile, FileMode.Open, FileAccess.Read);
                cmds = Deserialize(fs);

                // Is this a legacy load? If so, enable all commands and warn user
                if (string.IsNullOrEmpty(cmds.Version)) {
                    var msg = $"{userCommandsFile} was created with a legacy version of MCE Controller.\n\nConverting it and enabling all commands it contains.\n\nDisable any commands that are not used using the Commands window.";
                    MessageBox.Show(msg, Application.ProductName);
                    Logger.Instance.Log4.Info($"Commands: {msg}");
                    cmds.commandArray = cmds.commandArray.Select(c => { c.Enabled = true; return c; }).ToArray();
                }
            }
            catch (FileNotFoundException) {
                Logger.Instance.Log4.Info($"Commands: {userCommandsFile} was not found");
            }
            catch (Exception ex) {
                var msg = $"No commands loaded. Error reading {userCommandsFile} - {ex.Message}.\n\nSee log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec";
                MessageBox.Show(msg, Application.ProductName);
                Logger.Instance.Log4.Info($"Commands: {msg}");
                Logger.DumpException(ex);
            }
            finally {
                if (fs != null) fs.Close();
            }
            return cmds;
        }

        /// <summary>
        /// Creates .commands file
        /// </summary>
        static public void SaveCommands(string userCommandsFile, SerializedCommands commands) {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            // TODO: Emit comments: https://stackoverflow.com/questions/7385921/how-to-write-a-comment-to-an-xml-file-when-using-the-xmlserializer

            FileStream ucFS = null;
            try {
                commands.Version = Application.ProductVersion;
                ucFS = new FileStream(userCommandsFile, FileMode.Create, FileAccess.ReadWrite);
                new XmlSerializer(typeof(SerializedCommands)).Serialize(ucFS, commands);
            }
            catch (Exception e) {
                var msg = $"Could not create commands file ({userCommandsFile}) - {e.Message}.\n\nSee log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec";
                MessageBox.Show(msg, Application.ProductName);
                Logger.Instance.Log4.Info($"Commands: {msg}");
                Logger.DumpException(e);
            }
            finally {
                if (ucFS != null) ucFS.Close();
            }
        }

        /// <summary>
        /// Given an XML .commands stream, de-serializes, converting all element and key names to lowercase
        /// and returns a CommandTable
        /// </summary>
        /// <param name="xmlStream"></param>
        /// <returns></returns>
        private static SerializedCommands Deserialize(Stream xmlStream) {
            SerializedCommands cmds = null;
            XmlReader xmlReader = null;
            XmlReader xsltReader = null;
            XmlWriter lcWriter = null;
            XmlReader lcReader = null;
            try {
#pragma warning disable CA3075 // Insecure DTD processing in XML
                // Transform XML to all lower case key and value names
                xmlReader = new XmlTextReader(xmlStream);
                xsltReader = new XmlTextReader(
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.xslt"));
                var myXslTrans = new XslCompiledTransform();
                myXslTrans.Load(xsltReader);
                var stm = new MemoryStream();
                lcWriter = XmlWriter.Create(stm, new XmlWriterSettings() { Indent = false, OmitXmlDeclaration = false });
                myXslTrans.Transform(xmlReader, null, lcWriter);
                stm.Position = 0;
                lcReader = new XmlTextReader(stm); // lower-case reader

                cmds = (SerializedCommands)new XmlSerializer(typeof(SerializedCommands)).Deserialize(lcReader);
            }
            catch (InvalidOperationException ex) {
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing .commands XML. {ex.FullMessage()}");
                Logger.DumpException(ex);
            }
            catch (Exception ex) {
                Logger.Instance.Log4.Info($"Commands: Error parsing .commands XML. {ex.Message}");
                Logger.DumpException(ex);
            }
            finally {
                if (xmlReader != null) xmlReader.Dispose();
                if (xsltReader != null) xsltReader.Dispose();
                if (lcWriter != null) lcWriter.Dispose();
                if (lcReader != null) lcReader.Dispose();
            }
            return cmds;
        }
    }
}
