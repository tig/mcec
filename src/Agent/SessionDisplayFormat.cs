// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>
/// Compact, human-readable formatting for a provisioned session's age and size (#259). Shared by the
/// Settings dialog's Agent tab (the list) and the startup log (<see cref="Program"/>), so both render a
/// session the same way. Pure and headlessly testable (no UI dependency).
/// </summary>
public static class SessionDisplayFormat {
    /// <summary>Formats an age compactly ("just now", "5 min", "3 h", "2 d"). Negative clamps to zero.</summary>
    public static string Age(TimeSpan age) {
        if (age < TimeSpan.Zero) {
            age = TimeSpan.Zero;
        }
        if (age.TotalMinutes < 1) {
            return "just now";
        }
        if (age.TotalHours < 1) {
            return $"{(int)age.TotalMinutes} min";
        }
        if (age.TotalDays < 1) {
            return $"{(int)age.TotalHours} h";
        }
        return $"{(int)age.TotalDays} d";
    }

    /// <summary>Formats a byte count compactly ("512 B", "34 KB", "120.4 MB").</summary>
    public static string Size(long bytes) {
        if (bytes < 1024) {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024) {
            return $"{bytes / 1024} KB";
        }
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
