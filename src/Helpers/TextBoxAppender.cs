//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
//
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;


namespace MCEControl;

public class TextBoxAppender : AppenderSkeleton {
    private readonly Lock _lockObj = new();
    private TextBoxExt? _logTextBox;
    // AllowNull: the Logger constructor wires the appender up before any TextBox exists, so the
    // setter accepts null (meaning "no viewer yet") while the getter contract stays non-null.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public TextBoxExt LogTextBox {
        get => _logTextBox!;
        set {
            _logTextBox = value;
            if (value == null) {
                return;
            }

            // Set max # of chars. Given logfile logging there's no need to let a machine
            // page memory if MCE Controller has been runnnig long time
            value.MaxLength = 256 * 1024;

            value.TextChanged += LogTextChanged;
            Form? frm = value.FindForm();
            if (frm != null) {
                frm.FormClosed += delegate { Close(); };
            }
        }
    }

    private new void Close() {
        try {
            // This locking is required to avoid null reference exceptions
            // in situations where DoAppend() is writing to the TextBox while
            // Close() is nulling out the TextBox.
            lock (_lockObj) {
                _logTextBox = null;
            }

            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.RemoveAppender(this);
        }
        catch {
            // swallowing any errors
        }
    }

    protected override void Append(LoggingEvent loggingEvent) {
        try {
            if (_logTextBox == null) {
                return;
            }

            lock (_lockObj) {
                // Can only update the log in the main window when on the UI thread
                if (LogTextBox.InvokeRequired) {
                    LogTextBox.BeginInvoke((Action)(() => { LogTextBox.AppendText(RenderLoggingEvent(loggingEvent)); }));
                }
                else {
                    LogTextBox.AppendText(RenderLoggingEvent(loggingEvent));
                }
            }
        }
        catch {
            // swallow any errors
        }
    }

    // Keep the end of the log visible and prevent it from overflowing
    private void LogTextChanged(object? sender, EventArgs e) {
        Debug.Assert(LogTextBox != null);
        //LogTextBox.ScrollToCaret();
    }
}
