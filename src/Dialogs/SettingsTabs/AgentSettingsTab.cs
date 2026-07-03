// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The Settings dialog's "Agent" tab (#259): the operator's surface for agent session provisioning.
/// Two jobs only: (1) the <see cref="AppSettings.AllowSessionProvisioning"/> opt-in (whether a
/// connected agent may call <c>provision-session</c> to get a fresh, disposable, isolated copy of
/// MCEC to drive), and (2) listing/deleting the provisioned instances under
/// <see cref="SessionProvisioner.SessionsRoot"/> so leftovers can be cleaned up without hunting
/// through %LOCALAPPDATA%. The toggle does NOT enable agent commands in this installed copy; those
/// stay file-only (#213); a provisioned copy enables them only inside its throwaway directory.
///
/// <para>The checkbox follows the standard Bind/clone contract (applied on OK). The session list and
/// its Delete/Delete-all actions are immediate: they operate on disk, not on the settings clone, so
/// Cancel does not undo a delete (deliberate; deleting a directory is not a setting).</para>
/// </summary>
public partial class AgentSettingsTab : UserControl, ISettingsTab {
    private AppSettings? _settings;

    public AgentSettingsTab() {
        InitializeComponent();
        // SECURITY: inside a provisioned copy the agent CAN drive this very dialog (agent commands
        // are enabled there), and it must never widen its own permissions by re-authorizing
        // provisioning (Provision writes AllowSessionProvisioning=false into every copy). Freeze the
        // checkbox when this instance is itself running from under the sessions root.
        if (IsRunningFromSessionsRoot()) {
            _checkBoxAllowProvisioning.Enabled = false;
            _toolTipAgent.SetToolTip(_checkBoxAllowProvisioning,
                "This is a provisioned instance; it cannot authorize provisioning.");
        }
    }

    /// <inheritdoc/>
    public bool IsValid { get; private set; } = true;

    /// <inheritdoc/>
    public event EventHandler? ValidityChanged;

    /// <inheritdoc/>
    public void Bind(AppSettings settings) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _checkBoxAllowProvisioning.Checked = settings.AllowSessionProvisioning;
        RefreshSessions();

        IsValid = ValidateSection();
    }

    /// <inheritdoc/>
    public bool ValidateSection() {
        // A checkbox and a list have no invalid states.
        return true;
    }

    /// <summary>
    /// Repopulates the grid from <see cref="SessionProvisioner.ListSessions"/>. Called on Bind, when
    /// the tab becomes visible, and after every delete.
    /// </summary>
    internal void RefreshSessions() {
        IReadOnlyList<ProvisionedSessionInfo> sessions = SessionProvisioner.ListSessions();
        _gridSessions.Rows.Clear();
        foreach (ProvisionedSessionInfo session in sessions) {
            _ = _gridSessions.Rows.Add(
                session.SessionId,
                SessionDisplayFormat.Age(DateTime.UtcNow - session.CreatedUtc),
                SessionDisplayFormat.Size(session.SizeBytes),
                session.IsRunning ? "Running" : "Stale",
                "Delete");
        }
        _buttonDeleteAll.Enabled = sessions.Count > 0;
        _labelNoSessions.Visible = sessions.Count == 0;
    }

    private static bool IsRunningFromSessionsRoot() {
        try {
            string root = Path.GetFullPath(SessionProvisioner.SessionsRoot)
                .TrimEnd(Path.DirectorySeparatorChar);
            string baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            return baseDir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) {
            return false;
        }
    }

    private void SettingsChanged() {
        IsValid = ValidateSection();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckAllowProvisioningCheckedChanged(object? sender, EventArgs e) {
        if (_settings is null) {
            return;
        }

        _settings.AllowSessionProvisioning = _checkBoxAllowProvisioning.Checked;
        SettingsChanged();
    }

    private void AgentSettingsTab_VisibleChanged(object? sender, EventArgs e) {
        // Refresh whenever the tab is (re)shown so the list reflects sessions an agent
        // provisioned or ended while another tab was selected.
        if (Visible && _settings is not null) {
            RefreshSessions();
        }
    }

    private void GridSessionsCellContentClick(object? sender, DataGridViewCellEventArgs e) {
        if (e.RowIndex < 0 || e.ColumnIndex != _columnDelete.Index) {
            return;
        }
        string? sessionId = _gridSessions.Rows[e.RowIndex].Cells[_columnId.Index].Value as string;
        if (string.IsNullOrEmpty(sessionId)) {
            return;
        }
        DeleteSessions([sessionId]);
    }

    private void ButtonDeleteAllClick(object? sender, EventArgs e) {
        List<string> ids = [];
        foreach (DataGridViewRow row in _gridSessions.Rows) {
            if (row.Cells[_columnId.Index].Value is string id && !string.IsNullOrEmpty(id)) {
                ids.Add(id);
            }
        }
        DeleteSessions(ids);
    }

    /// <summary>
    /// Deletes the given session directories via <see cref="SessionProvisioner.RemoveListedSession"/>
    /// (the operator-facing, path-bounded delete that matches what the list shows and what the reaper
    /// collects), then refreshes. The only failure is a running instance whose files are locked; those
    /// are skipped with a message, matching the reaper. Internal so tests can drive the delete path
    /// headlessly (InternalsVisibleTo).
    /// </summary>
    internal void DeleteSessions(IReadOnlyList<string> sessionIds) {
        List<string> skipped = [];
        foreach (string id in sessionIds) {
            if (!SessionProvisioner.RemoveListedSession(id)) {
                skipped.Add(id);
            }
        }
        RefreshSessions();
        if (skipped.Count > 0) {
            _ = MessageBox.Show(this,
                $"Could not delete: {string.Join(", ", skipped)}.\n\nA running instance's files are " +
                "locked; stop it (close its window) and try again. Stale directories are also " +
                "cleaned up automatically over time.",
                "Provisioned instances", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
