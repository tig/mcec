using System.Collections.Generic;
using Xunit;

namespace MCEControl.xUnit;

// #216: AppSettings is now a (mostly) pure POCO. Persistence tests live in
// Services/SettingsStoreTests.cs; registry policy tests in Services/MachinePolicyTests.cs.
public class AppSettingsTests
{
    /// <summary>
    /// Tests that SafeForTelemetryAttribute is working
    /// </summary>
    [Fact]
    public void GetTelemetryDictionary_Test()
    {
        AppSettings appSettings = new AppSettings();
        IDictionary<string, string>? dict = appSettings.GetTelemetryDictionary();

        Assert.True(dict.ContainsKey("AutoStart"));

        Assert.False(dict.ContainsKey("WakeupCommand"));
    }
}
