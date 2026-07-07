// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests the <c>request-command-access</c> consent flow (#307) at the MCP dispatch boundary: argument
/// validation, the prompter seam (grant/deny/timeout/unavailable), in-memory-only grants, the sticky
/// deny, the standing allow-any grant, the consent-pending actuation freeze (with the input gate held),
/// the transport gate, the bootstrap exclusion, and the honest <c>command-disabled</c> refusal on the
/// <c>send_command</c> path. Uses a real (never-dispatching) <see cref="CommandInvoker"/> with the
/// built-in table (every command disabled by default) and a test prompter instead of the dialog.
/// </summary>
[Collection("AgentSerial")]
public class RequestCommandAccessTests : IDisposable {
    public RequestCommandAccessTests() {
        // A real command table (built-ins, all disabled) that can never start a dispatcher thread.
        // The standard test pattern: a temp file name whose parent directory exists (LoadCommands
        // writes a default file there); a nonexistent directory wedges the loader.
        string tempCommandsFile = Path.GetTempFileName();
        File.Delete(tempCommandsFile);
        CommandInvoker invoker = CommandInvoker.Create(tempCommandsFile, "9.9.9", disableInternalCommands: false);
        invoker.SuppressDispatcherForTests = true;
        AgentRuntime.Invoker = invoker;
        AgentConsent.ResetForTests();
        AgentConsent.Prompter = null;
    }

    public void Dispose() {
        AgentConsent.ResetForTests();
        AgentConsent.Prompter = null;
        AgentRuntime.Invoker = null;
        AgentRuntime.Settings = null;
        AgentRuntime.ResetSession();
    }

    private static JsonObject Request(int id, string method, JsonObject? prms = null) => new() {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["method"] = method,
        ["params"] = prms ?? [],
    };

