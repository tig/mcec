// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Owns <see cref="AppSettings"/> persistence: load/save of the XML settings file and the
/// settings-file path resolution. Extracted from <see cref="AppSettings"/> (#216) so the settings
/// POCO carries no I/O, no UI, and no telemetry; <see cref="Load"/> returns a
/// <see cref="SettingsLoadResult"/> and the HOST (GUI <c>MainWindow</c> or headless
/// <c>Program.RunHeadlessMcp</c>) decides what dialogs to show and what telemetry to emit.
/// </summary>
public static class SettingsStore {
    public const string SettingsFileName = "mcec.settings";

    /// <summary>
    /// By default we want the settings file stored with the EXE
    /// This allows the app to be run with multiple instances with a settings
    /// file for each instance (each being in different directory).
    /// However, typical installs get put into to %PROGRAMFILES% which
    /// is ACLd to allow only admin writes on Win7.
    /// </summary>
    /// <param name="startupPath">Path to where exe was started from (aka Application.StartupPath)</param>
    /// <returns>Path to where Settings &amp; Log Files should be</returns>
    public static string GetSettingsPath(string startupPath) {
        if (string.IsNullOrWhiteSpace(startupPath)) {
            throw new ArgumentException("startupPath must be specified", nameof(startupPath));
        }
        // If app was started from within ProgramFiles then use UserAppDataPath.
        // #216: this used to be a raw substring Contains() check, which false-positived on
        // sibling directories like "C:\Program Files Custom\..."; use a separator-aware,
        // case-insensitive path-prefix comparison instead.
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles) && IsPathUnder(startupPath, programFiles)) {
            // Strip off the trailing version ("\0.0.0.xxxx")
            startupPath = Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1));
        }

        return startupPath;
    }

    /// <summary>
    /// True if <paramref name="path"/> is <paramref name="directory"/> itself or a descendant of it.
    /// Separator-aware and case-insensitive (Windows semantics); both sides are normalized with
    /// <see cref="Path.GetFullPath(string)"/> so "C:\Program Files Custom" is NOT under "C:\Program Files".
    /// </summary>
    internal static bool IsPathUnder(string path, string directory) {
        string fullPath = Path.GetFullPath(path);
        string fullDir = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullDir, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullDir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Serializes <paramref name="settings"/> to XML at <paramref name="settingsFile"/>.
    /// Returns true on success. On failure, logs, sets <paramref name="error"/>, and returns
    /// false; the caller decides whether to surface it (the old <c>AppSettings.Serialize</c>
    /// showed a MessageBox from the data layer; see #216).
    /// </summary>
    public static bool TrySave(string settingsFile, AppSettings settings, out Exception? error) {
        ArgumentNullException.ThrowIfNull(settings);
        error = null;
        try {
            XmlSerializer ser = new XmlSerializer(typeof(AppSettings));
            // #216: the writer is now wrapped in `using`; the old code newed a StreamWriter and
            // only Close()d it on success, leaking the handle (and locking the file) on exception.
            using (StreamWriter sw = new StreamWriter(settingsFile)) {
                ser.Serialize(sw, settings);
            }

            Logger.Instance.Log4.Info("Settings: Wrote settings to " + settingsFile);
            return true;
        }
        catch (Exception e) {
            Logger.Instance.Log4.Info($"Settings: Settings file could not be written. {settingsFile} {e.Message}");
            error = e;
            return false;
        }
    }

    /// <summary>
    /// Loads settings from XML.
    /// SECURITY (issue #155): a bad settings file must never put MCEC into a fail-to-start state
    /// (GUI or headless --mcp). Missing file → defaults are created; corrupt/unreadable file →
    /// defaults are used for this run and the file is left untouched so the user can recover it.
    /// SECURITY (#216, real CA3075 fix): the file is user-writable and MCEC is network-facing, so
    /// the XML reader prohibits DTDs and has no resolver; no entity expansion, no external reads.
    /// Always returns usable settings (with the <see cref="MachinePolicy"/> DisableInternalCommands
    /// override applied); the outcome tells the host what happened. No dialogs, no telemetry here.
    /// </summary>
    /// <param name="settingsFile">full path to settings file</param>
    public static SettingsLoadResult Load(string settingsFile) {
        AppSettings? settings;
        SettingsLoadOutcome outcome;
        Exception? error = null;
        string? errorDetail = null;

        XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
        try {
            using FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
            XmlReaderSettings readerSettings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };
            using XmlReader reader = XmlReader.Create(fs, readerSettings);
            settings = (AppSettings?)serializer.Deserialize(reader);
            Logger.Instance.Log4.Info("Settings: Loaded settings from " + settingsFile);
            outcome = SettingsLoadOutcome.Loaded;
        }
        catch (FileNotFoundException) {
            // First time through, so create file with defaults
            Logger.Instance.Log4.Info($"Settings: Creating settings file with defaults: {settingsFile}");
            settings = new AppSettings();
            outcome = SettingsLoadOutcome.CreatedDefault;
            if (!TrySave(settingsFile, settings, out Exception? saveError)) {
                error = saveError;
                errorDetail = saveError?.Message;
            }
        }
        catch (UnauthorizedAccessException e) {
            // File exists but cannot be read (ACLs). Use defaults for this run; do NOT try to
            // overwrite a file we cannot even read.
            Logger.Instance.Log4.Error(
                $"Settings: Settings file could not be loaded ({settingsFile}); using default settings for this run. {e.Message}");
            settings = new AppSettings();
            outcome = SettingsLoadOutcome.AccessDenied;
            error = e;
            errorDetail = e.Message;
        }
        catch (Exception e) when (e is XmlException || e is InvalidOperationException) {
            // Malformed XML (mid-write crash, disk error, hand-edit, or a prohibited DTD).
            // XmlSerializer wraps the XmlException in an InvalidOperationException. Use defaults
            // for this run and deliberately do NOT overwrite the corrupt file, so the user can
            // inspect/repair it.
            string detail = (e.InnerException ?? e).Message;
            Logger.Instance.Log4.Error(
                $"Settings: Settings file is corrupt or invalid ({settingsFile}); using default settings for this run. " +
                $"The file was NOT overwritten - fix or delete it to recover. {detail}");
            settings = new AppSettings();
            outcome = SettingsLoadOutcome.ParseError;
            error = e;
            errorDetail = detail;
        }
        catch (Exception e) {
            // Last resort: a settings problem must never prevent startup.
            Logger.Instance.Log4.Error(
                $"Settings: Unexpected error loading settings file ({settingsFile}); using default settings for this run. {e.Message}");
            settings = new AppSettings();
            outcome = SettingsLoadOutcome.UnexpectedError;
            error = e;
            errorDetail = e.Message;
        }

        // XmlSerializer.Deserialize contractually returns non-null here, but guard anyway (#155):
        // this method must always hand back usable settings.
        settings ??= new AppSettings();

        // Machine policy: read the registry override regardless of how (or whether) the file
        // loaded. MachinePolicy tolerates junk values (see #155).
        settings.DisableInternalCommands = MachinePolicy.GetDisableInternalCommands();

        return new SettingsLoadResult {
            Settings = settings,
            Outcome = outcome,
            Error = error,
            ErrorDetail = errorDetail,
        };
    }
}
