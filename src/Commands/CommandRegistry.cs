// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// THE single place a command type is registered (#204). Adding a command used to require edits in
/// three invisible places, each failing silently when forgotten:
/// <list type="number">
/// <item>an <c>[XmlElement("name", typeof(T))]</c> on <see cref="Command.EmbeddedCommands"/>,</item>
/// <item>the same list repeated as <c>[XmlArrayItem]</c> on <see cref="SerializedCommands.commandArray"/>,</item>
/// <item>a magic <c>public static new BuiltInCommands</c> property discovered by reflection
/// (without <c>FlattenHierarchy</c>; misdeclare it and the command registered nothing).</item>
/// </list>
/// Now ONE entry in <see cref="Entries"/> drives all three: the serializer's polymorphic
/// element-name maps (<see cref="CreateXmlOverrides"/>), the invoker's built-ins table
/// (<c>CommandInvoker.CreateBuiltIns</c>), and the hygiene gate
/// (<c>CommandRegistryTests</c>; an unregistered command type is a red build, plus the
/// <see cref="DebugAssertComplete"/> debug backstop).
///
/// <para><b>To add a command type</b>: derive from <see cref="Command"/> (agent tools: from the
/// gated <c>AgentCommand</c> bases, #208), give it a <c>public static List&lt;Command&gt;
/// BuiltInCommands</c> property, and add ONE line to <see cref="Entries"/>.</para>
/// </summary>
public static class CommandRegistry {
    /// <summary>
    /// One line per command type. Order matches the legacy attribute lists for wire-format parity.
    /// WIRE FORMAT: XmlName is what .commands files contain; never rename an existing entry.
    /// </summary>
    public static IReadOnlyList<CommandRegistryEntry> Entries { get; } = [
        new("chars", typeof(CharsCommand), () => CharsCommand.BuiltInCommands),
        new("startprocess", typeof(StartProcessCommand), () => StartProcessCommand.BuiltInCommands),
        new("sendinput", typeof(SendInputCommand), () => SendInputCommand.BuiltInCommands),
        new("sendmessage", typeof(SendMessageCommand), () => SendMessageCommand.BuiltInCommands),
        new("setforegroundwindow", typeof(SetForegroundWindowCommand), () => SetForegroundWindowCommand.BuiltInCommands),
        new("shutdown", typeof(ShutdownCommand), () => ShutdownCommand.BuiltInCommands),
        new("pause", typeof(PauseCommand), () => PauseCommand.BuiltInCommands),
        new("mouse", typeof(MouseCommand), () => MouseCommand.BuiltInCommands),
        new("mceccommand", typeof(McecCommand), () => McecCommand.BuiltInCommands),
        new("capture", typeof(CaptureCommand), () => CaptureCommand.BuiltInCommands),
        new("get-text", typeof(GetTextCommand), () => GetTextCommand.BuiltInCommands),
        new("query", typeof(QueryCommand), () => QueryCommand.BuiltInCommands),
        new("find", typeof(FindCommand), () => FindCommand.BuiltInCommands),
        new("invoke", typeof(InvokeCommand), () => InvokeCommand.BuiltInCommands),
        new("drag", typeof(DragCommand), () => DragCommand.BuiltInCommands),
        new("launch", typeof(LaunchCommand), () => LaunchCommand.BuiltInCommands),
        new("click", typeof(ClickCommand), () => ClickCommand.BuiltInCommands),
        new("focus", typeof(FocusCommand), () => FocusCommand.BuiltInCommands),
        new("displays", typeof(DisplaysCommand), () => DisplaysCommand.BuiltInCommands),
        new("clipboard", typeof(ClipboardCommand), () => ClipboardCommand.BuiltInCommands),
        new("record", typeof(RecordCommand), () => RecordCommand.BuiltInCommands),
        new("windows", typeof(WindowsCommand), () => WindowsCommand.BuiltInCommands),
        new("window", typeof(WindowCommand), () => WindowCommand.BuiltInCommands),
    ];

    /// <summary>
    /// Builds the serializer attribute overrides that used to live as hardcoded attribute lists on
    /// <see cref="Command.EmbeddedCommands"/> (the <c>[XmlElement]</c> set) and
    /// <see cref="SerializedCommands.commandArray"/> (the <c>[XmlArrayItem]</c> set). An override
    /// REPLACES the member's reflected attributes wholesale, so the <c>commandArray</c> wrapper
    /// (<c>[XmlArray("commands", Order = 1)]</c>; Order required because the sibling
    /// <c>XmlComment</c> member carries <c>Order = 0</c>) is restated here too.
    ///
    /// CRITICAL (caller contract): an <see cref="XmlSerializer"/> constructed WITH overrides is NOT
    /// cached by the runtime; each construction emits a new dynamic assembly that is never
    /// unloaded. The ONLY consumer must be a cached static serializer
    /// (<c>SerializedCommands._serializer</c>); never call this per-serialize.
    /// </summary>
    internal static XmlAttributeOverrides CreateXmlOverrides() {
        XmlAttributes embedded = new();
        XmlAttributes arrayItems = new() {
            XmlArray = new XmlArrayAttribute("commands") { Order = 1 },
        };
        foreach (CommandRegistryEntry entry in Entries) {
            embedded.XmlElements.Add(new XmlElementAttribute(entry.XmlName, entry.CommandType));
            arrayItems.XmlArrayItems.Add(new XmlArrayItemAttribute(entry.XmlName, entry.CommandType));
        }
        // Parity with the legacy lists' un-named catch-all entries for the abstract base.
        embedded.XmlElements.Add(new XmlElementAttribute(typeof(Command)));
        arrayItems.XmlArrayItems.Add(new XmlArrayItemAttribute(typeof(Command)));

        XmlAttributeOverrides overrides = new();
        overrides.Add(typeof(Command), nameof(Command.EmbeddedCommands), embedded);
        overrides.Add(typeof(SerializedCommands), nameof(SerializedCommands.commandArray), arrayItems);
        return overrides;
    }

    /// <summary>
    /// Debug-build backstop (#204): asserts every concrete <see cref="Command"/> subclass in the
    /// product assembly has exactly one registry entry. Runs once per invoker creation; cheap, and
    /// it catches a locally added-but-unregistered command the moment the app starts instead of at
    /// test time. The REAL gate is <c>CommandRegistryTests</c>, which fails the build for the same
    /// drift (both directions) on every CI run.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void DebugAssertComplete() {
        List<Type> concrete = [.. typeof(Command).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Command)))];
        List<string> missing = [.. concrete
            .Where(t => Entries.All(e => e.CommandType != t))
            .Select(t => t.Name)];
        List<string> duplicates = [.. Entries
            .GroupBy(e => e.CommandType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Name)];
        Debug.Assert(missing.Count == 0 && duplicates.Count == 0,
            $"CommandRegistry (#204) is out of sync. Missing: [{string.Join(", ", missing)}]; " +
            $"duplicated: [{string.Join(", ", duplicates)}]. Every concrete Command subclass needs " +
            "exactly ONE CommandRegistry.Entries line; it drives serialization, the invoker's " +
            "built-ins, and the hygiene tests (CommandRegistryTests).");
    }
}
