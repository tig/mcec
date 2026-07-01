// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent gated launch command: starts an application directly (path + optional args + working dir)
/// and returns structured info including the primary window handle when one appears.
/// Replaces fragile Win+R + chars + enter composition for agents.
///
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> and per-command Enabled.
/// </summary>
public class LaunchCommand : Command {
    [XmlAttribute("path")] public string Path { get; set; } = null!;
    [XmlAttribute("arguments")] public string Arguments { get; set; } = null!;
    [XmlAttribute("workingDirectory")] public string WorkingDirectory { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static new List<Command> BuiltInCommands {
        get => [new LaunchCommand { Cmd = "launch" }];
    }

    public LaunchCommand() { }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new LaunchCommand {
        Path = Path,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        Timeout = Timeout,
    });

    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path)) {
            Reply?.WriteLine(CommandResult.Fail(Cmd, "launch requires a non-empty 'path' (executable, shell: protocol, or .lnk).").ToJson());
            return false;
        }

        int timeout = Timeout > 0 ? Timeout : 5000;
        AgentRuntime.Audit(Cmd, $"path='{Path}' args='{Arguments}' cwd='{WorkingDirectory}' timeout={timeout}");

        try {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = Path,
                Arguments = Arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? string.Empty : WorkingDirectory,
                UseShellExecute = true,
            };

            Process? p = Process.Start(psi);
            if (p is null) {
                Reply?.WriteLine(CommandResult.Fail(Cmd, $"Failed to start process for path '{Path}'.").ToJson());
                return false;
            }

            // Best effort: capture the started pid and try to surface its main (or first titled) window.
            int pid = p.Id;
            string processName = "";
            try { processName = p.ProcessName; } catch { }

            WindowInfo? win = WaitForWindowByPid(pid, timeout);

            JsonObject data = new JsonObject {
                ["processId"] = pid,
                ["processName"] = processName,
                ["path"] = Path,
            };
            if (win is not null) {
                data["handle"] = win.Handle;
                data["window"] = win.ToJsonObject();
            }
            else {
                data["handle"] = 0;
                data["note"] = "Window did not appear within timeout; use query/process or wait-for by process to locate later.";
            }

            Reply?.WriteLine(CommandResult.Ok(Cmd, data).ToJson());
            return true;
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"{GetType().Name}: launch failed for '{Path}': {ex.Message}");
            Reply?.WriteLine(CommandResult.Fail(Cmd, $"Launch failed: {ex.Message}", "launch-failed", "internal").ToJson());
            return false;
        }
    }

    private static WindowInfo? WaitForWindowByPid(int pid, int timeoutMs) {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs) {
            foreach (WindowInfo w in WindowResolver.EnumerateTopLevel()) {
                if (w.ProcessId == pid && !string.IsNullOrEmpty(w.Title)) {
                    return w;
                }
            }
            // Also check the process MainWindowHandle directly (some apps set it promptly)
            try {
                using Process p = Process.GetProcessById(pid);
                IntPtr h = p.MainWindowHandle;
                if (h != IntPtr.Zero && !WindowResolver.IsIgnoredWindow(h.ToInt64())) {
                    // Re-describe to get full info
                    WindowInfo? described = null;
                    // Use the enumerator result if present, else synthesize via Describe if possible
                    foreach (WindowInfo w2 in WindowResolver.EnumerateTopLevel()) {
                        if (w2.Handle == h.ToInt64()) { described = w2; break; }
                    }
                    if (described is not null) return described;
                    // Fallback describe
                    return WindowResolver.Describe(h);
                }
            }
            catch {
                // process may be short lived or UWP bridge
            }

            Thread.Sleep(80);
        }
        return null;
    }
}
