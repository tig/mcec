// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using MCEControl;

namespace MCEControl.xUnit;

/// <summary>
/// Shared helpers for the agent tests. The agent commands follow the house pattern where
/// <see cref="Command.Execute"/> first calls <c>base.Execute()</c>, which (for an Enabled command)
/// records a telemetry metric via <c>TelemetryService.Instance.TelemetryClient</c>. That client is
/// null until <see cref="TelemetryService.Start"/> is called, so any test that drives an Enabled
/// command through Execute must initialize telemetry first. <see cref="EnsureTelemetry"/> does that
/// exactly once for the whole test run; it is safe to call from multiple threads.
/// </summary>
internal static class AgentTestSupport {
    private static readonly object _gate = new();
    private static bool _started;

    /// <summary>
    /// Initializes the telemetry singleton (idempotent) so <c>base.Execute()</c> on an Enabled
    /// command does not dereference a null telemetry client. Telemetry stays disabled (the registry
    /// opt-in is not set on CI), so nothing is actually transmitted.
    /// </summary>
    public static void EnsureTelemetry() {
        lock (_gate) {
            if (_started) {
                return;
            }
            TelemetryService.Instance.Start("MCEControl.xUnit");
            _started = true;
        }
    }
}
