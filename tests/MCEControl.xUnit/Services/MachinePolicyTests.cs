using System;
using System.IO;
using Xunit;

namespace MCEControl.xUnit;

// #216: registry policy reads moved from AppSettings to MachinePolicy. These tests exercise the
// injectable seam only; the real registry is never touched.
public class MachinePolicyTests
{
    /// <summary>
    /// Issue #155 (third bug): Convert.ToBoolean on a junk DisableInternalCommands registry value
    /// (e.g. REG_SZ "banana") threw FormatException and crashed startup. The conversion seam must
    /// tolerate junk and fall back to the default; no real registry involved here.
    /// </summary>
    [Theory]
    [InlineData(null, false, false)] // absent value -> default
    [InlineData(null, true, true)]
    [InlineData("banana", false, false)] // junk REG_SZ -> default (FormatException path)
    [InlineData("banana", true, true)]
    [InlineData("true", false, true)] // valid values still parse
    [InlineData("False", true, false)]
    [InlineData(1, false, true)] // REG_DWORD
    [InlineData(0, true, false)]
    public void RegistryValueToBoolean_ToleratesJunk(object? value, bool defaultValue, bool expected)
    {
        Assert.Equal(expected, MachinePolicy.RegistryValueToBoolean(value, defaultValue));
    }

    [Fact]
    public void RegistryValueToBoolean_BinaryJunk_ReturnsDefault()
    {
        // REG_BINARY comes back as byte[] -> InvalidCastException path
        Assert.False(MachinePolicy.RegistryValueToBoolean(new byte[] { 1, 2, 3 }, false));
        Assert.True(MachinePolicy.RegistryValueToBoolean(new byte[] { 1, 2, 3 }, true));
    }

    /// <summary>
    /// Issue #155 review follow-up (M1): Registry.GetValue itself can throw SecurityException
    /// (deny-read ACE) or IOException (key marked for deletion). GetRegistryValue must swallow
    /// these and return the supplied default; a registry problem must never crash startup
    /// (this also covers the TelemetryService opt-in read). Exercised through the injectable
    /// seam; the real registry is never touched.
    /// </summary>
    [Fact]
    public void GetRegistryValue_SecurityException_ReturnsDefault()
    {
        object? result = MachinePolicy.GetRegistryValue("Whatever", "fallback",
            (_, _, _) => throw new System.Security.SecurityException("deny-read ACE"));
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void GetRegistryValue_IOException_ReturnsDefault()
    {
        object? result = MachinePolicy.GetRegistryValue("Whatever", 42,
            (_, _, _) => throw new IOException("key has been marked for deletion"));
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetRegistryValue_UnauthorizedAccessException_ReturnsDefault()
    {
        object? result = MachinePolicy.GetRegistryValue("Whatever", null,
            (_, _, _) => throw new UnauthorizedAccessException("no read access"));
        Assert.Null(result);
    }

    [Fact]
    public void GetRegistryValue_LegacyKeyThrows_ReturnsDefault()
    {
        // Current key reads fine (absent -> null); the legacy fallback key is the one that throws.
        object? result = MachinePolicy.GetRegistryValue("Whatever", false,
            (key, _, _) => key == MachinePolicy.RegistryKeyPath
                ? null
                : throw new System.Security.SecurityException("deny-read ACE on legacy key"));
        Assert.Equal(false, result);
    }

    /// <summary>
    /// Fallback-key behavior (#216): a value absent under the current (Kindel) key must be read
    /// from the legacy (Kindel Systems) key, so upgraded machines keep their policy.
    /// </summary>
    [Fact]
    public void GetRegistryValue_CurrentKeyAbsent_FallsBackToLegacyKey()
    {
        object? result = MachinePolicy.GetRegistryValue("Telemetry", 0,
            (key, _, _) => key == MachinePolicy.RegistryKeyPath ? null : 1);
        Assert.Equal(1, result);
    }

    /// <summary>
    /// The current (Kindel) key wins when both keys have the value.
    /// </summary>
    [Fact]
    public void GetRegistryValue_CurrentKeyPresent_LegacyIgnored()
    {
        object? result = MachinePolicy.GetRegistryValue("Telemetry", 0,
            (key, _, _) => key == MachinePolicy.RegistryKeyPath ? 1 : 0);
        Assert.Equal(1, result);
    }
}
