// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Asserts <paramref name="text"/> contains neither the user-profile path nor the username
    /// as a path segment (the two forms TelemetryScrubber must redact).
    /// </summary>
    private static void AssertScrubbed(string? text, string profile, string userName) {
        if (string.IsNullOrEmpty(text)) {
            return;
        }

        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(userName)) {
            Assert.DoesNotMatch($@"(?i)[\\/]{Regex.Escape(userName)}(?=$|[^\w\-])", text);
            Assert.DoesNotMatch($@"(?i)(?<=^|[^\w\-]){Regex.Escape(userName)}(?=[\\/])", text);
        }
    }

    /// <summary>
    /// Reads the wire-level stack data of an <see cref="ExceptionDetailsInfo"/> (its internal
    /// ExceptionDetails: the <c>stack</c> string and each parsedStack frame's <c>fileName</c>),
    /// which the public API does not expose, so the test can verify what actually serializes.
    /// </summary>
    private static (string? Stack, List<string?> FileNames) GetInternalStackData(ExceptionDetailsInfo details) {
        PropertyInfo detailsProp = typeof(ExceptionDetailsInfo).GetProperty(
            "ExceptionDetails", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object internalDetails = detailsProp.GetValue(details)!;
        Type detailsType = internalDetails.GetType();

        string? stack = (string?)detailsType.GetProperty("stack")!.GetValue(internalDetails);

        var fileNames = new List<string?>();
        var parsedStack = (IEnumerable)detailsType.GetProperty("parsedStack")!.GetValue(internalDetails)!;
        foreach (object? frame in parsedStack) {
            fileNames.Add((string?)frame!.GetType().GetProperty("fileName")!.GetValue(frame));
        }

        return (stack, fileNames);
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
            string userName = Environment.UserName;
            Exception inner = Thrown(new UnauthorizedAccessException(
                $@"Access to the path '{profile}\AppData\Local\Temp\mcec.tmp' is denied."));
            Exception ex = Thrown(new IOException(
                $@"The process cannot access the file '{profile}\AppData\Roaming\MCEControl\MCEControl.settings'.",
                inner));

            svc.TrackException(ex);

            ExceptionTelemetry sent = Assert.Single(channel.SentItems.OfType<ExceptionTelemetry>());
            // The sanitized payload must not carry the original exception object.
            Assert.NotSame(ex, sent.Exception);

            Assert.Equal(2, sent.ExceptionDetailsInfoList.Count);
            Assert.Equal(typeof(IOException).FullName, sent.ExceptionDetailsInfoList[0].TypeName);
            Assert.Contains("%USERPROFILE%", sent.ExceptionDetailsInfoList[0].Message, StringComparison.Ordinal);

            // Every detail of the outgoing item — message, wire stack string, and every
            // parsedStack frame fileName — must be free of the profile path and username.
            bool sawStackData = false;
            foreach (ExceptionDetailsInfo details in sent.ExceptionDetailsInfoList) {
                AssertScrubbed(details.Message, profile, userName);

                (string? stack, List<string?> fileNames) = GetInternalStackData(details);
                sawStackData |= !string.IsNullOrEmpty(stack) || fileNames.Count > 0;
                AssertScrubbed(stack, profile, userName);
                foreach (string? fileName in fileNames) {
                    AssertScrubbed(fileName, profile, userName);
                }
            }

            // Guard against vacuously passing: thrown exceptions must have produced stack data.
            Assert.True(sawStackData, "expected the captured telemetry to include stack data");
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
