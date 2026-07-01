//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
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

namespace MCEControl; 

/// <summary>
/// Serialzes to/from XML (.commands files)
/// IMPORTANT! Do not change the namespace or you will break existing installations 
/// </summary>
[XmlType(Namespace = "http://www.kindel.com/products/mcecontroller", TypeName = "mcecontroller")]
public class SerializedCommands {
#pragma warning disable CA3075 // Insecure DTD processing in XML
    // XmlComments - https://stackoverflow.com/a/46497304/297526
    [XmlAnyElement(Name = "XmlComment", Namespace = "mcecontroller", Order = 0)]
#pragma warning disable CA1822 // Member XmlComment does not access instance data and can be marked as static
    public XmlComment XmlComment { get => new XmlDocument().CreateComment(Properties.Resources.CommandsFileXmlComments); set { } }
#pragma warning restore CA1822

#pragma warning disable CA1051 // Do not declare visible instance fields
    [XmlAttribute("version")]
    public string Version = null!;

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
    [XmlArrayItem("capture", typeof(CaptureCommand))]
    [XmlArrayItem("query", typeof(QueryCommand))]
    [XmlArrayItem("find", typeof(FindCommand))]
    [XmlArrayItem("invoke", typeof(InvokeCommand))]
    [XmlArrayItem("drag", typeof(DragCommand))]
    [XmlArrayItem("click", typeof(ClickCommand))]
    [XmlArrayItem("displays", typeof(DisplaysCommand))]
    [XmlArrayItem("record", typeof(RecordCommand))]
    [XmlArrayItem(typeof(Command))]

    // XmlSerialization does not work with List<>. Must use an array.
    // Must be public for serialization to work
    public Command[] commandArray = null!;

    [XmlIgnore] public int Count { get => (commandArray == null ? 0 : commandArray.Length); }

    public SerializedCommands() {
    }

    /// <summary>
    /// Loads commands from an XML file. If `Version` attribute in file is missing, upgrade the file, 
    /// setting Enabled=true for all. If `Version` attribute is older, the file will be re-written
    /// with new schema (and Version #).
    /// </summary>
    /// <param name="userCommandsFile"></param>
    /// <param name="currentVersion">Version of running app</param>
    /// <returns></returns>
    static public SerializedCommands LoadCommands(string userCommandsFile, string currentVersion) {
        SerializedCommands? cmds = null;
        FileStream? fs = null;
        try {
            Logger.Instance.Log4.Info($"SerializedCommands: Loading user-defined commands from {userCommandsFile}");
            fs = new FileStream(userCommandsFile, FileMode.Open, FileAccess.Read);
            cmds = Deserialize(fs);

            if (cmds == null) {
                // Deserialization failed. We could automatically rewrite the file, but that would be rude.
                // Deserialize logged this
            }
            else {
                // Is this a legacy load? If so, enable all commands and warn user
                if (string.IsNullOrEmpty(cmds.Version)) {
                    string msg = $"{userCommandsFile} was created with a legacy version of MCE Controller.\n\nConverting it and enabling all commands it contains.\n\nDisable any commands that are not used using the Commands window.";
                    if (!AgentRuntime.Headless) {
                        MessageBox.Show(msg, Application.ProductName);
                    }
                    Logger.Instance.Log4.Info($"SerializedCommands: {msg}");
                    cmds.Version = currentVersion;
                    cmds.commandArray = [.. cmds.commandArray.Select(c => { c.Enabled = true; return c; })];
                }

                // If this was written by an older version, re-write it to update it. Use tolerant
                // parsing: informational/prerelease version strings (e.g. "2.4.2-branch.1+sha") are not
                // valid System.Version and would otherwise throw here, breaking .commands loading.
                if (!string.IsNullOrEmpty(cmds.Version)
                    && System.Version.TryParse(currentVersion, out System.Version? curV)
                    && System.Version.TryParse(cmds.Version, out System.Version? fileV)
                    && curV.CompareTo(fileV) > 0) {
                    Logger.Instance.Log4.Info($"SerializedCommands: Upgrading .commands file from v{cmds.Version}");
                    SaveCommands(userCommandsFile, cmds, currentVersion);
                }
            }
        }
        catch (FileNotFoundException) {
            Logger.Instance.Log4.Error($"SerializedCommands: {userCommandsFile} was not found");
        }
        catch (Exception ex) {
            string msg = $"No commands loaded. Error reading {userCommandsFile} - {ex.Message}.\n\nSee log file for details: {Logger.Instance.LogFile}\n\nFor help, open an issue at github.com/tig/mcec";
            if (!AgentRuntime.Headless) {
                MessageBox.Show(msg, currentVersion);
            }
            Logger.Instance.Log4.Error($"SerializedCommands: {msg}");
            Logger.DumpException(ex);
        }
        finally {
            if (fs != null) {
                fs.Close();
            }
        }
        return cmds!;
    }

