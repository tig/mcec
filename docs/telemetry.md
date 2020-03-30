# How MCE Controller Uses Telemetry

MCE Controller includes functionality to collect Telemetry Data, which is a term to denote data about how the software is used or performing. Telemetry Data is collected through a “phone home” mechanism built into the software itself. 

The MCE Controller installer provides  users an option to opt-in to share statistical data with the developers of the software.

[Installer Screenshot](telemetry_optin.png)

## Frequently Asked Questions

### What data is collected?

First, here's what is explicitly NOT collected:

* User names and IDs
* Machine names and IDs
* File and directory names
* Network host names
* User defined commands and other attributes

[This search shows](https://github.com/tig/mcec/search?q=TrackEvent&unscoped_q=TrackEvent) all instances of `TrackEvent` in the source-code with comments describing the telemetry data being collected and the rationale.

In addition, the source code has been annotated with comments that include `// TELEMETRY:` with a clear description of the information collected. For example:

```csharp
// TELEMETRY: 
// what: application runtime
// why: to understand how long the app stays running
// how is PII protected: the time the app runs is not PII
TrackEvent("Application Stopped", metrics: new Dictionary<string, double>
    {{"runTime", _runTime.Elapsed.TotalMilliseconds}});
```

### What measures have been implemented to ensure no personally identifiable information is collected via telemetry?

First, all telemetry data is regularly reviewed to identify potentially personally identifiable information. If there's any risk, a quick revision of the software is made to stop collecting such information. For example, an early build with telemetry enabled was incorrectly collecting the end user's machine name. This was fixed immediately with commit `046c5c3`.

For any data structures which are collected 'en-mass`, a property attribute has been implemented such that only properties that are explicitly tagged for collection will be collected. This [search illustrates](https://github.com/tig/mcec/search?q=SafeForTelemetryAttribute&unscoped_q=SafeForTelemetryAttribute) this mechanism.

For example, users can change the wakeup command used. While it's unlikely a user would ever set the wakeup command to be something with PII in it, they may. To protect against collecting such PII the `WakeupCommand` setting is NOT tagged with `SafeForTelemetryAttribute`:

```csharp
// [SafeForTelemetryAttribute] 
// TELEMETRY: WakeupCommand can be set by user and thus may contain PII, so it is not collected
public string WakeupCommand { get => wakeupCommand; set => wakeupCommand = value; }
```

### What system is used to collect and analyzed telemetry?

Microsoft Azure Application Insights.

### Can end-users review the telemetry information collected?

Yes, anyone who would like to review that information should create a GitHub issue requesting access. 