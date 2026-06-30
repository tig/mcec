// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace MCEControl.xUnit.Integration;

/// <summary>
/// Opt-in, desktop-disruptive end-to-end test: it dogfoods MCEC by having MCEC drive MCEC. The
/// <c>--mcp</c> server is sent JSON-RPC tool calls that (1) launch a second MCEC via the <b>Win+R</b>
/// Run dialog (keyboard), (2) open its Help &gt; About box with a real <b>mouse</b> click on the Help
/// menu plus a <b>keyboard</b> "A", (3) <c>capture</c>/<c>query</c> the About dialog to prove it, and
/// (4) gracefully shut the controlled instance down (Esc to dismiss About, then Alt+F, x = File &gt; Exit).
///
/// This drives the REAL desktop (global keystrokes, mouse, launching apps), so it is skipped unless the
/// environment variable <c>MCEC_DESKTOP_E2E=1</c> is set. It never runs on CI or a normal `dotnet test`.
/// Run it deliberately on an interactive session:
///   <c>$env:MCEC_DESKTOP_E2E=1; dotnet test --filter Category=DesktopE2E</c>
/// </summary>
public class AgentDesktopE2ETests {
    [Fact]
    [Trait("Category", "DesktopE2E")]
    public void Mcec_DrivesMcec_LaunchViaWinR_OpenAbout_GracefulExit() {
        if (Environment.GetEnvironmentVariable("MCEC_DESKTOP_E2E") != "1") {
            return; // gated off (CI / normal test runs do not touch the desktop)
        }

        string exe = LocateMcecExe();
        string dir = Path.GetDirectoryName(exe)!;

        DesktopE2ENative.SetProcessDPIAware();
        int sw = DesktopE2ENative.GetSystemMetrics(DesktopE2ENative.SM_CXSCREEN);
        int sh = DesktopE2ENative.GetSystemMetrics(DesktopE2ENative.SM_CYSCREEN);

        string settingsPath = Path.Combine(dir, "mcec.settings");
        string commandsPath = Path.Combine(dir, "mcec.commands");
        string? savedSettings = BackUp(settingsPath);
        string? savedCommands = BackUp(commandsPath);

        Process? driver = null;
        try {
            File.WriteAllText(settingsPath, SettingsXml());
            File.WriteAllText(commandsPath, CommandsXml());

            driver = StartDriver(exe, dir);

            Assert.NotNull(driver);
            JsonObject init = Rpc(driver, 1, "initialize", []);
            Assert.Equal("MCEC", init["result"]!["serverInfo"]!["name"]!.GetValue<string>());

            // (1) KEYBOARD: Win+R -> type the mcec.exe path -> Enter -> launches a second MCEC.
            SendCommand(driver, 10, "winr");
            Thread.Sleep(1800);
            SendCommand(driver, 11, "chars:" + exe);
            Thread.Sleep(1000);
            SendCommand(driver, 12, "enter");

            JsonObject? tree = PollForWindow(driver, "MCEC", 20);
            Assert.True(tree is not null, "Second MCEC window never appeared after Win+R launch.");

            // (2) MOUSE: click the Help menu (rect from UIA) ; KEYBOARD: 'A' opens About.
            JsonObject? help = FindNode(tree!, n =>
                (n["controlType"]?.GetValue<string>() ?? "").Contains("MenuItem", StringComparison.Ordinal)
                && (n["name"]?.GetValue<string>() ?? "") == "Help");
            Assert.True(help is not null, "Help menu item not found in the controlled MCEC's UIA tree.");

            int cx = help!["x"]!.GetValue<int>() + help["width"]!.GetValue<int>() / 2;
            int cy = help["y"]!.GetValue<int>() + help["height"]!.GetValue<int>() / 2;
            int ax = (int)Math.Round(cx * 65535.0 / (sw - 1));
            int ay = (int)Math.Round(cy * 65535.0 / (sh - 1));
            SendCommand(driver, 20, $"mouse:mt,{ax},{ay}");
            SendCommand(driver, 21, "mouse:lbc");
            Thread.Sleep(800);
            SendCommand(driver, 22, "key_a");
            Thread.Sleep(1200);

            // (3) verify the About dialog via capture + query.
            JsonObject capCall = Rpc(driver, 30, "tools/call",
                ToolParams("capture", new JsonObject { ["window"] = "About" }));
            Assert.True(HasImageContent(capCall), "capture of the About window returned no image content.");

            JsonObject aboutQ = Rpc(driver, 31, "tools/call",
                ToolParams("query", new JsonObject { ["window"] = "About" }));
            Assert.Contains("About", aboutQ.ToJsonString(), StringComparison.Ordinal);

            // (4) GRACEFUL SHUTDOWN: Esc dismisses About, then Alt+F, x = File > Exit -> ShutDown().
            SendCommand(driver, 40, "key_esc");
            Thread.Sleep(700);
            SendCommand(driver, 41, "alt_f");
            Thread.Sleep(700);
            SendCommand(driver, 42, "key_x");

            bool gone = PollUntilWindowGone(driver, "MCEC", 12);
            Assert.True(gone, "Controlled MCEC instance did not exit gracefully after File > Exit.");
        }
        finally {
            try { driver?.StandardInput.Close(); } catch (IOException) { }
            try { if (driver is not null && !driver.WaitForExit(3000)) { driver.Kill(true); } }
            catch (InvalidOperationException) { } catch (IOException) { }
            driver?.Dispose();
            KillStrayInstances();
            Restore(settingsPath, savedSettings);
            Restore(commandsPath, savedCommands);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static Process StartDriver(string exe, string dir) {
        ProcessStartInfo psi = new() {
            FileName = exe,
            Arguments = "--mcp",
            WorkingDirectory = dir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = new UTF8Encoding(false),
        };
        Process p = Process.Start(psi)!;
        // Drain stderr (log4net noise) so it can't block the pipe.
        p.ErrorDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        return p;
    }

    private static JsonObject Rpc(Process p, int id, string method, JsonObject prms) {
        JsonObject req = new() {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = prms,
        };
        p.StandardInput.WriteLine(req.ToJsonString());
        p.StandardInput.Flush();
        string? line = p.StandardOutput.ReadLine();
        Assert.True(line is not null, $"No JSON-RPC response for {method}.");
        return JsonNode.Parse(line!)!.AsObject();
    }

    private static void SendCommand(Process p, int id, string command) =>
        Rpc(p, id, "tools/call", ToolParams("send_command", new JsonObject { ["command"] = command }));

    private static JsonObject ToolParams(string name, JsonObject args) =>
        new() { ["name"] = name, ["arguments"] = args };

    /// <summary>Polls query(window) until the UIA tree is available; returns the tree or null.</summary>
    private static JsonObject? PollForWindow(Process p, string window, int tries) {
        for (int i = 0; i < tries; i++) {
            JsonObject r = Rpc(p, 100 + i, "tools/call",
                ToolParams("query", new JsonObject { ["window"] = window, ["maxDepth"] = 4 }));
            JsonNode? tree = TreeOf(r);
            if (tree is JsonObject obj) {
                return obj;
            }
            Thread.Sleep(1000);
        }
        return null;
    }

    private static bool PollUntilWindowGone(Process p, string window, int tries) {
        for (int i = 0; i < tries; i++) {
            JsonObject r = Rpc(p, 200 + i, "tools/call",
                ToolParams("query", new JsonObject { ["window"] = window, ["maxDepth"] = 1 }));
            if (TreeOf(r) is null && IsToolError(r)) {
                return true;
            }
            Thread.Sleep(1000);
        }
        return false;
    }

    private static JsonNode? TreeOf(JsonObject toolCallResponse) {
        JsonObject? data = PayloadData(toolCallResponse);
        return data? ["tree"];
    }

    private static bool IsToolError(JsonObject toolCallResponse) {
        JsonNode? isError = toolCallResponse["result"]?["isError"];
        return isError is not null && isError.GetValue<bool>();
    }

    /// <summary>Parses the first text content block of a tools/call result into its CommandResult.data.</summary>
    private static JsonObject? PayloadData(JsonObject toolCallResponse) {
        if (toolCallResponse["result"]?["content"] is not JsonArray content) {
            return null;
        }
        foreach (JsonNode? block in content) {
            if (block?["type"]?.GetValue<string>() == "text") {
                try {
                    return JsonNode.Parse(block["text"]!.GetValue<string>())?["data"] as JsonObject;
                }
                catch (System.Text.Json.JsonException) {
                    return null;
                }
            }
        }
        return null;
    }

    private static bool HasImageContent(JsonObject toolCallResponse) {
        if (toolCallResponse["result"]?["content"] is not JsonArray content) {
            return false;
        }
        foreach (JsonNode? block in content) {
            if (block?["type"]?.GetValue<string>() == "image") {
                return true;
            }
        }
        return false;
    }

    private static JsonObject? FindNode(JsonObject node, Func<JsonObject, bool> pred) {
        if (pred(node)) {
            return node;
        }
        if (node["children"] is JsonArray kids) {
            foreach (JsonNode? c in kids) {
                if (c is JsonObject child) {
                    JsonObject? found = FindNode(child, pred);
                    if (found is not null) {
                        return found;
                    }
                }
            }
        }
        return null;
    }

    private static string LocateMcecExe() {
        // Walk up from the test output dir to the repo root, then into the matching src build output.
        string? dir = AppContext.BaseDirectory;
        string config = dir.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release" : "Debug";
        for (int i = 0; i < 8 && dir is not null; i++) {
            string candidate = Path.Combine(dir, "src", "bin", config, "net10.0-windows", "mcec.exe");
            if (File.Exists(candidate)) {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        throw new FileNotFoundException("Could not locate the built mcec.exe relative to the test output.");
    }

    private static string? BackUp(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static void Restore(string path, string? saved) {
        try {
            if (saved is null) {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
            else {
                File.WriteAllText(path, saved);
            }
        }
        catch (IOException) { }
    }

    private static void KillStrayInstances() {
        foreach (Process p in Process.GetProcessesByName("mcec")) {
            try { p.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            p.Dispose();
        }
    }

    private static string SettingsXml() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<AppSettings>\r\n" +
        "  <AgentCommandsEnabled>true</AgentCommandsEnabled>\r\n" +
        "  <McpServerEnabled>false</McpServerEnabled>\r\n" +
        "  <ActAsServer>false</ActAsServer>\r\n" +
        "  <DisableUpdatePopup>true</DisableUpdatePopup>\r\n" +
        "</AppSettings>\r\n";

    private static string CommandsXml() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<MCEController xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\r\n" +
        "               xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" version=\"3.0.0\">\r\n" +
        "<Commands xmlns=\"http://www.kindel.com/products/mcecontroller\">\r\n" +
        // Agent observation commands must be enabled per-command (the second security gate), in
        // addition to AgentCommandsEnabled in settings, for the MCP tools to run them.
        "  <capture Cmd=\"capture\" Enabled=\"true\" />\r\n" +
        "  <query   Cmd=\"query\"   Enabled=\"true\" />\r\n" +
        "  <find     Cmd=\"find\"   Enabled=\"true\" />\r\n" +
        "  <invoke  Cmd=\"invoke\"  Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"winr\" Vk=\"r\" Win=\"true\" Enabled=\"true\" />\r\n" +
        "  <Chars     Cmd=\"chars:\" Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"enter\" Vk=\"VK_RETURN\" Enabled=\"true\" />\r\n" +
        "  <Mouse     Cmd=\"mouse:\" Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"key_a\" Vk=\"a\" Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"key_esc\" Vk=\"VK_ESCAPE\" Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"alt_f\" Vk=\"f\" Alt=\"true\" Enabled=\"true\" />\r\n" +
        "  <SendInput Cmd=\"key_x\" Vk=\"x\" Enabled=\"true\" />\r\n" +
        "</Commands>\r\n" +
        "</MCEController>\r\n";
}
