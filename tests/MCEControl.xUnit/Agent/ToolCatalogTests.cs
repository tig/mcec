// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Registry completeness and equivalence pins for the #205 <see cref="ToolCatalog"/>. The catalog
/// replaced ~8 hand-synced switches/lists; these tests pin the tool NAMES and the migrated policy
/// sets so an accidental drop (or a silent flag flip) fails loudly instead of drifting.
/// </summary>
[Collection("AgentSerial")]
public class ToolCatalogTests {
    /// <summary>The thirteen gated agent tools, in the order tools/list advertises them. Pinned by name.</summary>
    private static readonly string[] _catalogNames = [
        "capture", "query", "displays", "windows", "window", "find", "wait-for", "invoke", "drag", "click", "focus", "clipboard", "record", "launch",
    ];

    /// <summary>The meta-tools that are deliberately NOT in the catalog (no 1:1 Command mapping), in tools/list order.</summary>
    private static readonly string[] _metaToolNames = [
        "send_command", "session-start", "session-status", "session-end", "request-command-access", "provision-session", "end-session",
    ];

    [Fact]
    public void Catalog_ContainsExactlyTheGatedAgentTools_InToolsListOrder() {
        Assert.Equal(_catalogNames, ToolCatalog.All.Select(d => d.Name));
    }

    [Fact]
    public void EveryDescriptor_IsComplete_SchemaFactoryInstanceAndTersifier() {
        foreach (ToolDescriptor d in ToolCatalog.All) {
            // Schema: a complete tools/list entry whose name matches the descriptor.
            JsonObject schema = d.BuildSchema();
            Assert.Equal(d.Name, schema["name"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(schema["description"]!.GetValue<string>()));
            JsonObject inputSchema = schema["inputSchema"]!.AsObject();
            Assert.Equal("object", inputSchema["type"]!.GetValue<string>());
            Assert.NotNull(inputSchema["properties"]);
            Assert.NotNull(inputSchema["required"]);

            // Factories: both the tool-call mapping and the provisioning instance produce a command.
            Assert.NotNull(d.BuildCommand([]));
            Assert.NotNull(d.CreateCommandInstance());

            // Tersifier: every tool has a REAL formatter. Only the argument-less `displays` may render
            // as its bare name; a bare name for any other tool is exactly the record/launch drift
            // this registry fixed (#205).
            string label = d.Tersify([]);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.StartsWith(d.Name, label, StringComparison.Ordinal);
            if (d.Name != "displays") {
                Assert.NotEqual(d.Name, label);
            }
        }
    }

    [Fact]
    public void ToolsList_IsExactlyTheCatalogPlusTheMetaTools() {
        JsonObject resp = AgentServer.Dispatch(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/list",
        })!;

        List<string> names = [];
        foreach (JsonNode? tool in resp["result"]!.AsObject()["tools"]!.AsArray()) {
            names.Add(tool!["name"]!.GetValue<string>());
        }

        // Exact, ordered pin: dropping a tool from the catalog (or a meta-tool from BuildToolsList)
        // must fail this test, not silently shrink the advertised surface.
        Assert.Equal([.. _catalogNames, .. _metaToolNames], names);
    }

    [Fact]
    public void GateMembership_IsCatalogMembership() {
        foreach (string name in _catalogNames) {
            Assert.True(ToolCatalog.Contains(name), $"'{name}' must be in the catalog (tools/call gate).");
        }
        // Meta-tools are dispatched by their own special cases BEFORE the catalog gate; they must not
        // also be catalog members, or they'd be double-dispatched as agent commands.
        foreach (string name in _metaToolNames) {
            Assert.False(ToolCatalog.Contains(name), $"meta-tool '{name}' must NOT be in the catalog.");
        }
        Assert.False(ToolCatalog.Contains("hover"));
        // The historical gate pattern was case-sensitive; the catalog lookup must stay ordinal.
        Assert.False(ToolCatalog.Contains("Capture"));
    }

    [Fact]
    public void IsObservation_ExactlyQueryCaptureFindWaitFor() {
        string[] observation = ["query", "capture", "find", "wait-for"];
        foreach (ToolDescriptor d in ToolCatalog.All) {
            Assert.Equal(observation.Contains(d.Name), d.IsObservation);
        }
    }

    [Fact]
    public void SerializesOnInput_ExactlyDragAndFocus_InTheCatalog() {
        // drag and focus both synthesize a real click/gesture that must not interleave with queue-driven
        // input (#113); every other catalog tool runs unlocked.
        string[] serializing = ["drag", "focus"];
        foreach (ToolDescriptor d in ToolCatalog.All) {
            Assert.Equal(serializing.Contains(d.Name), d.SerializesOnInput);
        }
        // send_command is a meta-tool special case in AgentServer (it serializes indirectly via the
        // dispatcher thread, #195); the public predicate must still report it.
        Assert.True(AgentServer.SerializesOnInputLock("send_command"));
    }

    [Fact]
    public void ProvisionedByDefault_EverythingExceptLaunch_AndDefaultCommandsMatch() {
        foreach (ToolDescriptor d in ToolCatalog.All) {
            Assert.Equal(d.Name != "launch", d.ProvisionedByDefault);
        }
        // SessionProvisioner.DefaultCommands is derived from the catalog; pin the exact historical set.
        Assert.Equal(
            ["capture", "query", "displays", "windows", "window", "find", "wait-for", "invoke", "drag", "click", "focus", "clipboard", "record"],
            SessionProvisioner.DefaultCommands);
    }

    [Fact]
    public void BuildCommand_EveryTool_ProducesItsCommandType() {
        Dictionary<string, Type> expected = new() {
            ["capture"] = typeof(CaptureCommand),
            ["query"] = typeof(QueryCommand),
            ["displays"] = typeof(DisplaysCommand),
            ["windows"] = typeof(WindowsCommand),
            ["window"] = typeof(WindowCommand),
            ["find"] = typeof(FindCommand),
            ["wait-for"] = typeof(FindCommand),
            ["invoke"] = typeof(InvokeCommand),
            ["drag"] = typeof(DragCommand),
            ["click"] = typeof(ClickCommand),
            ["focus"] = typeof(FocusCommand),
            ["clipboard"] = typeof(ClipboardCommand),
            ["record"] = typeof(RecordCommand),
            ["launch"] = typeof(LaunchCommand),
        };
        foreach (ToolDescriptor d in ToolCatalog.All) {
            Assert.Equal(expected[d.Name], d.BuildCommand([]).GetType());
            Assert.Equal(expected[d.Name], d.CreateCommandInstance().GetType());
        }
    }
}
