// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "Activity Monitor" tab (#213): user-activity detection options.
/// Mechanical decomposition of the old SettingsDialog monolith; controls, pixel positions,
/// tooltips, tab order, and handler behavior are preserved as-is. Now that tab selection is a
/// <see cref="SettingsTab"/> enum, this tab can also be a default tab (the old stringly
/// contract never handled "Activity Monitor").
/// </summary>
public partial class ActivityMonitorSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public ActivityMonitorSettingsTab() {
        // The old Activity Monitor TabPage used UseVisualStyleBackColor (unlike the other four
        // tabs, which are SystemColors.Window). A transparent back color lets the hosting
        // TabPage's visual-style background show through, preserving that look.
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _groupBoxActivityMonitor.Enabled = _checkBoxEnableActivityMonitor.Checked = settings.ActivityMonitorEnabled;
        _unlockDetection.Checked = settings.UnlockDetection;
        _inputDetection.Checked = settings.InputDetection;
        _presenceDetection.Checked = settings.UserPresenceDetection;
        _textBoxActivityCommand.Text = settings.ActivityMonitorCommand;
        _textBoxDebounceTime.Text = $"{settings.ActivityMonitorDebounceTime}";

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        return IsSectionValid(_checkBoxEnableActivityMonitor.Checked, _textBoxActivityCommand.Text,
            _textBoxDebounceTime.Text);
    }

    /// <summary>
    /// Pure validation core (headlessly testable): when the activity monitor is enabled, the
    /// command must be non-empty and the debounce text must parse to a non-zero int. Same rule
    /// the pre-#213 SettingsChanged() applied.
    /// </summary>
    internal static bool IsSectionValid(bool monitorEnabled, string command, string debounceText) {
        if (!monitorEnabled) {
            return true;
        }

        if (!int.TryParse(debounceText, out int t)) {
            t = 0;
        }

        return !(String.IsNullOrEmpty(command) ||
                 String.IsNullOrEmpty(debounceText) ||
                 (t == 0));
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void checkBoxEnableActivityMonitor_CheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ActivityMonitorEnabled = _checkBoxEnableActivityMonitor.Checked;
        _groupBoxActivityMonitor.Enabled = _checkBoxEnableActivityMonitor.Checked;
        SettingsChanged();
    }

    private void textBoxActivityCommand_TextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (_textBoxActivityCommand.Text.Length > 0) {
            _settings.ActivityMonitorCommand = _textBoxActivityCommand.Text;
        }

        SettingsChanged();
    }

    private void textBoxDebounceTime_TextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_textBoxDebounceTime.Text, out int t)) {
            _settings.ActivityMonitorDebounceTime = t;
        }

        SettingsChanged();
    }

    private void inputDetectionRadio_CheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.InputDetection = _inputDetection.Checked;
        SettingsChanged();
    }

    private void unlockDetectionRadio_CheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.UnlockDetection = _unlockDetection.Checked;
        SettingsChanged();
    }

    private void presenceDetectionRadio_CheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.UserPresenceDetection = _presenceDetection.Checked;
        SettingsChanged();
    }
}
