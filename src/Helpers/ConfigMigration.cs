// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;

namespace MCEControl;

/// <summary>
/// v3.0 renamed the executable to <c>mcec.exe</c> and the config files to <c>mcec.settings</c> /
/// <c>mcec.commands</c> (from <c>MCEControl.settings</c> / <c>MCEControl.commands</c>). To preserve an
/// existing user's configuration across the upgrade, this copies the legacy files to their new names
/// on first run when the new file does not yet exist. The legacy files are left in place (non-destructive).
/// </summary>
public static class ConfigMigration {
    public static void Run(string configPath) {
        Migrate(configPath, "MCEControl.settings", "mcec.settings");
        Migrate(configPath, "MCEControl.commands", "mcec.commands");
    }

    private static void Migrate(string configPath, string legacyName, string newName) {
        try {
            string legacy = Path.Combine(configPath, legacyName);
            string current = Path.Combine(configPath, newName);
            if (File.Exists(legacy) && !File.Exists(current)) {
                File.Copy(legacy, current);
                Logger.Instance.Log4.Info($"ConfigMigration: migrated {legacyName} -> {newName}");
            }
        }
        catch (IOException e) {
            Logger.Instance.Log4.Error($"ConfigMigration: could not migrate {legacyName}: {e.Message}");
        }
        catch (UnauthorizedAccessException e) {
            Logger.Instance.Log4.Error($"ConfigMigration: could not migrate {legacyName}: {e.Message}");
        }
    }
}
