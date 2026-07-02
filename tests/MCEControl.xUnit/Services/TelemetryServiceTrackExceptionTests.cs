// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// End-to-end tests for #156: <see cref="TelemetryService.TrackException"/> must route the
/// exception through the scrubber so the payload handed to the telemetry channel contains no
/// user-profile paths. Uses a stub channel — no telemetry leaves the process.
/// Joins the "AgentSerial" collection because tests in that collection initialize the
/// TelemetryService singleton (AgentTestSupport.EnsureTelemetry); this test temporarily swaps
/// the singleton's client and must not race with them.
/// </summary>
[Collection("AgentSerial")]
public class TelemetryServiceTrackExceptionTests {
    private static Exception Thrown(Exception ex) {
        try {
            throw ex;
        }
        catch (Exception caught) {
            return caught;
        }
    }

    [Fact]
    public void TrackException_WhenEnabled_ScrubsUserPathsFromOutgoingPayload() {
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

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Exception ex = Thrown(new IOException(
                $@"The process cannot access the file '{profile}\AppData\Roaming\MCEControl\MCEControl.settings'."));

            svc.TrackException(ex);

            ExceptionTelemetry sent = Assert.Single(channel.SentItems.OfType<ExceptionTelemetry>());
            ExceptionDetailsInfo details = Assert.Single(sent.ExceptionDetailsInfoList);
            Assert.Equal(typeof(IOException).FullName, details.TypeName);
            Assert.DoesNotContain(profile, details.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%USERPROFILE%", details.Message, StringComparison.Ordinal);
        }
        finally {
            svc.TelemetryEnabled = originalEnabled;
            svc.TelemetryClient = originalClient;
        }
    }

    [Fact]
    public void TrackException_WhenDisabled_SendsNothing() {
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

            svc.TrackException(Thrown(new InvalidOperationException("boom")));

            Assert.Empty(channel.SentItems);
        }
        finally {
            svc.TelemetryEnabled = originalEnabled;
            svc.TelemetryClient = originalClient;
        }
    }
}
