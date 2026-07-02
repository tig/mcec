// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>
/// Contract each Settings dialog tab UserControl implements (#213). The dialog is a thin shell:
/// it clones the app settings, hands the clone to every tab via <see cref="Bind"/>, and enables
/// OK only while every tab's <see cref="IsValid"/> is true (preserving #203's all-sections
/// validation semantics — a valid Server section must not mask an invalid Client section).
/// </summary>
public interface ISettingsTab {
    /// <summary>
    /// The cached result of the last <see cref="ValidateSection"/> run. Kept current by the tab's own
    /// change handlers so the dialog can aggregate without re-running every validator on each
    /// keystroke of every tab.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Raised on EVERY settings change in the tab (not only when <see cref="IsValid"/> flips):
    /// the dialog enables OK on the first change that leaves all sections valid, matching the
    /// pre-#213 <c>SettingsChanged()</c> semantics where OK starts disabled until the user
    /// changes something.
    /// </summary>
    event EventHandler? ValidityChanged;

    /// <summary>
    /// Populates the tab's controls from <paramref name="settings"/> and wires the change
    /// handlers so they mutate ONLY this bound object (the dialog's clone — Cancel discards it;
    /// MainWindow adopts and persists it on OK).
    /// </summary>
    void Bind(AppSettings settings);

    /// <summary>
    /// Validates this tab's section of the settings. Reads the raw control text (not the bound
    /// clone) so an unparsable port/number is invalid even though the clone still holds the last
    /// good value. (Named ValidateSection because <c>ContainerControl.Validate()</c> already
    /// exists on UserControl.)
    /// </summary>
    bool ValidateSection();
}
