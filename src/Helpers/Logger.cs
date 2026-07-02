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
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;


namespace MCEControl; 
// Logger is a singleton that implements logging to both an external (file based) log (via log4net)
// and the MainWindow log viewer (read only edit box).
//
// It does this by using the log4 as the centerpiece, logging everything. Then
// only "Info" level items are shown in the log viewer (by default).
//
public class Logger {
    private static readonly Lazy<Logger> _lazy = new(() => new Logger());
    public static Logger Instance { get { return _lazy.Value; } }

    // reference to MainWindow._log
    // Null only while the singleton constructor is still running; the property accessors below
    // guard that window. External callers always observe a non-null Log4 (set at the end of the ctor).
    private readonly ILog? _log4;
    public ILog Log4 {
        get => _log4 ?? throw new InvalidOperationException("Logger.Log4 touched while the singleton constructor is still running.");
        private init => _log4 = value;
    }

    public TextBoxExt LogTextBox {
        get {
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                TextBoxAppender? a = (TextBoxAppender?)hierarchy.Root.GetAppender("TextBox");
                return a!.LogTextBox;
            }
            else {
                return null!;
            }
        }
        set {
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                TextBoxAppender? a = (TextBoxAppender?)hierarchy.Root.GetAppender("TextBox");
                a!.LogTextBox = value;
                a.ActivateOptions();
            }
        }
    }

    public Level TextBoxThreshold {
        get {
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                TextBoxAppender? a = (TextBoxAppender?)hierarchy.Root.GetAppender("TextBox");
                return a!.Threshold;
            }
            else {
                return Level.Debug;
            }
        }
        set {
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                TextBoxAppender? a = (TextBoxAppender?)hierarchy.Root.GetAppender("TextBox");
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
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                RollingFileAppender? a = (RollingFileAppender?)hierarchy.Root.GetAppender("File");
                return a!.File!;
            }
            else {
                return "mcec.log"; // default
            }
        }
        set {
            if (_log4 != null) {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                RollingFileAppender? a = (RollingFileAppender?)hierarchy.Root.GetAppender("File");
                a!.File = value;
                a.ActivateOptions();
            }
        }
    }

    private readonly PatternLayout _patternLayout = new();
    private Logger() {
        // Pattern
        _patternLayout.ConversionPattern = "%date %-5level - %message%newline";
        _patternLayout.ActivateOptions();

        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

        // Log to file
        RollingFileAppender roller = new RollingFileAppender {
            Name = "File",
            AppendToFile = true,
            Layout = _patternLayout,
            MaxSizeRollBackups = 10,
            MaximumFileSize = "1MB",
            RollingStyle = RollingFileAppender.RollingMode.Size,
            StaticLogFileName = true,
            File = LogFile
        };
        roller.ActivateOptions();
        hierarchy.Root.AddAppender(roller);

        TextBoxAppender textbox = new TextBoxAppender {
            Name = "TextBox",
            Layout = _patternLayout,
            LogTextBox = LogTextBox,
            Threshold = TextBoxThreshold
        };
        textbox.ActivateOptions();
        hierarchy.Root.AddAppender(textbox);

        // Log to console; on STDERR, never stdout. In headless MCP mode (--mcp) stdout is reserved
        // for the JSON-RPC protocol stream, so any log line on stdout would corrupt it. Routing to
        // stderr (the conventional log channel for stdio servers) keeps stdout clean in all modes.
        ConsoleAppender debugAppender = new ConsoleAppender {
            Name = "Console",
            Target = ConsoleAppender.ConsoleError,
            Layout = _patternLayout
        };
        debugAppender.ActivateOptions();
        hierarchy.Root.AddAppender(debugAppender);

        hierarchy.Root.Level = Level.All;
        hierarchy.Configured = true;
        Log4 = LogManager.GetLogger("MCEControl");
    }
    public static void DumpException(Exception ex) {
        if (ex is null) {
            throw new ArgumentNullException(nameof(ex));
        }

        WriteExceptionInfo(ex);
    }

    private static void WriteExceptionInfo(Exception ex) {
        if (ex is null) {
            throw new ArgumentNullException(nameof(ex));
        }

        Logger.Instance.Log4.Debug("--------- Exception Data ---------");
        Logger.Instance.Log4.Debug($"Message:        {ex.FullMessage()}");
        Logger.Instance.Log4.Debug($"Exception Type: {ex.GetType().FullName}");
        Logger.Instance.Log4.Debug($"Source:         {ex.Source}");
        Logger.Instance.Log4.Debug($"StrackTrace:    {ex.StackTrace}");
        Logger.Instance.Log4.Debug($"TargetSite:     {ex.TargetSite}");
        Logger.Instance.Log4.Debug("--------- Full Exception ---------");
        Logger.Instance.Log4.Debug($"{ex}");
    }
}
