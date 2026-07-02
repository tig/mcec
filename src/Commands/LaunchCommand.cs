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
/// SECURITY: gated behind <see cref="AgentRuntime.AgentCommandsEnabled"/> (enforced structurally by
/// <see cref="AgentCommand"/>) and per-command Enabled.
/// </summary>
public class LaunchCommand : AgentCommand {
    [XmlAttribute("path")] public string Path { get; set; } = null!;
    [XmlAttribute("arguments")] public string Arguments { get; set; } = null!;
    [XmlAttribute("workingdirectory")] public string WorkingDirectory { get; set; } = null!;
    [XmlAttribute("timeout")] public int Timeout { get; set; }

    public static List<Command> BuiltInCommands {
        get => [new LaunchCommand { Cmd = "launch" }];
    }

    public LaunchCommand() { }

    // Audits after path validation (below), with the effective timeout — not via AuditDetails.
    protected override CommandResult ExecuteCore() {
        if (string.IsNullOrWhiteSpace(Path)) {
            return CommandResult.Fail(Cmd,
                "launch requires a non-empty 'path' (executable, shell: protocol, or .lnk).",
                "launch-path-missing", "invalid-argument");
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

            // CR resolution (Codex P2 feedback on PR #133):
            // When `path` is a shell: target, .lnk, or single-instance app, Process.Start(psi)
            // (even with UseShellExecute=true) can return null even though the launch "succeeded"
            // via ShellExecute and a window was activated or will appear. We must not fail the
            // `launch` tool in these cases. We report pid=0 when no Process object and fall back
            // to foreground window detection. This preserves the advertised behavior for shell
            // launches while still preferring pid-based discovery when available.
            // Test-first: LaunchCommandTests.cs was added first (basic coverage + note on this
            // scenario); the fix was applied after.

            int pid = p?.Id ?? 0;
            string processName = "";
            if (p != null) {
                try { processName = p.ProcessName; } catch { }
            }

            WindowInfo? win = pid > 0 ? WaitForWindowByPid(pid, timeout) : null;

            if (win is null) {
                // Fallback for shell/null-Process cases: brief pause then use foreground.
                // (Often the just-activated app window.)
                Thread.Sleep(250);
                try {
                    WindowInfo? fg = WindowResolver.Resolve(null, null, null, null, foreground: true);
                    if (fg is not null && !string.IsNullOrEmpty(fg.Title)) {
                        win = fg;
                    }
                }
                catch {
                    // best-effort only
                }
            }

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

            return CommandResult.Ok(Cmd, data);
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"{GetType().Name}: launch failed for '{Path}': {ex.Message}");
            return CommandResult.Fail(Cmd, $"Launch failed: {ex.Message}", "launch-failed", "internal");
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
