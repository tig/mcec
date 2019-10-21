//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Windows.Forms;
using MCEControl.Properties;
using Microsoft.Win32.Security;
using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;

namespace MCEControl {
    // Logger is a singleton that implements logging to both an external (file based) log (via log4net)
    // and the MainWindow log viewer (read only edit box).
    //
    // It does this by using the log4 as the centerpiece, logging everything. Then
    // only "Info" level items are shown in the log viewer (by default).
    //
    public class Logger {
        private static readonly Lazy<Logger> lazy = new Lazy<Logger>(() => new Logger());
        public static Logger Instance { get { return lazy.Value; } }

        // reference to MainWindow._log
        private ILog log4;
        public ILog Log4 {
            get {
                return log4;
            }
            set {
                log4 = value;
            }
        }

        public TextBox LogTextBox {
            get {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    TextBoxAppender a = (TextBoxAppender)hierarchy.Root.GetAppender("TextBox");
                    return a.LogTextBox;
                }
                else return null;
            }
            set {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    TextBoxAppender a = (TextBoxAppender)hierarchy.Root.GetAppender("TextBox");
                    a.LogTextBox = value;
                    a.ActivateOptions();
                }
            }
        }

        public Level TextBoxThreshold {
            get {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    TextBoxAppender a = (TextBoxAppender)hierarchy.Root.GetAppender("TextBox");
                    return a.Threshold;
                }
                else return Level.Debug;
            }
            set {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    TextBoxAppender a = (TextBoxAppender)hierarchy.Root.GetAppender("TextBox");
                    if (a != null) {
                        a.Threshold = value;
                        a.ActivateOptions();
                    }
                }
            }
        }

        // Setting the logFile location resets log4net
        public string LogFile {
            get {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    RollingFileAppender a = (RollingFileAppender)hierarchy.Root.GetAppender("File");
                    return a.File;
                }
                else return "MCEController.log"; // default
            }
            set {
                if (log4 != null) {
                    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                    RollingFileAppender a = (RollingFileAppender)hierarchy.Root.GetAppender("File");
                    a.File = value;
                    a.ActivateOptions();
                    log4.Info($"Logger: Logging to {value}");
                }
            }
        }

#pragma warning disable IDE0044 // Add readonly modifier
        private PatternLayout patternLayout = new PatternLayout();
#pragma warning restore IDE0044 // Add readonly modifier
        private Logger() {
            // Pattern
            patternLayout.ConversionPattern = "%date %-5level - %message%newline";
            patternLayout.ActivateOptions();

            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            // Log to file
            RollingFileAppender roller = new RollingFileAppender {
                Name = "File",
                AppendToFile = true,
                Layout = patternLayout,
                MaxSizeRollBackups = 5,
                MaximumFileSize = "100KB",
                RollingStyle = RollingFileAppender.RollingMode.Size,
                StaticLogFileName = true,
                File = LogFile
            };
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            // log to LogTextBox
            TextBoxAppender textbox = new TextBoxAppender {
                Name = "TextBox",
                Layout = patternLayout,
                LogTextBox = LogTextBox,
                Threshold = TextBoxThreshold
            };
            textbox.ActivateOptions();
            hierarchy.Root.AddAppender(textbox);

            // Log to console
            var debugAppender = new ConsoleAppender {
                Name = "Console",
                Layout = patternLayout
            };
            debugAppender.ActivateOptions();
            hierarchy.Root.AddAppender(debugAppender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
            Log4 = log4net.LogManager.GetLogger("MCEControl");
        }
    }

    public class TextBoxAppender : AppenderSkeleton {
        private readonly object lockObj = new object();
        private TextBox logTextBox;
        public TextBox LogTextBox {
            get => logTextBox;
            set {
                logTextBox = value;
                if (value == null) return;
                value.TextChanged += new System.EventHandler(this.LogTextChanged);
                value.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.LogKeyPress);
                var frm = value.FindForm();
                if (frm != null)
                    frm.FormClosed += delegate { Close(); };
            }
        }

        public new void Close() {
            try {
                // This locking is required to avoid null reference exceptions
                // in situations where DoAppend() is writing to the TextBox while
                // Close() is nulling out the TextBox.
                lock (lockObj) {
                    logTextBox = null;
                }

                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.RemoveAppender(this);
            }
            catch {
                // swallowing any errors
            }
        }

        protected override void Append(LoggingEvent loggingEvent) {
            try {

                //Debug.Assert(LogTextBox != null);
                if (LogTextBox == null)
                    return;

                lock (lockObj) {
                    // Can only update the log in the main window when on the UI thread
                    if (LogTextBox.InvokeRequired)  // (Instance.InvokeRequired || logTextBox.InvokeRequired)
                        LogTextBox.BeginInvoke((Action)(() => { LogTextBox.AppendText(RenderLoggingEvent(loggingEvent)); }));
                    else {
                        LogTextBox.AppendText(RenderLoggingEvent(loggingEvent));
                    }
                }
            }
            catch {
                // swallow any errors
            }
        }

        // Prevent input into the edit box
        private void LogKeyPress(object sender, KeyPressEventArgs e) {
            e.Handled = true;
        }

        // Keep the end of the log visible and prevent it from overflowing
        private void LogTextChanged(object sender, EventArgs e) {
            Debug.Assert(LogTextBox != null);
            // We don't want to overrun the size a textbox can handle 
            // limit to 64k
            if (LogTextBox.TextLength > (64 * 1024)) {
                LogTextBox.Text = LogTextBox.Text.Remove(0, LogTextBox.Text.IndexOf("\r\n", StringComparison.Ordinal) + 2);
                LogTextBox.Select(LogTextBox.TextLength, 0);
            }
            LogTextBox.ScrollToCaret();
        }
    }
}
