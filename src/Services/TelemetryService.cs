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

        private static readonly Lazy<TelemetryService> lazy = new Lazy<TelemetryService>(() => new TelemetryService());
        public static TelemetryService Instance => lazy.Value;

        public bool TelemetryEnabled { get; set; }

        public TelemetryClient GetTelemetryClient() => telemetry;
        private TelemetryClient telemetry;

        private Stopwatch runtime;

        public void Start(string appName, IDictionary<string, string> startProperties = null) {
            runtime = System.Diagnostics.Stopwatch.StartNew();

            var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller", "Telemetry", 0);
            TelemetryEnabled = (val != null && val.ToString() == "1") ? true : false;

            // Setup telemetry via Azure Application Insights.
            using var config = TelemetryConfiguration.CreateDefault();

            // Get key from UserSecrets in a way that never puts the key in source
            config.InstrumentationKey = TelemetryService.Key;

            // Turn off Debug spew
            TelemetryDebugWriter.IsTracingDisabled = true;
#if DEBUG
            config.TelemetryChannel.DeveloperMode = true;
#else
            config.TelemetryChannel.DeveloperMode = Debugger.IsAttached;
#endif

            telemetry = new TelemetryClient(config);
            telemetry.Context.Component.Version = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(TelemetryService)).Location).FileVersion;
            telemetry.Context.Session.Id = Guid.NewGuid().ToString();
            telemetry.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            // Anonymyize user ID
            using var h = SHA256.Create();
            h.Initialize();
            h.ComputeHash(Encoding.UTF8.GetBytes($"{Environment.UserName}/{Environment.MachineName}"));
            telemetry.Context.User.Id = Convert.ToBase64String(h.Hash);

            if (startProperties == null)
                startProperties = new Dictionary<string, string>();

            // Merged passed in properites
            startProperties.Concat(new Dictionary<string, string> {
                ["app"] = appName,
                ["version"] = telemetry.Context.Component.Version,
                ["os"] = Environment.OSVersion.ToString(),
                ["arch"] = Environment.Is64BitProcess ? "x64" : "x86",
                ["dotNetVersion"] = Environment.Version.ToString()
            }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            TrackEvent("Application Started", startProperties);
        }

        public void Stop() {
            TrackEvent("Application Stopped", metrics: new Dictionary<string, double>
                {{"runTime", runtime.Elapsed.TotalMilliseconds}});

            // before exit, flush the remaining data
            Flush();
            // Flush is not blocking so wait a bit
            Task.Delay(1000).Wait();
        }
        public void SetUser(string user) {
            telemetry.Context.User.AuthenticatedUserId = user;
        }

        public void TrackEvent(string key, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null) {
            if (TelemetryEnabled && telemetry != null) {
                telemetry.TrackEvent(key, properties, metrics);
            }
        }

        public void TrackException(Exception ex, bool log = false) {
            if (ex != null && log is true)
                Logger.Instance.Log4.Debug($"Exception: {ex.Message}");

            if (telemetry != null && ex != null && TelemetryEnabled) {
                var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex);
                telemetry.TrackException(telex);
                Flush();
            }
        }
        internal void Flush() {
            if (telemetry != null)
                telemetry.Flush();
        }

        //private class CustomConverter : TraceTelemetryConverter {
        //    public override IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider) {
        //        // first create a default TraceTelemetry using the sink's default logic
        //        // .. but without the log level, and (rendered) message (template) included in the Properties
        //        foreach (ITelemetry telemetry in base.Convert(logEvent, formatProvider)) {
        //            // then go ahead and post-process the telemetry's context to contain the user id as desired
        //            if (logEvent.Properties.ContainsKey("UserId")) {
        //                telemetry.Context.User.Id = logEvent.Properties["UserId"].ToString();
        //            }
        //            // post-process the telemetry's context to contain the operation id
        //            if (logEvent.Properties.ContainsKey("operation_Id")) {
        //                telemetry.Context.Operation.Id = logEvent.Properties["operation_Id"].ToString();
        //            }
        //            // post-process the telemetry's context to contain the operation parent id
        //            if (logEvent.Properties.ContainsKey("operation_parentId")) {
        //                telemetry.Context.Operation.ParentId = logEvent.Properties["operation_parentId"].ToString();
        //            }
        //            // typecast to ISupportProperties so you can manipulate the properties as desired
        //            ISupportProperties propTelematry = (ISupportProperties)telemetry;

        //            // find redundent properties
        //            var removeProps = new[] { "UserId", "operation_parentId", "operation_Id" };
        //            removeProps = removeProps.Where(prop => propTelematry.Properties.ContainsKey(prop)).ToArray();

        //            foreach (var prop in removeProps) {
        //                // remove redundent properties
        //                propTelematry.Properties.Remove(prop);
        //            }

        //            yield return telemetry;
        //        }
        //    }
        //}
    }
}
