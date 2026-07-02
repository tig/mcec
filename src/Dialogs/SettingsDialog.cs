// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Settings dialog box (#213): a thin shell that hosts one <see cref="ISettingsTab"/> UserControl
/// per tab. The dialog clones the app settings, hands the clone to every tab via
/// <see cref="ISettingsTab.Bind"/>, and enables OK only while EVERY tab's section validates
/// (preserving #203's all-sections semantics — a valid Server section must not mask an invalid
/// Client section). The dialog itself never persists anything: Cancel discards the clone;
/// MainWindow adopts (ApplySettings) and persists (SaveSettings) it on OK.
/// </summary>
public partial class SettingsDialog : Form {
    /// <summary>
    /// The dialog's deep clone of the settings. The tabs mutate ONLY this object; the caller
    /// adopts it on OK.
    /// </summary>
    public AppSettings Settings { get; }

    /// <summary>
    /// Which tab to select when the dialog opens. An enum (#213) — the old stringly contract
    /// ("General"/"Client"/...) compiled typos fine and never handled "Activity Monitor".
    /// </summary>
    public SettingsTab DefaultTab { get; set; } = SettingsTab.General;

    private readonly ISettingsTab[] _tabs;

    public SettingsDialog(AppSettings settings) {
        if (settings is null) {
            throw new ArgumentNullException(nameof(settings));
        }
        //
        // Required for Windows Form Designer support
        //
        // https://www.sgrottel.de/?p=1581&lang=en
        Font = SystemFonts.DefaultFont;
        InitializeComponent();

        // Clone the settings object
        Settings = (AppSettings)settings.Clone();

        // SECURITY (#213): the MCEC 3.0 agent/safety gates — McpServerEnabled,
        // AgentCommandsEnabled, EmergencyStopEnabled/EmergencyStopHotkey, CommandOverlayEnabled,
        // and AllowSessionProvisioning — deliberately have NO tab here. They are file-only
        // (mcec.settings) by design: opening the agent front door is a considered, out-of-band
        // act by the operator, not a checkbox to be toggled (or social-engineered) mid-session,
        // and an agent driving this very dialog must never be able to widen its own permissions.
        // See docs/agent-server.md and docs/safety-emergency-stop-and-provisioning.md before
        // "helpfully" adding checkboxes for them.
        _tabs = [_tabGeneral, _tabClient, _tabServer, _tabSerial, _tabActivityMonitor];
        foreach (ISettingsTab tab in _tabs) {
            tab.Bind(Settings);
        }

        // Subscribe AFTER Bind: populating the controls fires their change handlers, and OK must
        // stay disabled until the USER changes something (the pre-#213 constructor did the same
        // by forcing _buttonOk.Enabled = false as its last statement).
        foreach (ISettingsTab tab in _tabs) {
            tab.ValidityChanged += (_, _) => SettingsChanged();
        }

        _buttonOk.Enabled = false;
    }

    private void SettingsChanged() {
        // #203: this used to `return` after validating the first enabled section, so a
        // valid Server+Wakeup masked an invalid Client (etc.). ALL sections must validate
        // before OK is enabled — now structurally, by aggregating every tab (#213).
        _buttonOk.Enabled = _tabs.All(t => t.IsValid);
    }

    private void ButtonCancelClick(object? sender, EventArgs e) {
        Close();
    }

    private void ButtonOkClick(object? sender, EventArgs e) {
        // #213: the dialog does NOT serialize to disk. MainWindow's OK path owns the ordering:
        // Stop -> ApplySettings (adopt the clone) -> SaveSettings (persist) -> Start.
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SettingsDialog_Load(object? sender, EventArgs e) {
        _tabcontrol.SelectedTab = DefaultTab switch {
            SettingsTab.Client => _tabPageClient,
            SettingsTab.Server => _tabPageServer,
            SettingsTab.Serial => _tabPageSerial,
            SettingsTab.ActivityMonitor => _tabPageActivityMonitor,
            // SettingsTab.General and anything unexpected land on the first tab.
            _ => _tabPageGeneral,
        };
    }
}
