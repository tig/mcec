// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MCEControl.Dialogs;

/// <summary>
/// A copyable crash dialog (#295). The old handler used <see cref="MessageBox"/> with only
/// <see cref="Exception.Message"/> (and a verbatim interpolated string, so it rendered a literal
/// <c>\n</c> instead of line breaks), which gave a user nothing to paste into a bug report. This shows
/// the FULL detail (app version, OS, exception type, message, stack trace, and inner-exception chain) in a
/// read-only monospace box with <b>Copy details</b> and <b>Report issue…</b> buttons, so a user can file a
/// good issue in two clicks. Built entirely in code (no Designer) so it is one self-contained type.
/// </summary>
public sealed class ExceptionDialog : Form {
    private ExceptionDialog(string headline, string details, string logFile) {
        Text = Application.ProductName;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowIcon = false;
        ClientSize = new Size(720, 460);
        MinimumSize = new Size(500, 340);

        TableLayoutPanel root = new() {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // single full-width column
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // headline
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // details box (takes remaining height)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // log path
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // buttons

        Label headlineLabel = new() {
            Text = headline,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font(Font, FontStyle.Bold),
        };

        TextBox detailsBox = new() {
            Text = details,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill, // fills its Percent-100 row/cell
            Font = new Font("Consolas", 9F),
            BackColor = SystemColors.Window,
        };
        detailsBox.Select(0, 0);

        Label logLabel = new() {
            Text = $"Log file: {logFile}",
            AutoSize = true,
            AutoEllipsis = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 4),
            ForeColor = SystemColors.GrayText,
        };

        Button closeButton = new() { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        Button copyButton = new() { Text = "Copy details", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        Button reportButton = new() { Text = "Report issue…", AutoSize = true, Margin = new Padding(0) };
        copyButton.Click += (_, _) => {
            try {
                Clipboard.SetText(details);
            }
            catch (Exception ex) {
                // Clipboard can be transiently locked by another process; the text stays selectable in
                // the box for a manual copy, so a failed programmatic copy is not worth surfacing.
                Logger.Instance.Log4.Warn($"ExceptionDialog: copy to clipboard failed: {ex.Message}");
            }
        };
        reportButton.Click += (_, _) => Program.LaunchExternal("https://github.com/tig/mcec/issues/new");

        // AutoSize + right anchor keeps the button group hugging the right edge without a fragile Dock in
        // an auto-size cell; RightToLeft flow puts Close first (rightmost), then Copy, then Report.
        FlowLayoutPanel buttons = new() {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(copyButton);
        buttons.Controls.Add(reportButton);

        root.Controls.Add(headlineLabel, 0, 0);
        root.Controls.Add(detailsBox, 0, 1);
        root.Controls.Add(logLabel, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    /// <summary>
    /// Shows the crash dialog modally, with <paramref name="headline"/> above the copyable detail built
    /// from <paramref name="ex"/>.
    /// <para>A fatal exception can originate on a background/ThreadPool (MTA) thread, in which case
    /// <c>CurrentDomain.UnhandledException</c> runs here on that MTA thread. The <b>Copy details</b>
    /// button's <see cref="Clipboard.SetText(string)"/> throws outside STA (CR #297), so the report is
    /// always shown on a dedicated STA thread when the caller is not already STA. That also avoids
    /// depending on the UI thread, which may be the very thread that died.</para>
    /// </summary>
    public static void Show(string headline, Exception ex) {
        string details = BuildDetails(ex);
        string logFile = Logger.Instance.LogFile;
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
            ShowCore(headline, details, logFile);
            return;
        }
        Thread sta = new(() => ShowCore(headline, details, logFile)) { IsBackground = false, Name = "MCEC-crash-dialog" };
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join(); // block the crashing thread until the user dismisses the report, like MessageBox.Show
    }

    /// <summary>Builds and shows the dialog (assumes an STA thread). Falls back to a message box if the
    /// dialog itself cannot be shown, so a crash never becomes silent.</summary>
    private static void ShowCore(string headline, string details, string logFile) {
        try {
            using ExceptionDialog dialog = new(headline, details, logFile);
            dialog.ShowDialog();
        }
        catch (Exception dialogEx) {
            Logger.Instance.Log4.Error($"ExceptionDialog: could not show crash dialog: {dialogEx.Message}");
            MessageBox.Show(details, Application.ProductName);
        }
    }

    /// <summary>
    /// Builds the full, copyable crash report: app version and environment header, then the exception's
    /// own <see cref="Exception.ToString"/> (type, message, stack trace, and the full inner-exception
    /// chain). Kept separate and internal so it is unit-testable without a window.
    /// </summary>
    internal static string BuildDetails(Exception ex) {
        ArgumentNullException.ThrowIfNull(ex);
        string nl = Environment.NewLine;
        return $"MCEC {Application.ProductVersion}{nl}" +
               $"OS: {Environment.OSVersion} | .NET {Environment.Version} | {(Environment.Is64BitProcess ? "x64" : "x86")}{nl}{nl}" +
               ex;
    }
}
