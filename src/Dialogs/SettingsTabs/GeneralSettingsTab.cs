// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Windows.Forms;
using log4net;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "General" tab (#213): log threshold, default command pacing, and the
/// startup checkboxes. Mechanical decomposition of the old SettingsDialog monolith; controls,
/// pixel positions, tooltips, tab order, and handler behavior are preserved as-is.
/// </summary>
public partial class GeneralSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public GeneralSettingsTab() {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _checkBoxHideOnStartup.Checked = settings.HideOnStartup;
        _checkBoxAutoStart.Checked = settings.AutoStart;

        _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository!.LevelMap["ALL"]!);
        _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository!.LevelMap["INFO"]!);
        _comboBoxLogThresholds.Items.Add(LogManager.GetLogger("MCEControl").Logger.Repository!.LevelMap["DEBUG"]!);

        switch (settings.TextBoxLogThreshold) {
            case "ALL":
                _comboBoxLogThresholds.SelectedIndex = 0;
                break;

            case "INFO":
                _comboBoxLogThresholds.SelectedIndex = 1;
                break;

            case "DEBUG":
                _comboBoxLogThresholds.SelectedIndex = 2;
                break;
        }

        _textBoxPacing.Text = $"{settings.CommandPacing}";

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        // The General tab has no invalid states; every control is a checkbox, a pre-filled
        // combo, or a numeric text box whose handler simply ignores unparsable input.
        return true;
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckBoxHideOnStartupCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.HideOnStartup = _checkBoxHideOnStartup.Checked;
        SettingsChanged();
    }

    private void CheckBoxAutoStartCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.AutoStart = _checkBoxAutoStart.Checked;
        SettingsChanged();
    }

    private void comboBoxLogThresholds_SelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        // #203: do NOT mutate the global Logger.Instance.TextBoxThreshold here; Cancel
        // could never restore it. The tab only updates its (cloned) Settings;
        // MainWindow.ApplySettings applies the threshold on the OK path.
        _settings.TextBoxLogThreshold = _comboBoxLogThresholds.SelectedItem!.ToString()!;

        SettingsChanged();
    }

    private void textBoxPacing_TextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_textBoxPacing.Text, out int t)) {
            _settings.CommandPacing = t;
        }

        SettingsChanged();
    }
}
