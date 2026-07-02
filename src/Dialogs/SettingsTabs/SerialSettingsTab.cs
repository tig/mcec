// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "Serial Server" tab (#213): COM port, baud rate, framing, and handshake.
/// Mechanical decomposition of the old SettingsDialog monolith; controls, pixel positions,
/// tooltips, tab order, and handler behavior are preserved as-is.
/// </summary>
public partial class SerialSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public SerialSettingsTab() {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _checkBoxEnableSerialServer.Checked = settings.ActAsSerialServer;
        _comboBoxSerialPort.SelectedItem = settings.SerialServerPortName;
        _comboBoxBaudRate.SelectedItem = $"{settings.SerialServerBaudRate}";
        _comboBoxDataBits.SelectedItem = $"{settings.SerialServerDataBits}";
        // For the enum types, we cheat and rely on knowledge of what the enum
        // values are. The combo boxes are pre-filled with in-order strings.
        _comboBoxParity.SelectedIndex = (int)settings.SerialServerParity;
        _comboBoxStopBits.SelectedIndex = (int)settings.SerialServerStopBits - 1; // None (0) is not allowed
        _comboBoxHandshake.SelectedIndex = (int)settings.SerialServerHandshake;

        _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        // The Serial Server tab has no invalid states; every setting is chosen from a
        // pre-filled drop-down list.
        return true;
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckBoxEnableSerialServerCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.ActAsSerialServer = _checkBoxEnableSerialServer.Checked;

        _serialServerGroup.Enabled = _checkBoxEnableSerialServer.Checked;
        SettingsChanged();
    }

    private void ComboBoxSerialPortSelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        // #203: this used to check _comboBoxBaudRate.SelectedItem and then dereference
        // _comboBoxSerialPort.SelectedItem; guard the control actually being read.
        if (_comboBoxSerialPort.SelectedItem != null) {
            _settings.SerialServerPortName = _comboBoxSerialPort.SelectedItem.ToString()!;
            SettingsChanged();
        }
    }

    private void ComboBoxBaudRateSelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_comboBoxBaudRate.SelectedItem!.ToString(), out int baud)) {
            _settings.SerialServerBaudRate = baud;
        }

        SettingsChanged();
    }

    private void ComboBoxParitySelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (_comboBoxParity.SelectedItem != null) {
            _settings.SerialServerParity = (Parity)_comboBoxParity.SelectedIndex;
            SettingsChanged();
        }
    }

    private void ComboBoxDataBitsSelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (int.TryParse(_comboBoxDataBits.SelectedItem!.ToString(), out int bits)) {
            _settings.SerialServerDataBits = bits;
        }

        SettingsChanged();
    }

    private void ComboBoxStopBitsSelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (_comboBoxStopBits.SelectedItem != null) {
            // Add one because None is invalid and is not included in the combo box
            _settings.SerialServerStopBits = (StopBits)_comboBoxStopBits.SelectedIndex + 1;
            SettingsChanged();
        }
    }

    private void ComboBoxHandshakeSelectedIndexChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        if (_comboBoxHandshake.SelectedItem != null) {
            _settings.SerialServerHandshake = (Handshake)_comboBoxHandshake.SelectedIndex;
            SettingsChanged();
        }
    }
}
