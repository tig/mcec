// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Globalization;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "Server" tab (#213): the TCP/IP server and wakeup settings. Mechanical
/// decomposition of the old SettingsDialog monolith — controls, pixel positions, tooltips, tab
/// order, and handler behavior are preserved as-is.
/// </summary>
public partial class ServerSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public ServerSettingsTab() {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _checkBoxEnableServer.Checked = settings.ActAsServer;
        _editServerPort.Text = settings.ServerPort.ToString(CultureInfo.InvariantCulture);
        _checkBoxEnableWakeup.Checked = settings.WakeupEnabled;
        _editWakeupServer.Text = settings.WakeupHost;
        _editWakeupPort.Text = settings.WakeupPort.ToString(CultureInfo.InvariantCulture);
        _editWakeupCommand.Text = settings.WakeupCommand;
        _editClosingCommand.Text = settings.ClosingCommand;

        _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
        _serverGroup.Enabled = _checkBoxEnableServer.Checked;

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        return IsSectionValid(_checkBoxEnableServer.Checked, _checkBoxEnableWakeup.Checked,
            _editWakeupServer.Text, _editWakeupCommand.Text, _editClosingCommand.Text,
            _editWakeupPort.Text);
    }

    /// <summary>
    /// Pure validation core (headlessly testable): when both the server and wakeup are enabled,
    /// the wakeup host/command/closing-command must be non-empty and the wakeup port text must
    /// parse to a non-zero int. Same rule the pre-#213 SettingsChanged() applied.
    /// </summary>
    internal static bool IsSectionValid(bool serverEnabled, bool wakeupEnabled, string wakeupHost,
        string wakeupCommand, string closingCommand, string wakeupPortText) {
        if (!serverEnabled || !wakeupEnabled) {
            return true;
        }

        if (!int.TryParse(wakeupPortText, out int port)) {
            port = 0;
        }

        return !(String.IsNullOrEmpty(wakeupHost) ||
                 String.IsNullOrEmpty(wakeupCommand) ||
                 String.IsNullOrEmpty(closingCommand) ||
                 (port == 0));
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckBoxEnableServerCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ActAsServer = _checkBoxEnableServer.Checked;

        _serverGroup.Enabled = _checkBoxEnableServer.Checked;
        SettingsChanged();
    }

    private void EditServerPortTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_editServerPort.Text, out int port)) {
            _settings.ServerPort = port;
        }

        SettingsChanged();
    }

    private void CheckBoxEnableWakeupCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.WakeupEnabled = _checkBoxEnableWakeup.Checked;
        _wakeupGroup.Enabled = _checkBoxEnableWakeup.Checked;
        SettingsChanged();
    }

    private void EditWakeupServerTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.WakeupHost = _editWakeupServer.Text;
        SettingsChanged();
    }

    private void EditWakeupPortTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_editWakeupPort.Text, out int port)) {
            _settings.WakeupPort = port;
        }

        SettingsChanged();
    }

    private void EditWakeupCommandTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.WakeupCommand = _editWakeupCommand.Text;
        SettingsChanged();
    }

    private void EditClosingCommandTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ClosingCommand = _editClosingCommand.Text;
        SettingsChanged();
    }
}