    private static JsonObject Call(int id, string tool, JsonObject args, AgentTransport transport = AgentTransport.Stdio) =>
        AgentServer.Dispatch(Request(id, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = args }), transport)!;

    private static JsonObject Envelope(JsonObject resp) =>
        JsonNode.Parse(FirstTextBlock(resp["result"]!.AsObject()))!.AsObject();

    private static string FirstTextBlock(JsonObject toolResult) {
        foreach (JsonNode? block in toolResult["content"]!.AsArray()) {
            if (block?["type"]?.GetValue<string>() == "text") {
                return block["text"]!.GetValue<string>();
            }
        }
        Assert.Fail("no text content block in tool result");
        return "";
    }

    private static JsonObject Ask(int id, string reason = "test reason", params string[] commands) {
        JsonArray names = [.. commands];
        return Envelope(Call(id, "request-command-access", new JsonObject { ["commands"] = names, ["reason"] = reason }));
    }

    private static string ErrorCode(JsonObject env) => env["error"]!.AsObject()["code"]!.GetValue<string>();

    private static List<string> Strings(JsonObject env, string key) =>
        [.. env["result"]!.AsObject()[key]!.AsArray().Select(n => n!.GetValue<string>())];

    // ---------------------------------------------------------------------------------------------
    // Argument validation (nothing reaches the operator on a malformed request)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MissingCommands_IsBadArguments() {
        JsonObject env = Envelope(Call(1, "request-command-access", new JsonObject { ["reason"] = "r" }));
        Assert.Equal("bad-arguments", ErrorCode(env));
    }

    [Fact]
    public void MissingReason_IsBadArguments() {
        JsonObject env = Envelope(Call(2, "request-command-access", new JsonObject { ["commands"] = new JsonArray("chars:") }));
        Assert.Equal("bad-arguments", ErrorCode(env));
    }

    [Fact]
    public void TooManyCommands_IsBadArguments() {
        string[] names = [.. Enumerable.Range(0, AgentConsent.MaxCommandsPerRequest + 1).Select(i => $"cmd{i}")];
        Assert.Equal("bad-arguments", ErrorCode(Ask(3, "r", names)));
    }

    [Fact]
    public void UnknownCommand_RefusesWholeRequest_WithoutPrompting() {
        int prompts = 0;
        AgentConsent.Prompter = _ => { prompts++; return CommandAccessDecision.AllowRequested; };
        JsonObject env = Ask(4, "r", "chars:", "definitely-not-a-command");
        Assert.Equal("unknown-command", ErrorCode(env));
        Assert.Equal("invalid-argument", env["error"]!.AsObject()["category"]!.GetValue<string>());
        Assert.Contains("definitely-not-a-command", env["error"]!.AsObject()["detail"]!.GetValue<string>());
        Assert.Equal(0, prompts);
    }

    // ---------------------------------------------------------------------------------------------
    // Decisions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Granted_EnablesInMemory_AndPrefixSpellingResolves() {
        CommandAccessRequest? seen = null;
        AgentConsent.Prompter = req => { seen = req; return CommandAccessDecision.AllowRequested; };

        // The 'chars:hello' spelling resolves to the table's 'chars:' entry, like Enqueue parses it.
        JsonObject env = Ask(10, "need to type a path", "chars:hello");
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.Equal(["chars:"], Strings(env, "granted"));
        Assert.Equal("requested", env["result"]!.AsObject()["scope"]!.GetValue<string>());
        Assert.True((AgentRuntime.Invoker!["chars:"] as Command)!.Enabled);
        Assert.Equal(["chars:"], seen!.Commands);

        // Second ask is a no-op success; nothing to grant, no prompt.
        AgentConsent.Prompter = _ => throw new InvalidOperationException("must not prompt again");
        JsonObject again = Ask(11, "still typing", "chars:");
        Assert.True(again["ok"]!.GetValue<bool>());
        Assert.Empty(Strings(again, "granted"));
        Assert.Equal(["chars:"], Strings(again, "alreadyEnabled"));
    }

    [Fact]
    public void PrefixSpelledCommand_GrantsTheKeyEnqueueWillExecute() {
        // PR #308 review (Codex): the table holds BOTH a blank prefix entry ('mcec:') and full
        // spellings ('mcec:exit'), but CommandInvoker.Enqueue parses any 'prefix:args' string as the
        // bare prefix FIRST, so the prefix entry is what a send_command will actually gate on.
        // A grant for 'mcec:exit' must therefore enable 'mcec:', not the (never-executed) full entry;
        // otherwise the operator approves and the very next send_command still fails command-disabled.
        AgentConsent.Prompter = _ => CommandAccessDecision.AllowRequested;

        JsonObject env = Ask(110, "need to exit the instance", "mcec:exit");

        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.Equal(["mcec:"], Strings(env, "granted"));
        Assert.True((AgentRuntime.Invoker!["mcec:"] as Command)!.Enabled);
        Assert.False((AgentRuntime.Invoker!["mcec:exit"] as Command)!.Enabled);
    }

    [Fact]
    public void Denied_IsSticky_NoSecondPrompt() {
        int prompts = 0;
        AgentConsent.Prompter = _ => { prompts++; return CommandAccessDecision.Denied; };

        Assert.Equal("consent-denied", ErrorCode(Ask(20, "r", "launch")));
        Assert.Equal("consent-denied", ErrorCode(Ask(21, "r", "launch")));
        Assert.Equal(1, prompts);
        Assert.False((AgentRuntime.Invoker!["launch"] as Command)!.Enabled);
    }

    [Fact]
    public void Timeout_IsNotSticky_AllowsAskingAgain() {
        int prompts = 0;
        AgentConsent.Prompter = _ => { prompts++; return CommandAccessDecision.TimedOut; };

        Assert.Equal("consent-timeout", ErrorCode(Ask(30, "r", "launch")));
        Assert.Equal("consent-timeout", ErrorCode(Ask(31, "r", "launch")));
        Assert.Equal(2, prompts);
    }

    [Fact]
    public void AllowAny_AutoApprovesLaterRequests_WithoutPrompting() {
        int prompts = 0;
        AgentConsent.Prompter = _ => { prompts++; return CommandAccessDecision.AllowAny; };

        JsonObject first = Ask(40, "r", "launch");
        Assert.True(first["ok"]!.GetValue<bool>());
        Assert.Equal("any", first["result"]!.AsObject()["scope"]!.GetValue<string>());

        JsonObject second = Ask(41, "r", "chars:");
        Assert.True(second["ok"]!.GetValue<bool>());
        Assert.Equal("any", second["result"]!.AsObject()["scope"]!.GetValue<string>());
        Assert.True((AgentRuntime.Invoker!["chars:"] as Command)!.Enabled);
        Assert.Equal(1, prompts); // the standing grant answered the second ask
    }

    [Fact]
    public void NoPrompter_FailsClosed_ConsentUnavailable() {
        AgentConsent.Prompter = null;
        Assert.Equal("consent-unavailable", ErrorCode(Ask(50, "r", "launch")));
        Assert.False((AgentRuntime.Invoker!["launch"] as Command)!.Enabled);
    }

    [Fact]
    public void PrompterReturningNull_FailsClosed_ConsentUnavailable() {
        AgentConsent.Prompter = _ => null;
        Assert.Equal("consent-unavailable", ErrorCode(Ask(51, "r", "launch")));
        Assert.False((AgentRuntime.Invoker!["launch"] as Command)!.Enabled);
    }

    [Fact]
    public void Reason_IsSanitizedBeforeDisplay() {
        CommandAccessRequest? seen = null;
        AgentConsent.Prompter = req => { seen = req; return CommandAccessDecision.Denied; };
        _ = Ask(60, "line one\r\nline \"two\"\tend", "launch");
        Assert.Equal("line one line 'two' end", seen!.Reason);
    }

    // ---------------------------------------------------------------------------------------------
    // The consent freeze: while the prompt is up, the agent may look but not touch, and the input
    // gate is held so nothing synthesized can land on the dialog.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WhilePromptPending_ActuationIsRefused_AndInputGateIsHeld() {
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true };
        string? clickCode = null;
        bool gateWasHeld = false;
        AgentConsent.Prompter = _ => {
            Assert.True(AgentConsent.IsPending);
            // The gate is held by the calling worker for the prompt's whole lifetime. Probe from a
            // DIFFERENT thread: Monitor is reentrant, so a same-thread TryEnter would succeed.
            Thread probe = new(() => {
                if (Monitor.TryEnter(AgentRuntime.InputGate, 0)) {
                    Monitor.Exit(AgentRuntime.InputGate);
                }
                else {
                    gateWasHeld = true;
                }
            });
            probe.Start();
            probe.Join();
            // An actuation call arriving mid-prompt is refused before any gate/catalog dispatch.
            clickCode = ErrorCode(Envelope(Call(71, "click", new JsonObject {
                ["window"] = "x",
                ["at"] = new JsonObject { ["x"] = 1, ["y"] = 1 },
            })));
            // A second consent ask mid-prompt is refused the same way.
            Assert.Equal("consent-pending", ErrorCode(Ask(72, "r", "chars:")));
            return CommandAccessDecision.Denied;
        };

        _ = Ask(70, "r", "launch");
        Assert.True(gateWasHeld, "InputGate was not held while the prompt was up");
        Assert.Equal("consent-pending", clickCode);
        Assert.False(AgentConsent.IsPending); // released after the decision
    }

    [Fact]
    public void ServedWhilePending_IsAnObservationOnlyAllowList() {
        foreach (string tool in (string[])["capture", "query", "displays", "windows", "find", "wait-for", "record"]) {
            Assert.True(AgentToolExecutor.ServedWhileConsentPending(tool), tool);
        }
        foreach (string tool in (string[])["invoke", "drag", "click", "focus", "clipboard", "launch", "send_command", "request-command-access"]) {
            Assert.False(AgentToolExecutor.ServedWhileConsentPending(tool), tool);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Review findings (#308): grant persistence, the widened consent freeze, e-stop vs sticky deny,
    // reason sanitization depth, strict argument validation, single-char resolution
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Grant_DoesNotPersistThroughCommandsSave() {
        // Finding 1: the dialog promises 'nothing is written to any config file', so a consent grant
        // must not ride the Commands window's Save into mcec.commands. An enable the OPERATOR made
        // by hand still persists; only consent-granted enables serialize as disabled.
        AgentConsent.Prompter = _ => CommandAccessDecision.AllowRequested;
        _ = Ask(120, "need to launch", "launch");
        Assert.True((AgentRuntime.Invoker!["launch"] as Command)!.Enabled);
        (AgentRuntime.Invoker!["mcec:ver"] as Command)!.Enabled = true; // operator-made, not consent

        string path = Path.GetTempFileName();
        try {
            AgentRuntime.Invoker!.Save(path);
            CommandInvoker reloaded = CommandInvoker.Create(path, "9.9.9", disableInternalCommands: false);
            Assert.False((reloaded["launch"] as Command)!.Enabled);
            Assert.True((reloaded["mcec:ver"] as Command)!.Enabled);
            // The live, in-memory grant is untouched by saving.
            Assert.True((AgentRuntime.Invoker!["launch"] as Command)!.Enabled);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void WhilePromptPending_ProvisioningAndSessionLifecycle_AreRefused() {
        // Finding 2: provision-session mid-prompt would mint a SECOND instance whose input is not
        // blocked by this process's protections (input gate, never-a-target, dispatch freeze), so the
        // consent freeze must cover the meta-tools too, not just the catalog.
        string origRoot = SessionProvisioner.SessionsRoot;
        string origBin = SessionProvisioner.BinariesDir;
        string baseTemp = Path.Combine(Path.GetTempPath(), "mcec-consent-freeze-test", Path.GetRandomFileName());
        string fakeBin = Path.Combine(baseTemp, "install");
        MCEControl.xUnit.Helpers.ProvisionTestFixtures.SeedMinimalInstall(fakeBin);
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = true, AllowSessionProvisioning = true };
        SessionProvisioner.SessionsRoot = Path.Combine(baseTemp, "sessions");
        SessionProvisioner.BinariesDir = fakeBin;
        try {
            JsonObject? provision = null;
            JsonObject? sessionStart = null;
            AgentConsent.Prompter = _ => {
                provision = Envelope(Call(130, "provision-session", []));
                sessionStart = Envelope(Call(131, "session-start", []));
                return CommandAccessDecision.Denied;
            };
            _ = Ask(129, "r", "launch");

            Assert.False(provision!["ok"]!.GetValue<bool>());
            Assert.Equal("consent-pending", ErrorCode(provision));
            Assert.False(sessionStart!["ok"]!.GetValue<bool>());
            Assert.Equal("consent-pending", ErrorCode(sessionStart));
        }
        finally {
            SessionProvisioner.SessionsRoot = origRoot;
            SessionProvisioner.BinariesDir = origBin;
            try { if (Directory.Exists(baseTemp)) { Directory.Delete(baseTemp, recursive: true); } }
            catch (IOException) { /* best-effort temp cleanup */ }
            catch (UnauthorizedAccessException) { /* best-effort temp cleanup */ }
        }
    }

    [Fact]
    public void EmergencyStopDuringPrompt_IsEmergencyStopped_NotAStickyDeny() {
        // Finding 3: the panic hotkey halts the session; it does not answer the consent question.
        // The dialog dismisses as TimedOut, and with the latch engaged the agent must get the
        // standard emergency-stopped signal; and the command must NOT be sticky-denied.
        AgentConsent.Prompter = _ => {
            AgentRuntime.SetEmergencyStopped(true); // the hotkey engaging mid-prompt
            return CommandAccessDecision.TimedOut;  // how the dialog now reports that dismissal
        };
        try {
            Assert.Equal("emergency-stopped", ErrorCode(Ask(140, "r", "launch")));
        }
        finally {
            AgentRuntime.SetEmergencyStopped(false);
        }

        AgentConsent.Prompter = _ => CommandAccessDecision.AllowRequested;
        JsonObject env = Ask(141, "r", "launch");
        Assert.True(env["ok"]!.GetValue<bool>()); // re-armed; prompts again and grants
    }

    [Fact]
    public void Reason_NeutralizesBidiAndUnicodeSeparators() {
        // Finding 5: char.IsControl misses Cf (bidi overrides like U+202E), Zl (U+2028), and
        // Zp (U+2029); untrusted text must not reorder or line-break its way out of the quoting.
        // Built from char codes so no invisible/bidi character rides in this source file.
        CommandAccessRequest? seen = null;
        AgentConsent.Prompter = req => { seen = req; return CommandAccessDecision.Denied; };
        string hostile = "ok " + (char)0x202E + "evil" + (char)0x2028 + " " + (char)0x2029 + "stuff end";
        _ = Ask(150, hostile, "launch");
        Assert.DoesNotContain((char)0x202E, seen!.Reason);
        Assert.DoesNotContain((char)0x2028, seen.Reason);
        Assert.DoesNotContain((char)0x2029, seen.Reason);
        Assert.Equal("ok evil stuff end", seen.Reason);
    }

    [Fact]
    public void NonStringCommandEntry_IsBadArguments_NotSilentlyDropped() {
        // Finding 7: a malformed entry must refuse the whole request, not shrink it to a silent
        // subset the operator never saw.
        AgentConsent.Prompter = _ => throw new InvalidOperationException("must not prompt");
        JsonObject env = Envelope(Call(160, "request-command-access", new JsonObject {
            ["commands"] = new JsonArray("launch", 1),
            ["reason"] = "r",
        }));
        Assert.Equal("bad-arguments", ErrorCode(env));
    }

    [Fact]
    public void SingleCharRequest_GrantsTheCharsGate() {
        // Finding 8: Enqueue runs a single character as a chars:-gated keypress, so requesting "a"
        // must grant 'chars:' (the entry the invoker actually gates on), same as the prefix rule.
        AgentConsent.Prompter = _ => CommandAccessDecision.AllowRequested;
        JsonObject env = Ask(170, "type a letter", "a");
        Assert.True(env["ok"]!.GetValue<bool>());
        Assert.Equal(["chars:"], Strings(env, "granted"));
        Assert.True((AgentRuntime.Invoker!["chars:"] as Command)!.Enabled);
    }

    // ---------------------------------------------------------------------------------------------
    // Gates: transport, bootstrap, tools/list
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OverHttp_RequiresAgentCommandsEnabled() {
        AgentRuntime.Settings = new AppSettings { AgentCommandsEnabled = false };
        JsonObject env = Envelope(Call(80, "request-command-access",
            new JsonObject { ["commands"] = new JsonArray("launch"), ["reason"] = "r" }, AgentTransport.Http));
        Assert.Equal("agent-commands-disabled", ErrorCode(env));
    }

    [Fact]
    public void BootstrapOnly_RefusesRequestCommandAccess_AndOmitsItFromToolsList() {
        Program.IsProgramFilesInstallOverrideForTests = true;
        try {
            Assert.Equal("bootstrap-only", ErrorCode(Ask(90, "r", "launch")));
            JsonArray tools = AgentServer.Dispatch(Request(91, "tools/list"))!["result"]!.AsObject()["tools"]!.AsArray();
            Assert.DoesNotContain(tools, t => t!["name"]!.GetValue<string>() == "request-command-access");
        }
        finally {
            Program.IsProgramFilesInstallOverrideForTests = null;
        }
    }

    [Fact]
    public void FullToolsList_AdvertisesRequestCommandAccess() {
        JsonArray tools = AgentServer.Dispatch(Request(92, "tools/list"))!["result"]!.AsObject()["tools"]!.AsArray();
        Assert.Contains(tools, t => t!["name"]!.GetValue<string>() == "request-command-access");
    }

    // ---------------------------------------------------------------------------------------------
    // send_command now reports command-disabled honestly (it used to silently no-op)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SendCommand_DisabledCommand_IsRefusedWithRecoveryPointer() {
        JsonObject env = Envelope(Call(100, "send_command", new JsonObject { ["command"] = "chars:hello" }));
        Assert.Equal("command-disabled", ErrorCode(env));
        Assert.Contains("request-command-access", env["error"]!.AsObject()["detail"]!.GetValue<string>());
    }

    [Fact]
    public void SendCommand_SingleCharWithDisabledChars_IsRefusedAsCharsDisabled() {
        JsonObject env = Envelope(Call(101, "send_command", new JsonObject { ["command"] = "a" }));
        Assert.Equal("command-disabled", ErrorCode(env));
        Assert.Contains("chars:", env["error"]!.AsObject()["detail"]!.GetValue<string>());
    }

    [Fact]
    public void SendCommand_UnknownCommand_StillReportsUnknown() {
        JsonObject env = Envelope(Call(102, "send_command", new JsonObject { ["command"] = "definitely-not-a-command" }));
        Assert.Equal("unknown-command", ErrorCode(env));
    }
}
