// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// The <see cref="IAppHost"/> for headless <c>--mcp</c> mode (#209), registered by
/// <c>Program.RunHeadlessMcp</c> next to the rest of the <see cref="AgentRuntime"/> bootstrap.
/// There is no MainWindow and no legacy transports (the operator surface is
/// <see cref="HeadlessOperatorUi"/>'s pump thread: e-stop hotkey, overlay, re-arm prompt); so
/// <see cref="SendLine"/> is a logged no-op, <see cref="RequestShutdown"/> performs the same clean
/// teardown the stdio-EOF path does and exits the process, and <see cref="MessageWindowHandle"/>
/// throws (the only consumer, the activity monitor's power-broadcast detection, is started
/// exclusively by the GUI host).
/// </summary>
internal sealed class HeadlessAppHost : IAppHost {
    /// <summary>
    /// How long <see cref="RequestShutdown"/> waits before tearing down. <c>mcec:exit</c> executes on
    /// the invoker's dispatcher thread; the agent's <c>send_command</c> awaits a completion marker the
    /// dispatcher signals AFTER Execute returns, and only then writes the JSON-RPC response line. This
    /// grace lets that response reach the client before the process ends. Internal so tests can shrink it.
    /// </summary>
    internal static int ShutdownGraceMs { get; set; } = 500;

    /// <summary>
    /// TEST SEAM: what actually ends the process. Defaults to <see cref="Environment.Exit"/>; tests
    /// swap in a probe so exercising the shutdown path doesn't kill the test runner.
    /// </summary>
    internal static Action<int> ExitProcess { get; set; } = Environment.Exit;

    public void SendLine(string line) =>
        // No legacy transports (TCP/serial) exist headless; drop the line but leave a trace so a
        // misconfigured activity monitor is diagnosable.
        Logger.Instance.Log4.Debug($"{nameof(HeadlessAppHost)}: no transports in --mcp mode; SendLine dropped: {line}");

    public void RequestShutdown() {
        Logger.Instance.Log4.Info(
            $"{nameof(HeadlessAppHost)}: shutdown requested; exiting after in-flight replies flush ({ShutdownGraceMs}ms grace).");
        // Deferred so the caller (typically mcec:exit on the dispatcher thread) can finish its
        // Execute, the dispatcher can signal send_command's completion marker, and the stdio writer
        // (AutoFlush) can emit the JSON-RPC response. Then the same teardown Program's stdio-EOF path
        // performs: stop the operator safety surface (disarm the hotkey, drop the overlay), stop the
        // dispatcher (dropping the queue releases any held input), and exit.
        _ = Task.Run(() => {
            Thread.Sleep(ShutdownGraceMs);
            HeadlessOperatorUi.Stop();
            AgentRuntime.Invoker?.Shutdown(joinTimeoutMs: 2000);
            ExitProcess(0);
        });
    }

    public IntPtr MessageWindowHandle =>
        throw new InvalidOperationException(
            "No message window exists in headless --mcp mode. UserActivityMonitorService's power-broadcast " +
            "detection is only started by the GUI host (MainWindow.Start); it cannot run headless.");
}
