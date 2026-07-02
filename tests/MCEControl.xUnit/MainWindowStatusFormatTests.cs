//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using Xunit;
using MCEControl;

namespace MCEControl.xUnit;

/// <summary>
/// The pure status-formatting logic extracted from MainWindow's old triplicated painters and
/// switch handlers (#211). Static internals — no Form is constructed (InternalsVisibleTo).
/// </summary>
public class MainWindowStatusFormatTests {
    // ---- Traffic light mapping ----

    [Theory]
    [InlineData(ServiceStatus.Connected, StatusLight.Green)]
    [InlineData(ServiceStatus.Stopped, StatusLight.Gray)]
    // The shipped icon set has only red/green/gray (no yellow), so Started and Waiting both
    // map to red — same as all three pre-#211 painters.
    [InlineData(ServiceStatus.Started, StatusLight.Red)]
    [InlineData(ServiceStatus.Waiting, StatusLight.Red)]
    // Sleeping (the client's between-reconnects state) leaves the current light unchanged,
    // matching the old painters, which had no case for it.
    [InlineData(ServiceStatus.Sleeping, StatusLight.Unchanged)]
    public void StatusLightFor_MapsStatusToLight(ServiceStatus status, StatusLight expected) {
        Assert.Equal(expected, MainWindow.StatusLightFor(status));
    }

    // ---- Server ----

    [Theory]
    [InlineData(ServiceStatus.Started, "Started on port 5150")]
    [InlineData(ServiceStatus.Waiting, "Waiting for a client to connect")]
    [InlineData(ServiceStatus.Stopped, "Stopped")]
    [InlineData(ServiceStatus.Connected, null)] // the strip light says it all (old handler returned)
    public void FormatServerStatus_MatchesLegacyLogLines(ServiceStatus status, string? expected) {
        Assert.Equal(expected, MainWindow.FormatServerStatus(status, 5150));
    }

    // ---- Client ----

    [Theory]
    [InlineData(ServiceStatus.Started, "Connecting to host:5150")]
    [InlineData(ServiceStatus.Connected, "Connected to host:5150")]
    [InlineData(ServiceStatus.Stopped, "Stopped")]
    [InlineData(ServiceStatus.Sleeping, "Waiting 30 seconds to connect")]
    public void FormatClientStatus_MatchesLegacyLogLines(ServiceStatus status, string? expected) {
        Assert.Equal(expected, MainWindow.FormatClientStatus(status, "host", 5150, 30000));
    }

    // ---- Serial ----

    [Theory]
    [InlineData(ServiceStatus.Started, "Opening port: COM1 9600 baud N81 None")]
    [InlineData(ServiceStatus.Waiting, "Waiting for commands on COM1 9600 baud N81 None...")]
    [InlineData(ServiceStatus.Stopped, "Stopped")]
    [InlineData(ServiceStatus.Connected, null)]
    public void FormatSerialStatus_MatchesLegacyLogLines(ServiceStatus status, string? expected) {
        Assert.Equal(expected, MainWindow.FormatSerialStatus(status, "COM1 9600 baud N81 None"));
    }
}
