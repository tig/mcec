// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace MCEControl;

/// <summary>
/// Per-machine (HKLM) policy reads: the telemetry opt-in and the DisableInternalCommands override.
/// Extracted from <see cref="AppSettings"/> (#216) so the settings POCO carries no registry access.
/// </summary>
public static class MachinePolicy {
    // Registry key for per-machine settings (telemetry opt-in, disable-internal-commands override).
    // For MCEC 3.0 rebrand, new location under "Kindel"; legacy "Kindel Systems" is read as fallback for upgrades.
    public const string RegistryKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel\MCE Controller";
    private const string LegacyRegistryKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller";

    /// <summary>
    /// Read a registry value preferring the current (Kindel) key, falling back to legacy (Kindel Systems) key.
    /// SECURITY (issue #155): a registry problem (deny-read ACE, key marked for deletion) must never
    /// crash startup; such failures return <paramref name="defaultValue"/> and are logged.
    /// </summary>
    public static object? GetRegistryValue(string valueName, object? defaultValue) {
        return GetRegistryValue(valueName, defaultValue, Registry.GetValue);
    }

    /// <summary>
    /// Seam for <see cref="GetRegistryValue(string, object?)"/>: <paramref name="getValue"/> stands in
    /// for <see cref="Registry.GetValue(string, string?, object?)"/> so the failure path is testable
    /// without touching the real registry.
    /// </summary>
    internal static object? GetRegistryValue(string valueName, object? defaultValue, Func<string, string?, object?, object?> getValue) {
        try {
            object? val = getValue(RegistryKeyPath, valueName, null) ?? getValue(LegacyRegistryKeyPath, valueName, defaultValue);
            return val;
        }
        catch (Exception e) when (e is System.Security.SecurityException || e is IOException || e is UnauthorizedAccessException) {
            Logger.Instance.Log4.Error(
                $"Settings: Could not read registry value '{valueName}'; using default ({defaultValue ?? "null"}). {e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts a raw registry value to a bool, tolerating junk (issue #155). A REG_SZ like "banana"
    /// makes <see cref="Convert.ToBoolean(object?, IFormatProvider)"/> throw FormatException (and a
    /// REG_BINARY blob throws InvalidCastException); a machine-policy value that cannot be parsed
    /// must not crash startup. Falls back to <paramref name="defaultValue"/> and logs the bad value
    /// so an admin can fix it.
    /// </summary>
    internal static bool RegistryValueToBoolean(object? value, bool defaultValue) {
        if (value is null) {
            return defaultValue;
        }
        try {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException || e is InvalidCastException) {
            Logger.Instance.Log4.Error(
                $"Settings: Ignoring invalid registry value '{value}' (expected a boolean); using default ({defaultValue}). {e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// The machine-wide DisableInternalCommands override. Read on every settings load, regardless
    /// of how (or whether) the settings file loaded. Tolerates junk values (see #155).
    /// </summary>
    public static bool GetDisableInternalCommands() {
        return RegistryValueToBoolean(GetRegistryValue("DisableInternalCommands", false), false);
    }
}
