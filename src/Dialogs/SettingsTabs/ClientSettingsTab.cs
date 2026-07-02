// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Globalization;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "Client" tab (#213): the TCP/IP client connection settings. Mechanical
/// decomposition of the old SettingsDialog monolith — controls, pixel positions, tooltips, tab
/// order, and handler behavior are preserved as-is.
/// </summary>
public partial class ClientSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public ClientSettingsTab() {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _checkBoxEnableClient.Checked = settings.ActAsClient;
        _editClientPort.Text = settings.ClientPort.ToString(CultureInfo.InvariantCulture);
        _editClientHost.Text = settings.ClientHost;
        _editClientDelayTime.Text = settings.ClientDelayTime.ToString(CultureInfo.InvariantCulture);

        _clientGroup.Enabled = _checkBoxEnableClient.Checked;

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        return IsSectionValid(_checkBoxEnableClient.Checked, _editClientHost.Text, _editClientPort.Text);
    }

    /// <summary>
    /// Pure validation core (headlessly testable): when the client is enabled, the host must be
    /// non-empty and the port text must parse to a non-zero int. Same rule the pre-#213
    /// SettingsChanged() applied.
    /// </summary>
    internal static bool IsSectionValid(bool clientEnabled, string host, string portText) {
        if (!clientEnabled) {
            return true;
        }

        if (!int.TryParse(portText, out int port)) {
            port = 0;
        }

        return !(String.IsNullOrEmpty(host) || (port == 0));
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckEnableClientCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ActAsClient = _checkBoxEnableClient.Checked;

        _clientGroup.Enabled = _checkBoxEnableClient.Checked;
        SettingsChanged();
    }

    private void EditClientPortTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_editClientPort.Text, out int port)) {
            _settings.ClientPort = port;
        }

        SettingsChanged();
    }

    private void EditClientHostTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ClientHost = _editClientHost.Text;
        SettingsChanged();
    }

    private void EditClientDelayTimeTextChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        // #203: Convert.ToInt32 fired on every keystroke and threw on non-digits — use
        // TryParse like the sibling handlers.
        if (int.TryParse(_editClientDelayTime.Text, out int delay)) {
            _settings.ClientDelayTime = delay;
        }

        SettingsChanged();
    }
}
