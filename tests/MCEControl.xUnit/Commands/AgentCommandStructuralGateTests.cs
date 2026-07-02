// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Structural enforcement for issue #208: the <see cref="AgentRuntime.AgentCommandsEnabled"/> gate
/// lives in <see cref="AgentCommand"/>'s sealed <c>Execute()</c> template, because over the legacy
/// TCP/serial pipeline the in-command check is the ONLY gate (the server-side check covers only the
/// MCP/HTTP path). These tests make "someone adds an agent tool whose command skips the gate" a test
/// failure instead of a silent security hole.
/// </summary>
public class AgentCommandStructuralGateTests {
    /// <summary>
    /// Every tool name in AgentServer's tools/call gate; which since #205 is exactly the
    /// <see cref="ToolCatalog"/> membership. Kept as an INDEPENDENT test-side pin (not read from the
    /// catalog) so removing or renaming a catalog entry can't silently shrink this enforcement;
    /// <c>ToolCatalogTests</c> pins the same list against the catalog itself.
    /// </summary>
    private static readonly string[] GatedToolNames = [
        "capture", "query", "displays", "find", "wait-for", "invoke", "record", "launch", "drag", "click",
    ];

    public static TheoryData<string> GatedToolNameData {
        get {
            TheoryData<string> data = [];
            foreach (string name in GatedToolNames) {
                data.Add(name);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(GatedToolNameData))]
    public void EveryGatedToolName_MapsToAnAgentCommand(string name) {
        // BuildCommand is AgentServer's own name -> command mapping (#201: exhaustive, null for
        // unknown). Whatever command a gated tool name produces MUST derive from AgentCommand so the
        // sealed Execute() gate applies on transports that have no server-side gate (TCP/serial).
        Command? cmd = AgentServer.BuildCommand(name, []);

        Assert.NotNull(cmd);
        Assert.IsAssignableFrom<AgentCommand>(cmd);
    }

    [Fact]
    public void AgentCommand_Execute_IsSealed_SoSubclassesCannotBypassTheGate() {
        MethodInfo execute = typeof(AgentCommand).GetMethod(nameof(Command.Execute))!;

        // Declared on AgentCommand (the template) and final; a subclass cannot re-open it and skip
        // the AgentCommandsEnabled check.
        Assert.Equal(typeof(AgentCommand), execute.DeclaringType);
        Assert.True(execute.IsFinal, "AgentCommand.Execute() must stay sealed: it is the structural AgentCommandsEnabled gate.");
    }

    [Fact]
    public void EveryConcreteAgentCommandType_IsCoveredByTheGatedToolList() {
        // The inverse guard: a NEW AgentCommand subclass must be represented in the gated tool list
        // (i.e. producible by BuildCommand under some gated name), otherwise the enforcement above
        // can't see it. FindCommand covers both "find" and "wait-for".
        HashSet<Type> mappedTypes = [];
        foreach (string name in GatedToolNames) {
            Command? cmd = AgentServer.BuildCommand(name, []);
            if (cmd is not null) {
                mappedTypes.Add(cmd.GetType());
            }
        }

        foreach (Type type in typeof(Command).Assembly.GetTypes()) {
            if (type.IsClass && !type.IsAbstract && typeof(AgentCommand).IsAssignableFrom(type)) {
                Assert.True(mappedTypes.Contains(type),
                    $"{type.Name} derives from AgentCommand but no gated tool name maps to it; " +
                    "add its tool name to AgentServer's gate whitelist, BuildCommand, and GatedToolNames here.");
            }
        }
    }

    [Fact]
    public void WindowTargetingClone_CopiesTheSharedSelectors() {
        // The five window selectors moved to WindowTargetingAgentCommand (#208); since #207 the
        // MemberwiseClone-based Command.Clone copies them (no per-layer copying anywhere). Pin that
        // they survive Clone so a future clone refactor can't drop them.
        QueryCommand original = new() {
            Cmd = "query",
            Window = "Notepad",
            Handle = 0x1234,
            Process = "notepad",
            ClassName = "Notepad",
            Foreground = true,
        };

        QueryCommand clone = (QueryCommand)original.Clone(null!);

        Assert.Equal("Notepad", clone.Window);
        Assert.Equal(0x1234L, clone.Handle);
        Assert.Equal("notepad", clone.Process);
        Assert.Equal("Notepad", clone.ClassName);
        Assert.True(clone.Foreground);
    }
}
