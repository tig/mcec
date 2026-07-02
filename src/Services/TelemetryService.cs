// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Win32;

namespace MCEControl;

/// <summary>
///     Singleton class enabling Microsoft Azure Application Insights telemetry
/// </summary>
public partial class TelemetryService {
    private static readonly Lazy<TelemetryService> _lazy = new(() => new TelemetryService());

    private readonly TelemetryConfiguration _config = TelemetryConfiguration.CreateDefault();
    public static TelemetryService Instance => _lazy.Value;

    public bool TelemetryEnabled { get; set; }
    public Stopwatch? RunTime { get; set; }

    // internal setter so tests can substitute a client backed by a stub channel (#156).
    public TelemetryClient? TelemetryClient { get; internal set; }

    public void Start(string appName, IDictionary<string, string>? startProperties = null) {
        RunTime = Stopwatch.StartNew();

        object? val = AppSettings.GetRegistryValue("Telemetry", 0);
        TelemetryEnabled = val != null && val.ToString() == "1" ? true : false;

        // Setup telemetry via Azure Application Insights.

        // Get key from UserSecrets in a way that never puts the key in source
        if (!string.IsNullOrWhiteSpace(TelemetryService.Key)) {
            _config.ConnectionString = TelemetryService.Key;
        }

        // Turn off Debug spew
        TelemetryDebugWriter.IsTracingDisabled = true;
#if DEBUG
        _config.TelemetryChannel.DeveloperMode = true;
#else
        _config.TelemetryChannel.DeveloperMode = Debugger.IsAttached;
#endif

        TelemetryClient = new TelemetryClient(_config);

        TelemetryClient.Context.Component.Version = FileVersionInfo
            .GetVersionInfo(Assembly.GetAssembly(typeof(TelemetryService))!.Location).FileVersion;
        TelemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
        TelemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
        // Anonymyize user ID
        SHA256 h = SHA256.Create();
        h.Initialize();
        h.ComputeHash(Encoding.UTF8.GetBytes($"{Environment.UserName}/{Environment.MachineName}"));
        TelemetryClient.Context.User.Id = Convert.ToBase64String(h.Hash!);
        // See: https://stackoverflow.com/questions/42861344/how-to-overwrite-or-ignore-cloud-roleinstance-with-application-insights
        TelemetryClient.Context.Cloud.RoleInstance = TelemetryClient.Context.User.Id;

        // TELEMETRY: 
        // what: application properties
        // why: to track versions in use, OS support, and .NET versions
        // how is PII protected: none of this is PII
        if (startProperties == null) {
            startProperties = new Dictionary<string, string>();
        }

        // Merged passed in properites
        startProperties.Concat(new Dictionary<string, string> {
            ["app"] = appName,
            ["version"] = TelemetryClient.Context.Component.Version!,
            ["os"] = Environment.OSVersion.ToString(),
            ["arch"] = Environment.Is64BitProcess ? "x64" : "x86",
            ["dotNetVersion"] = Environment.Version.ToString()
        }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        TrackEvent("Application Started", startProperties);
    }

    public void Stop() {
        // TELEMETRY: 
        // what: application runtime
        // why: to understand how long the app stays running
        // how is PII protected: the time the app runs is not PII
        TrackEvent("Application Stopped",
            metrics: new Dictionary<string, double> { { "runTime", RunTime!.Elapsed.TotalMilliseconds } });

        // before exit, flush the remaining data
        Flush();
        // Flush is not blocking so wait a bit
        Task.Delay(1000).Wait();
    }

    public void TrackEvent(string key, IDictionary<string, string>? properties = null,
        IDictionary<string, double>? metrics = null) {
        if (TelemetryEnabled && TelemetryClient != null) {
            TelemetryClient.TrackEvent(key, properties, metrics);
        }
    }

    public void TrackException(Exception ex, bool log = false) {
        if (ex != null && log is true) {
            Logger.Instance.Log4.Debug($"Exception: {ex.Message}");
        }

        if (TelemetryClient != null && ex != null && TelemetryEnabled) {
            // TELEMETRY:
            // what: exception type, scrubbed message, and scrubbed stack
            // why: to diagnose crashes and failures in the field
            // how is PII protected: user-profile paths and username path segments in the
            // message/stack are redacted (#156) so the cleartext Windows username never leaves
            // the machine; User.Id stays pseudonymized.
            ExceptionTelemetry telex = TelemetryScrubber.CreateScrubbedExceptionTelemetry(ex);
            TelemetryClient.TrackException(telex);
            Flush();
        }
    }

    internal void Flush() {
        if (TelemetryClient != null) {
            TelemetryClient.Flush();
        }
    }
}
