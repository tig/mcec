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

    // SERIALIZATION (#204): the [XmlArray("commands", Order = 1)] wrapper and the polymorphic
    // [XmlArrayItem("name", typeof(T))] map (one per command type, formerly hardcoded here) now
    // come from CommandRegistry.CreateXmlOverrides(), applied via the cached Serializer below —
    // register a new command type there, not here.
    //
    // XmlSerialization does not work with List<>. Must use an array.
    // Must be public for serialization to work
    public Command[] commandArray = null!;

    [XmlIgnore] public int Count { get => (commandArray == null ? 0 : commandArray.Length); }

    /// <summary>
    /// THE XmlSerializer for .commands files, wired to the one explicit command registry (#204) via
    /// XmlAttributeOverrides. CRITICAL: serializers constructed WITH overrides are NOT cached by the
    /// runtime — every construction emits a fresh dynamic assembly that is never unloaded (a leak).
    /// This static is the process-wide cache; XmlSerializer instance methods are thread-safe. Always
    /// use it — never write `new XmlSerializer(typeof(SerializedCommands), ...)` at a call site.
    /// </summary>
    private static readonly XmlSerializer Serializer = new(typeof(SerializedCommands), CommandRegistry.CreateXmlOverrides());

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

        // First run (and provisioned/demo subject copies): create the default commands file, the
        // same way SettingsStore creates a default mcec.settings — the full built-in catalog with
        // every command Enabled="false" (nothing enabled, so no actuation surface changes), per the
        // long-documented contract in docs/home-automation.md.
        if (!File.Exists(userCommandsFile)) {
            TryCreateDefaultCommandsFile(userCommandsFile, currentVersion);
        }

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
            // Only reachable when TryCreateDefaultCommandsFile above could not write (e.g. a
            // read-only location). Not an error: MCEC runs fully on built-ins without the file.
            Logger.Instance.Log4.Info($"SerializedCommands: No user commands file ({userCommandsFile}); using built-in commands only.");
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
            Serializer.Serialize(ucFS, commands);
        }
        catch (Exception e) {
            string msg = $"Could not create commands file ({userCommandsFile}) - {e.Message}.\n\n" +
                         $"See log file for details: {Logger.Instance.LogFile}\n\n" +
                         $"For help, open an issue at github.com/tig/mcec";
            // #209: same headless gate as LoadCommands above — in --mcp mode there is no operator
            // and stdout is the protocol stream, so a failed save must log, never block on a dialog
            // no one can dismiss (SessionProvisioner saves .commands headless).
            if (!AgentRuntime.Headless) {
                MessageBox.Show(msg, Application.ProductName);
            }
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
    /// Creates the default commands file on first run: every built-in command from
    /// <see cref="CommandRegistry"/>, all with <c>Enabled="false"</c>, version-stamped, and carrying
    /// the standard guidance comments. This is the contract docs/home-automation.md has always
    /// described ("containing all built-in commands with Enabled=false") and it is what makes the
    /// security model's enable-a-command workflow real: the user flips Enabled on an existing entry
    /// instead of authoring XML from scratch. Nothing enabled → the actuation surface is unchanged.
    /// Mirrors SettingsStore's create-default-settings behavior. Quiet on failure (Info/Warn logs
    /// only, never a dialog): a location we cannot write to just means MCEC keeps running on
    /// built-ins, and LoadCommands' FileNotFoundException fallback reports that.
    /// Internal so tests can exercise the failure path directly (InternalsVisibleTo MCEControl.xUnit).
    /// </summary>
    internal static void TryCreateDefaultCommandsFile(string userCommandsFile, string currentVersion) {
        try {
            SerializedCommands defaults = new() {
                Version = currentVersion,
                // The registry's built-in factories produce Enabled=false instances
                // (pinned by CommandRegistryTests) — serialize them as-is.
                commandArray = [.. CommandRegistry.Entries.SelectMany(e => e.BuiltIns())],
            };
            // CreateNew: if another instance raced us to it, theirs wins and our load proceeds.
            using FileStream fs = new(userCommandsFile, FileMode.CreateNew, FileAccess.Write);
            Serializer.Serialize(fs, defaults);
            Logger.Instance.Log4.Info($"SerializedCommands: Created default commands file ({defaults.Count} built-in commands, all disabled): {userCommandsFile}");
        }
        catch (IOException e) {
            Logger.Instance.Log4.Warn($"SerializedCommands: Could not create default commands file ({userCommandsFile}): {e.Message}");
        }
        catch (UnauthorizedAccessException e) {
            Logger.Instance.Log4.Warn($"SerializedCommands: Could not create default commands file ({userCommandsFile}): {e.Message}");
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

            cmds = (SerializedCommands)Serializer.Deserialize(lcReader)!;
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
