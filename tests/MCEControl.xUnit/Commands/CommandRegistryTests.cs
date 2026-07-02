// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// <para><b>THE COMMAND HYGIENE SUITE; index.</b> Four test classes together turn four kinds of
/// silent drift into red builds whenever a command type is added or changed:</para>
/// <list type="bullet">
/// <item><b><c>CommandRegistryTests</c></b> (this file, #204): every concrete <see cref="Command"/>
/// subclass appears exactly once in <see cref="CommandRegistry.Entries"/> (and vice versa), wire
/// names are all-lowercase and unique, every entry's built-ins factory yields usable disabled-by-
/// default prototypes, and the registry-driven serializer still writes/reads the established
/// .commands wire format. Closes: "a new command compiles but silently can't be serialized and
/// registers no built-ins" (the old three-hidden-conventions trap).</item>
/// <item><b><c>XmlNameCasingTests</c></b> (#200): every serialized XML name reachable from the
/// command types is all-lowercase. Closes: "a property saves fine but is silently dropped on load
/// by the lower-casing XSLT".</item>
/// <item><b><c>CommandClonePropertyRoundTripTests</c></b> (#207): <c>Clone(reply)</c> round-trips
/// every public settable property of every command. Closes: "a property works from mcec.commands
/// prototypes but arrives 0/null at execution".</item>
/// <item><b><c>AgentCommandStructuralGateTests</c></b> (#208): every agent tool name maps to a
/// command deriving from the gated <c>AgentCommand</c> base, whose sealed <c>Execute()</c> enforces
/// <c>AgentCommandsEnabled</c>. Closes: "a new agent command skips the operator opt-in gate on the
/// TCP/serial transports".</item>
/// </list>
/// <para>To add a command type: derive from <see cref="Command"/> (or the gated agent bases), give
/// it a <c>public static List&lt;Command&gt; BuiltInCommands</c>, and add ONE line to
/// <see cref="CommandRegistry.Entries"/>; this suite verifies everything else.</para>
/// </summary>
public class CommandRegistryTests {
    private static List<Type> ConcreteCommandTypes => [.. typeof(Command).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)))
        .OrderBy(t => t.FullName, StringComparer.Ordinal)];

    [Fact]
    public void Sweep_FindsTheCommandTypes() {
        // Guard the reflection sweep itself: if it ever came back (near-)empty, the completeness
        // assertions below would vacuously pass.
        List<Type> types = ConcreteCommandTypes;
        Assert.True(types.Count >= 18, $"expected the sweep to find all command types, got {types.Count}");
        Assert.Contains(typeof(CharsCommand), types);
        Assert.Contains(typeof(RecordCommand), types);
    }

    [Fact]
    public void EveryConcreteCommandSubclass_IsRegisteredExactlyOnce() {
        List<Type> concrete = ConcreteCommandTypes;

        List<string> missing = [.. concrete
            .Where(t => !CommandRegistry.Entries.Any(e => e.CommandType == t))
            .Select(t => t.Name)];
        List<string> duplicated = [.. CommandRegistry.Entries
            .GroupBy(e => e.CommandType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Name)];
        // The reverse direction: an entry whose type is not a concrete Command subclass (abstract,
        // deleted, or foreign) would explode at serialize/built-ins time.
        List<string> bogus = [.. CommandRegistry.Entries
            .Where(e => !concrete.Contains(e.CommandType))
            .Select(e => $"{e.XmlName} ({e.CommandType.Name})")];

        Assert.True(missing.Count == 0,
            $"Concrete Command subclass(es) missing from CommandRegistry.Entries: {string.Join(", ", missing)}. " +
            "Add ONE registry line per command type (#204); without it the command cannot be serialized " +
            "and registers no built-ins.");
        Assert.True(duplicated.Count == 0,
            $"Command type(s) registered more than once in CommandRegistry.Entries: {string.Join(", ", duplicated)}.");
        Assert.True(bogus.Count == 0,
            $"CommandRegistry entries that are not concrete Command subclasses: {string.Join(", ", bogus)}.");
    }

    [Fact]
    public void RegistryXmlNames_AreLowercaseAndUnique() {
        // Loading pipes .commands files through a lower-casing XSLT (#200): an uppercase wire name
        // could be written but would never bind on load. Uniqueness: two entries sharing a name
        // would make deserialization of that element ambiguous.
        List<string> notLowercase = [.. CommandRegistry.Entries
            .Where(e => e.XmlName.Any(char.IsUpper) || string.IsNullOrWhiteSpace(e.XmlName))
            .Select(e => e.XmlName)];
        List<string> duplicated = [.. CommandRegistry.Entries
            .GroupBy(e => e.XmlName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        Assert.True(notLowercase.Count == 0,
            $"Registry XmlName(s) not all-lowercase (or blank): {string.Join(", ", notLowercase)}; " +
            "the .commands lower-casing XSLT means such an element is written but silently never read back (#200).");
        Assert.True(duplicated.Count == 0,
            $"Registry XmlName(s) used by more than one entry: {string.Join(", ", duplicated)}.");
    }

    [Fact]
    public void RegistryBuiltInsFactories_YieldDisabledNamedPrototypes() {
        foreach (CommandRegistryEntry entry in CommandRegistry.Entries) {
            List<Command> builtIns = [.. entry.BuiltIns()];
            Assert.True(builtIns.Count > 0,
                $"Registry entry '{entry.XmlName}' produced no built-in prototypes; point BuiltIns at the type's BuiltInCommands.");
            foreach (Command builtIn in builtIns) {
                Assert.False(string.IsNullOrWhiteSpace(builtIn.Cmd),
                    $"A built-in from registry entry '{entry.XmlName}' has a blank Cmd; the invoker would reject it.");
                // SECURITY: built-ins ship disabled; the operator opts in per command.
                Assert.False(builtIn.Enabled,
                    $"Built-in '{builtIn.Cmd}' (registry entry '{entry.XmlName}') is Enabled by default; built-ins must ship disabled.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Wire format (#204): moving the [XmlElement]/[XmlArrayItem] maps into registry-built
    // XmlAttributeOverrides must not change what .commands files look like.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Save_WritesEveryRegisteredType_UnderItsRegistryName() {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        try {
            SerializedCommands sc = new() {
                commandArray = [.. CommandRegistry.Entries.Select(e => NewInstance(e, "hygiene_"))],
            };
            SerializedCommands.SaveCommands(tempFile, sc, "1.0.0.0");

            XDocument doc = XDocument.Load(tempFile);
            XElement commands = doc.Descendants().Single(el => el.Name.LocalName == "commands");
            Assert.Equal("http://www.kindel.com/products/mcecontroller", commands.Name.NamespaceName);
            Assert.Equal(
                CommandRegistry.Entries.Select(e => e.XmlName),
                commands.Elements().Select(el => el.Name.LocalName));
        }
        finally {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SaveLoad_RoundTrips_OneOfEveryRegisteredType_TopLevelAndEmbedded() {
        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        try {
            // Top level: one of each registered type. Embedded: a parent carrying one of each, to
            // exercise the Command.EmbeddedCommands override map (distinct from commandArray's).
            StartProcessCommand parent = new() {
                Cmd = "hygiene_parent",
                File = "x.exe",
                EmbeddedCommands = [.. CommandRegistry.Entries.Select(e => NewInstance(e, "embedded_"))],
            };
            SerializedCommands original = new() {
                commandArray = [.. CommandRegistry.Entries.Select(e => NewInstance(e, "hygiene_")), parent],
            };

            SerializedCommands.SaveCommands(tempFile, original, "1.0.0.0");
            SerializedCommands loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

            Assert.NotNull(loaded);
            Assert.Equal(CommandRegistry.Entries.Count + 1, loaded.commandArray.Length);
            foreach (CommandRegistryEntry entry in CommandRegistry.Entries) {
                Command topLevel = Assert.Single(loaded.commandArray, c => c.Cmd == $"hygiene_{entry.XmlName}");
                Assert.IsType(entry.CommandType, topLevel);
            }

            StartProcessCommand? loadedParent = loaded.commandArray.Single(c => c.Cmd == "hygiene_parent") as StartProcessCommand;
            Assert.NotNull(loadedParent);
            Assert.NotNull(loadedParent.EmbeddedCommands);
            Assert.Equal(CommandRegistry.Entries.Count, loadedParent.EmbeddedCommands.Count);
            foreach (CommandRegistryEntry entry in CommandRegistry.Entries) {
                Command embedded = Assert.Single(loadedParent.EmbeddedCommands, c => c.Cmd == $"embedded_{entry.XmlName}");
                Assert.IsType(entry.CommandType, embedded);
            }
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// The established v3.0 wire names, FROZEN. Installed .commands files contain these element
    /// names; whatever refactoring happens to how the serializer is built, these must keep loading
    /// as these types forever. New commands ADD names (add them to the registry, not here; this
    /// list deliberately does not chase the registry); renaming or unregistering any of these
    /// breaks users and fails here.
    /// </summary>
    private static readonly (string XmlName, Type CommandType)[] FrozenV3WireNames = [
        ("chars", typeof(CharsCommand)),
        ("startprocess", typeof(StartProcessCommand)),
        ("sendinput", typeof(SendInputCommand)),
        ("sendmessage", typeof(SendMessageCommand)),
        ("setforegroundwindow", typeof(SetForegroundWindowCommand)),
        ("shutdown", typeof(ShutdownCommand)),
        ("pause", typeof(PauseCommand)),
        ("mouse", typeof(MouseCommand)),
        ("mceccommand", typeof(McecCommand)),
        ("capture", typeof(CaptureCommand)),
        ("query", typeof(QueryCommand)),
        ("find", typeof(FindCommand)),
        ("invoke", typeof(InvokeCommand)),
        ("drag", typeof(DragCommand)),
        ("launch", typeof(LaunchCommand)),
        ("click", typeof(ClickCommand)),
        ("displays", typeof(DisplaysCommand)),
        ("record", typeof(RecordCommand)),
    ];

    [Fact]
    public void LoadCommands_CurrentFormatFixture_BindsEveryFrozenWireName() {
        // A hand-frozen current-format file (NOT generated from the registry, so a registry-side
        // rename cannot silently rewrite the expectation): every established element name must
        // still deserialize to its established type.
        string fixture =
            "<?xml version=\"1.0\"?>\r\n" +
            "<mcecontroller version=\"1.0.0.0\">\r\n" +
            "  <commands xmlns=\"http://www.kindel.com/products/mcecontroller\">\r\n" +
            string.Concat(FrozenV3WireNames.Select(f => $"    <{f.XmlName} cmd=\"fix_{f.XmlName}\" enabled=\"true\" />\r\n")) +
            "  </commands>\r\n" +
            "</mcecontroller>\r\n";

        string tempFile = Path.GetTempFileName();
        try {
            File.WriteAllText(tempFile, fixture);
            SerializedCommands loaded = SerializedCommands.LoadCommands(tempFile, "1.0.0.0");

            Assert.NotNull(loaded);
            Assert.Equal(FrozenV3WireNames.Length, loaded.commandArray.Length);
            foreach ((string xmlName, Type commandType) in FrozenV3WireNames) {
                Command cmd = Assert.Single(loaded.commandArray, c => c.Cmd == $"fix_{xmlName}");
                Assert.IsType(commandType, cmd);
                Assert.True(cmd.Enabled);
            }
        }
        finally {
            File.Delete(tempFile);
        }
    }

    private static Command NewInstance(CommandRegistryEntry entry, string cmdPrefix) {
        Command command = (Command)Activator.CreateInstance(entry.CommandType)!;
        command.Cmd = cmdPrefix + entry.XmlName;
        return command;
    }
}
