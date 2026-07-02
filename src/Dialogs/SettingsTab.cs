// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Identifies a tab in the Settings dialog (#213). Replaces the old stringly-typed contract
/// between <c>MainWindow</c> and <c>SettingsDialog</c> ("General"/"Client"/...), where a typo
/// compiled fine and "Activity Monitor" was silently unhandled.
/// </summary>
public enum SettingsTab {
    /// <summary>The General tab (log threshold, pacing, startup options).</summary>
    General,

    /// <summary>The Client tab (TCP/IP client connection).</summary>
    Client,

    /// <summary>The Server tab (TCP/IP server and wakeup).</summary>
    Server,

    /// <summary>The Serial Server tab (COM port settings).</summary>
    Serial,

    /// <summary>The Activity Monitor tab (user-activity detection).</summary>
    ActivityMonitor,
}
