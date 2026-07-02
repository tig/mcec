// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for #199: <see cref="TelemetryService.TrackMetric"/> is the only supported way to send
/// metrics and must honor the user's telemetry opt-in (<c>TelemetryEnabled</c>) exactly like
/// <c>TrackEvent</c>/<c>TrackException</c>, and must never throw when the client has not been
/// constructed yet (i.e., before <c>Start()</c>). Uses a stub channel — no telemetry leaves the
/// process, and the real registry opt-in is never touched (the tests set
/// <c>TelemetryEnabled</c> directly).
/// Joins the "AgentSerial" collection because tests in that collection initialize the
/// TelemetryService singleton (AgentTestSupport.EnsureTelemetry); these tests temporarily swap
/// the singleton's client and must not race with them.
/// </summary>
[Collection("AgentSerial")]
public class TelemetryServiceTrackMetricTests {
    [Fact]
    public void TrackMetric_WhenEnabled_SendsMetricThroughChannel() {
        TelemetryService svc = TelemetryService.Instance;
        TelemetryClient? originalClient = svc.TelemetryClient;
        bool originalEnabled = svc.TelemetryEnabled;

        using StubTelemetryChannel channel = new();
        using TelemetryConfiguration config = new() {
            TelemetryChannel = channel,
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
        };

        try {
            svc.TelemetryClient = new TelemetryClient(config);
            svc.TelemetryEnabled = true;

            svc.TrackMetric("test Sent", 42);
            // GetMetric aggregates locally; Flush pushes the aggregate through the channel.
            svc.Flush();

            MetricTelemetry sent = Assert.Single(channel.SentItems.OfType<MetricTelemetry>());
            Assert.Equal("test Sent", sent.Name);
            Assert.Equal(42, sent.Sum);
        }
        finally {
            svc.TelemetryEnabled = originalEnabled;
            svc.TelemetryClient = originalClient;
        }
    }

    [Fact]
    public void TrackMetric_WhenDisabled_SendsNothing() {
        TelemetryService svc = TelemetryService.Instance;
        TelemetryClient? originalClient = svc.TelemetryClient;
        bool originalEnabled = svc.TelemetryEnabled;

        using StubTelemetryChannel channel = new();
        using TelemetryConfiguration config = new() {
            TelemetryChannel = channel,
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
        };

        try {
            svc.TelemetryClient = new TelemetryClient(config);
            svc.TelemetryEnabled = false;

            svc.TrackMetric("test Sent", 1);
            svc.Flush();

            Assert.Empty(channel.SentItems);
        }
        finally {
            svc.TelemetryEnabled = originalEnabled;
            svc.TelemetryClient = originalClient;
        }
    }

    [Fact]
    public void TrackMetric_WhenClientNull_DoesNotThrow() {
        TelemetryService svc = TelemetryService.Instance;
        TelemetryClient? originalClient = svc.TelemetryClient;
        bool originalEnabled = svc.TelemetryEnabled;

        try {
            svc.TelemetryClient = null;
            // Enabled on purpose: the null-client guard alone must make this safe (#199 —
            // previously anything sending a metric before Start() dereferenced null).
            svc.TelemetryEnabled = true;

            Exception? ex = Record.Exception(() => svc.TrackMetric("test Sent", 1));

            Assert.Null(ex);
        }
        finally {
            svc.TelemetryEnabled = originalEnabled;
            svc.TelemetryClient = originalClient;
        }
    }
}
