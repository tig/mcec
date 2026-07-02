// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;

namespace MCEControl.xUnit.Services;

/// <summary>
/// In-memory Application Insights channel for tests. Captures telemetry items locally so tests
/// can assert on the outgoing payload; nothing ever leaves the process.
/// </summary>
internal sealed class StubTelemetryChannel : ITelemetryChannel {
    private readonly ConcurrentQueue<ITelemetry> _sent = new();

    public IReadOnlyList<ITelemetry> SentItems => _sent.ToList();

    public bool? DeveloperMode { get; set; }
    public string? EndpointAddress { get; set; }

    public void Send(ITelemetry item) => _sent.Enqueue(item);

    public void Flush() {
        // Nothing to flush; items are captured synchronously in Send.
    }

    public void Dispose() {
        // Nothing to dispose.
    }
}
