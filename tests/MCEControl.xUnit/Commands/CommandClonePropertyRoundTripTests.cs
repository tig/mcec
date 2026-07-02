// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Clone-drift hygiene (#207): for EVERY concrete <see cref="Command"/> subclass, Clone(reply) must
/// round-trip EVERY public settable property. Under the old hand-copied field lists, one forgotten
/// line meant a property that worked from mcec.commands prototypes but arrived 0/null at execution,
/// silently. The base Clone is now MemberwiseClone-based so this can't happen — this test pins that
/// guarantee against any future re-introduction of per-property copying (a new command, a new
/// property, or a subclass override that forgets base state all fail here immediately).
/// </summary>
public class CommandClonePropertyRoundTripTests {
    // Properties Clone deliberately does NOT round-trip:
    // - Reply: Clone's contract is to REPLACE it with the fresh per-connection reply context (the
    //   whole reason Enqueue clones). Asserted separately below.
    // - EmbeddedCommands: reference-typed; Clone deep-clones it rather than copying the reference.
    //   Covered by CommandTests.Clone_WithEmbeddedCommands_ClonesEmbedded / _AreDeepCopies /
    //   _KeepTheirOwnEnabled (#183).
    private static readonly HashSet<string> SkippedProperties = [
        nameof(Command.Reply),
        nameof(Command.EmbeddedCommands),
    ];

    public static TheoryData<Type> ConcreteCommandTypes {
        get {
            TheoryData<Type> data = [];
            foreach (Type type in typeof(Command).Assembly.GetTypes()
                         .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)))
                         .OrderBy(t => t.FullName, StringComparer.Ordinal)) {
                data.Add(type);
            }
            return data;
        }
    }

    [Fact]
    public void Sweep_FindsTheCommandTypes() {
        // Guard the reflection sweep itself: if it ever comes back (near-)empty, every theory case
        // below would vacuously "pass" by not existing.
        List<Type> types = [.. ConcreteCommandTypes.Cast<object[]>().Select(row => (Type)row[0])];
        Assert.True(types.Count >= 18, $"expected the sweep to find all command types, got {types.Count}");
        Assert.Contains(typeof(RecordCommand), types);
        Assert.Contains(typeof(SendInputCommand), types);
    }

    [Theory]
    [MemberData(nameof(ConcreteCommandTypes))]
    public void Clone_RoundTripsEveryPublicSettableProperty(Type commandType) {
        Command original = (Command)Activator.CreateInstance(commandType)!;

        List<PropertyInfo> properties = [.. commandType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod is { IsPublic: true } && !SkippedProperties.Contains(p.Name))];
        Assert.NotEmpty(properties); // every Command at least has Cmd/Args/Enabled/UserDefined

        // Drive every property to a non-default probe value. Comparing original-vs-clone (rather
        // than probe-vs-clone) keeps aliased properties honest (e.g. SetForegroundWindowCommand's
        // ClassName is an alias for AppName).
        int salt = 0;
        foreach (PropertyInfo p in properties) {
            p.SetValue(original, MakeProbeValue(p, original, salt++));
        }

        TestReply reply = new();
        Command clone = original.Clone(reply);

        Assert.NotSame(original, clone);
        Assert.IsType(commandType, clone);
        Assert.Same(reply, clone.Reply); // Clone must install the fresh reply context

        foreach (PropertyInfo p in properties) {
            object? expected = p.GetValue(original);
            object? actual = p.GetValue(clone);
            Assert.True(Equals(expected, actual),
                $"{commandType.Name}.{p.Name} did not survive Clone: expected '{expected}', clone has '{actual}'. " +
                "Clone must copy every public settable property (#207).");
        }
    }

    /// <summary>
    /// A deterministic, guaranteed non-default probe for the property. Fails loudly on a property
    /// type it does not know so a future reference-typed/mutable property must be added here — and
    /// its deep-copy semantics considered in Command.Clone — consciously.
    /// </summary>
    private static object MakeProbeValue(PropertyInfo p, Command instance, int salt) {
        object? current = p.GetValue(instance);
        if (p.PropertyType == typeof(string)) {
            return $"probe-{p.Name}-{salt}";
        }
        if (p.PropertyType == typeof(bool)) {
            return !(bool)current!; // invert so a non-false default still changes
        }
        if (p.PropertyType == typeof(int)) {
            return (int)current! + 7001 + salt; // offset clears defaults like Count=1, MaxNodes=1000
        }
        if (p.PropertyType == typeof(long)) {
            return (long)current! + 700001L + salt;
        }
        Assert.Fail(
            $"{p.DeclaringType!.Name}.{p.Name} has unhandled type {p.PropertyType.Name}. " +
            "Add a probe here AND verify Command.Clone handles it (value/string types are copied by " +
            "MemberwiseClone; a reference-typed mutable property needs explicit deep-copy in Clone).");
        return null!; // unreachable
    }
}