    /// <summary>
    /// Saves .commands XML file
    /// </summary>
    /// <param name="userCommandsFile">path to file to save to</param>
    /// <param name="commands">commands to serialize</param>
    static public void SaveCommands(string userCommandsFile, SerializedCommands commands, string currentVersion) {
        if (commands == null) {
            throw new ArgumentNullException(nameof(commands));
        }
        // TODO: Emit comments: https://stackoverflow.com/questions/7385921/how-to-write-a-comment-to-an-xml-file-when-using-the-xmlserializer

        FileStream? ucFS = null;
        try {
            commands.Version = currentVersion;
            ucFS = new FileStream(userCommandsFile, FileMode.Create);
            new XmlSerializer(typeof(SerializedCommands)).Serialize(ucFS, commands);
        }
        catch (Exception e) {
            // TODO: Move MessageBox out of here into MainWindow
            string msg = $"Could not create commands file ({userCommandsFile}) - {e.Message}.\n\n" +
                         $"See log file for details: {Logger.Instance.LogFile}\n\n" +
                         $"For help, open an issue at github.com/tig/mcec";
            MessageBox.Show(msg, Application.ProductName);
            Logger.Instance.Log4.Error($"SerializedCommands: {msg}");
            Logger.DumpException(e);
        }
        finally {
            if (ucFS != null) {
                ucFS.Close();
            }
        }
    }

    /// <summary>
    /// Given an XML .commands stream, de-serializes, converting all element and key names to lowercase
    /// and returns a CommandTable
    /// </summary>
    /// <param name="xmlStream"></param>
    /// <returns></returns>
    private static SerializedCommands? Deserialize(Stream xmlStream) {
        SerializedCommands? cmds = null;
        XmlReader? xmlReader = null;
        XmlReader? xsltReader = null;
        XmlWriter? lcWriter = null;
        XmlReader? lcReader = null;
        try {
#pragma warning disable CA3075 // Insecure DTD processing in XML
            // Transform XML to all lower case key and value names
            xmlReader = new XmlTextReader(xmlStream);
            xsltReader = new XmlTextReader(
                Assembly.GetExecutingAssembly().GetManifestResourceStream("MCEControl.Resources.MCEControl.xslt")!);
            XslCompiledTransform myXslTrans = new XslCompiledTransform();
            myXslTrans.Load(xsltReader);
            MemoryStream stm = new MemoryStream();
            lcWriter = XmlWriter.Create(stm, new XmlWriterSettings() { Indent = false, OmitXmlDeclaration = false });
            myXslTrans.Transform(xmlReader, null, lcWriter);
            stm.Position = 0;
            lcReader = new XmlTextReader(stm); // lower-case reader

            cmds = (SerializedCommands)new XmlSerializer(typeof(SerializedCommands)).Deserialize(lcReader)!;
        }
        catch (InvalidOperationException ex) {
            Logger.Instance.Log4.Error($"SerializedCommands: No commands loaded. Error parsing .commands XML. {ex.FullMessage()}");
            Logger.DumpException(ex);
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"SerializedCommands: Error parsing .commands XML. {ex.Message}");
            Logger.DumpException(ex);
        }
        finally {
            if (xmlReader != null) {
                xmlReader.Dispose();
            }

            if (xsltReader != null) {
                xsltReader.Dispose();
            }

            if (lcWriter != null) {
                lcWriter.Dispose();
            }

            if (lcReader != null) {
                lcReader.Dispose();
            }
        }
        return cmds;
    }
}
