//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

//#define SERIALIZE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Base class for all Command types
/// IMPORANT: Be very careful changing this schema as it may break forward compat
/// </summary>
public abstract class Command : ICommand {
    private String cmd = null!;

    protected Command() {
        Enabled = false; // SECURITY: Explicity
        UserDefined = false; // TELEMERTRY: Explicit
    }
    public static List<Command> BuiltInCommands { get => []; }

    [XmlAttribute("cmd")]
    public string Cmd { get => cmd; set => cmd = value; }
    [XmlElement("chars", typeof(CharsCommand))]
    [XmlElement("startprocess", typeof(StartProcessCommand))]
    [XmlElement("sendinput", typeof(SendInputCommand))]
    [XmlElement("sendmessage", typeof(SendMessageCommand))]
    [XmlElement("setforegroundwindow", typeof(SetForegroundWindowCommand))]
    [XmlElement("shutdown", typeof(ShutdownCommand))]
    [XmlElement("pause", typeof(PauseCommand))]
    [XmlElement("mouse", typeof(MouseCommand))]
    [XmlElement("mceccommand", typeof(McecCommand))]
    [XmlElement("capture", typeof(CaptureCommand))]
    [XmlElement("query", typeof(QueryCommand))]
    [XmlElement("find", typeof(FindCommand))]
    [XmlElement("invoke", typeof(InvokeCommand))]
    [XmlElement("drag", typeof(DragCommand))]
    [XmlElement("launch", typeof(LaunchCommand))]
    [XmlElement("click", typeof(ClickCommand))]
    [XmlElement("displays", typeof(DisplaysCommand))]
    [XmlElement("record", typeof(RecordCommand))]
    [XmlElement(typeof(Command))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Serializable")]
    public List<Command> EmbeddedCommands { get; set; } = null!;

    [XmlAttribute("args")]
    public virtual string Args { get; set; } = null!;

    [XmlAttribute("enabled")]
    public bool Enabled { get; set; }
    public virtual Reply Reply { get; set; } = null!;

    public override string ToString() => $"Cmd=\"{Cmd}\" Args=\"{Args}\"";

    // TELEMETRY:
    // Ensure only built-in command names are collected
    [XmlIgnore]
    public bool UserDefined { get; set; }

    /// <summary>
    /// Whether executing this command can synthesize physical desktop input (keys/mouse/window
    /// messages). The dispatcher (#195) holds <see cref="AgentRuntime.InputGate"/> around
    /// <see cref="Execute"/> only when this is true — so a command that provably touches no input
    /// (e.g. <c>pause</c>) doesn't starve a concurrent agent <c>drag</c> for its whole duration.
    /// DEFAULTS TO TRUE; overrides must be conservative — claim false only when Execute can never
    /// emit input, directly or transitively. Getting this wrong re-opens the #113 interleaving hazard.
    /// </summary>
    internal virtual bool SynthesizesInput => true;

    /// <summary>
    /// Clones this command (a table prototype) for execution with a fresh <paramref name="reply"/>
    /// context. Built on <see cref="object.MemberwiseClone"/> so EVERY field — including all
    /// serializable subclass state, which is value/string-typed throughout — is copied by
    /// construction; a subclass cannot forget a property (#207, the old hand-copied field lists).
    /// The only reference-typed mutable members are then handled explicitly: <see cref="Reply"/> is
    /// replaced with the fresh context, and <see cref="EmbeddedCommands"/> is deep-cloned (each
    /// child cloned recursively, so children keep their own Enabled — the #183 bug is impossible
    /// here by construction). Virtual only for a subclass that gains genuinely non-mechanical clone
    /// semantics (e.g. deep-copying a future reference-typed member); field copying never needs an
    /// override.
    /// </summary>
    public virtual Command Clone(Reply reply) {
        Command clone = (Command)MemberwiseClone();
        clone.Reply = reply;
        if (this.EmbeddedCommands != null) {
            clone.EmbeddedCommands = [];
            foreach (Command next in this.EmbeddedCommands) {
                clone.EmbeddedCommands.Add(next.Clone(reply));
            }
        }
        return clone;
    }

    ICommand ICommand.Clone(Reply reply) => Clone(reply);

    /// <summary>
    /// Execute command. Derived classes must call base before processing in order to ensure
    /// only enabled commands get run, and to collect telemetry.
    /// </summary>
    /// <returns></returns>
    public virtual bool Execute() {
        if (!Enabled) {
            Logger.Instance.Log4.Info($"Command: Attempt to execute a disabled command ({Cmd})");
            Logger.Instance.Log4.Info($"         As of MCEC v2.2.1 commands are disabled by default.");
            Logger.Instance.Log4.Info($"         Edit mcec.commands to enable commands (change `Enabled=\"false\"' to 'Enabled=\"true\"').");
            return false;
        }
        // TELEMETRY: 
        // what: the number of commands of each type (key) received and executed
        // why: to understand what commands are used and which are not
        // how is PII protected: the name of the command, key, is not user definable
        TelemetryService.Instance.TrackMetric($"{(UserDefined ? "<userDefined>" : cmd)} Executed", 1);
        return true;
    }

    /// <summary>
    /// https://stackoverflow.com/questions/5411694/get-all-inherited-classes-of-an-abstract-class
    /// </summary>
    public static ICollection<Command> GetDerivedClassesCollection() {
        List<Command> objects = [];
        foreach (Type type in typeof(Command).Assembly.GetTypes()
            .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(Command)))) {
            objects.Add((Command)Activator.CreateInstance(type)!);
        }
        return objects;
    }
}
