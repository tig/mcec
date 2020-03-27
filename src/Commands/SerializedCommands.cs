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
    public class SerializedCommands  {
        [XmlArray("commands")]
        [XmlArrayItem("chars", typeof(CharsCommand))]
        [XmlArrayItem("startprocess", typeof(StartProcessCommand))]
        [XmlArrayItem("sendinput", typeof(SendInputCommand))]
        [XmlArrayItem("sendmessage", typeof(SendMessageCommand))]
        [XmlArrayItem("setforegroundwindow", typeof(SetForegroundWindowCommand))]
        [XmlArrayItem("shutdown", typeof(ShutdownCommand))]
        [XmlArrayItem("pause", typeof(PauseCommand))]
        [XmlArrayItem("mouse", typeof(MouseCommand))]
        [XmlArrayItem(typeof(Command))]
        // XmlSerialization does not work with List<>. Must use an array.
        // Must be public for serialization to work
#pragma warning disable CA1051 // Do not declare visible instance fields
        public Command[] commandArray;
#pragma warning restore CA1051 // Do not declare visible instance fields

        [XmlIgnore] public int Count { get => commandArray.Length;  }
          
        public SerializedCommands() {
        }

        /// <summary>
        /// Load commands from the .commands file embedded in the .exe resources.
        /// </summary>
        /// <returns></returns>
        public static SerializedCommands LoadBuiltInCommands() {
            // Load the built-in pre-defined commands from an assembly resource
            SerializedCommands cmds = Deserialize(Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.Builtin.commands"));
            if (cmds == null) {
                MessageBox.Show("Error parsing built-in .commands resource.");
                Logger.Instance.Log4.Info($"Commands: Error parsing built-in .commands resource.");
            }
            return cmds;
        }

        /// <summary>
        /// Load any over-rides from the MCECommands.commands file.
        /// </summary>
        static public SerializedCommands LoadUserCommands(string userCommandsFile) {
            SerializedCommands cmds = null;
            FileStream fs = null;
            try {
                Logger.Instance.Log4.Info($"Commands: Loading user-defined commands from {userCommandsFile}.");
                fs = new FileStream(userCommandsFile, FileMode.Open, FileAccess.Read);
                cmds = Deserialize(fs);
            }
            catch (FileNotFoundException) {
                Logger.Instance.Log4.Info($"Commands: {userCommandsFile} was not found; creating it.");

                // If the user .commands file is not found, create it
                Stream uc = Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.commands");
                FileStream ucFS = null;
                try {
                    ucFS = new FileStream(userCommandsFile, FileMode.Create, FileAccess.ReadWrite);
                    uc.CopyTo(ucFS);
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Info($"Commands: Could not create user-defined commands file {userCommandsFile}. {e.Message}");
                    Logger.DumpException(e);
                }
                finally {
                    if (uc != null) uc.Close();
                    if (ucFS != null) ucFS.Close();
                }
            }
            catch (Exception ex) {
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error with {userCommandsFile}. {ex.Message}");
                Logger.DumpException(ex);
            }
            finally {
                if (fs != null) fs.Close();
            }

            return cmds;
        }

        /// <summary>
        /// Given an XML .commands stream, converts all element and key names to lowercase
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
                xmlReader = new XmlTextReader(xmlStream);
#pragma warning restore CA3075 // Insecure DTD processing in XML

                // Transform XML to all lower case key and value names
#pragma warning disable CA3075 // Insecure DTD processing in XML
                xsltReader = new XmlTextReader(
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.xslt"));
#pragma warning restore CA3075 // Insecure DTD processing in XML
                var myXslTrans = new XslCompiledTransform();
                myXslTrans.Load(xsltReader);
                var stm = new MemoryStream();
                lcWriter = XmlWriter.Create(stm, new XmlWriterSettings() { Indent = false, OmitXmlDeclaration = false });
                myXslTrans.Transform(xmlReader, null, lcWriter);
                stm.Position = 0;
#pragma warning disable CA3075 // Insecure DTD processing in XML
                lcReader = new XmlTextReader(stm); // lower-case reader
#pragma warning restore CA3075 // Insecure DTD processing in XML

                cmds = (SerializedCommands)new XmlSerializer(typeof(SerializedCommands)).Deserialize(lcReader);
            }
            catch (InvalidOperationException ex) {
                Logger.Instance.Log4.Info($"Commands: No commands loaded. Error parsing .commands XML. {ex.Message} {ex.InnerException.Message}");
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
