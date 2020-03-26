using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Win32;
using log4net;

namespace MCEControl.Services {
    /// <summary>
    /// Singleton class enabling Microsoft Azure Application Insights telemetry
    /// </summary>
    public partial class TelemetryService {

        private static readonly Lazy<TelemetryService> _lazy = new Lazy<TelemetryService>(() => new TelemetryService());
        public static TelemetryService Instance => _lazy.Value;

        public bool TelemetryEnabled { get; set; }

        public TelemetryClient GetTelemetryClient() => _telemetry;
        private TelemetryClient _telemetry;
        TelemetryConfiguration _config = TelemetryConfiguration.CreateDefault();

        private Stopwatch _runTime;

        public void Start(string appName, IDictionary<string, string> startProperties = null) {
            _runTime = System.Diagnostics.Stopwatch.StartNew();

            var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller", "Telemetry", 0);
            TelemetryEnabled = (val != null && val.ToString() == "1") ? true : false;

            // Setup telemetry via Azure Application Insights.

            // Get key from UserSecrets in a way that never puts the key in source
            _config.InstrumentationKey = TelemetryService.Key;

            // Turn off Debug spew
            TelemetryDebugWriter.IsTracingDisabled = true;
#if DEBUG
            _config.TelemetryChannel.DeveloperMode = true;
#else
            _config.TelemetryChannel.DeveloperMode = Debugger.IsAttached;
#endif

            _telemetry = new TelemetryClient(_config);

            _telemetry.Context.Component.Version = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(TelemetryService)).Location).FileVersion;
            _telemetry.Context.Session.Id = Guid.NewGuid().ToString();
            _telemetry.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            // Anonymyize user ID
            using var h = SHA256.Create();
            h.Initialize();
            h.ComputeHash(Encoding.UTF8.GetBytes($"{Environment.UserName}/{Environment.MachineName}"));
            _telemetry.Context.User.Id = Convert.ToBase64String(h.Hash);
            // See: https://stackoverflow.com/questions/42861344/how-to-overwrite-or-ignore-cloud-roleinstance-with-application-insights
            _telemetry.Context.Cloud.RoleInstance = _telemetry.Context.User.Id;

            if (startProperties == null)
                startProperties = new Dictionary<string, string>();

            // Merged passed in properites
            startProperties.Concat(new Dictionary<string, string> {
                ["app"] = appName,
                ["version"] = _telemetry.Context.Component.Version,
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
            TrackEvent("Application Stopped", metrics: new Dictionary<string, double>
                {{"runTime", _runTime.Elapsed.TotalMilliseconds}});

            // before exit, flush the remaining data
            Flush();
            // Flush is not blocking so wait a bit
            Task.Delay(1000).Wait();
        }
        public void SetUser(string user) {
            _telemetry.Context.User.AuthenticatedUserId = user;
        }

        public void TrackEvent(string key, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null) {
            if (TelemetryEnabled && _telemetry != null) {
                _telemetry.TrackEvent(key, properties, metrics);
            }
        }

        public void TrackException(Exception ex, bool log = false) {
            if (ex != null && log is true)
                Logger.Instance.Log4.Debug($"Exception: {ex.Message}");

            if (_telemetry != null && ex != null && TelemetryEnabled) {
                var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex);
                _telemetry.TrackException(telex);
                Flush();
            }
        }
        internal void Flush() {
            if (_telemetry != null)
                _telemetry.Flush();
        }
    }
}
