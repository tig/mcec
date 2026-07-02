// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Threading;
using Timer = System.Threading.Timer;

namespace MCEControl;

/// <summary>
/// Watches a single file (the <c>.commands</c> file) for changes and raises
/// <see cref="ChangedEvent"/> once the writes settle. A bare <see cref="FileSystemWatcher"/> fires a
/// burst of Changed notifications for one logical save (editors write content, truncate, and flush
/// metadata separately), so the raw events are collapsed with a trailing-edge debounce: every raw
/// event re-arms a one-shot timer, and <see cref="ChangedEvent"/> fires only after
/// <see cref="DefaultDebounceMilliseconds"/> of quiet. This replaced the vendored menelabs
/// <c>FileSystemSafeWatcher</c>/<c>DelayedEvent</c> (#214); ~470 lines of self-described untested
/// <c>ArrayList</c> queueing whose only consumer was this class watching one file for Changed.
/// </summary>
public class CommandFileWatcher : IDisposable {
    /// <summary>
    /// The default quiet period, in milliseconds, after the last raw change notification before
    /// <see cref="ChangedEvent"/> fires (matches the old FileSystemSafeWatcher consolidation interval).
    /// </summary>
    internal const int DefaultDebounceMilliseconds = 1000;

    private readonly FileSystemWatcher _fileWatcher;
    private readonly Timer _debounceTimer;
    private readonly int _debounceMilliseconds;
    private string? _lastChangedPath;
    private volatile bool _disposed;

    public CommandFileWatcher(string file) : this(file, DefaultDebounceMilliseconds) {
    }

    /// <summary>
    /// Test seam (InternalsVisibleTo MCEControl.xUnit): lets tests shrink the debounce window so the
    /// "burst of writes → exactly one event" behavior is verifiable quickly.
    /// </summary>
    internal CommandFileWatcher(string file, int debounceMilliseconds) {
        _debounceMilliseconds = debounceMilliseconds;
        // Created disarmed; OnRawChanged arms it.
        _debounceTimer = new Timer(DebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Property-assignment order matters for compatibility: with a null/invalid path the Path
        // setter no-ops and EnableRaisingEvents throws FileNotFoundException (pinned by tests).
        _fileWatcher = new FileSystemWatcher();
        _fileWatcher.Path = Path.GetDirectoryName(file)!;
        _fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        _fileWatcher.Filter = Path.GetFileName(file);
        _fileWatcher.Changed += OnRawChanged;
        _fileWatcher.Error += OnWatcherError;
        _fileWatcher.EnableRaisingEvents = true;
        Logger.Instance.Log4.Info($"{GetType().Name}: Watching {_fileWatcher.Path}\\{_fileWatcher.Filter} for changes");
    }

    /// <summary>
    /// Raised (on a thread-pool thread) after the watched file changed and the writes have settled.
    /// </summary>
    public event EventHandler? ChangedEvent;

    /// <summary>
    /// Raises <see cref="ChangedEvent"/>. Called once per settled burst of file changes.
    /// </summary>
    protected virtual void OnChangedEvent() {
        ChangedEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A raw FileSystemWatcher notification: (re-)arm the one-shot debounce timer. A burst of raw
    /// events keeps pushing the deadline out; the timer only elapses once the file has been quiet
    /// for the full debounce window (trailing edge).
    /// </summary>
    private void OnRawChanged(object sender, FileSystemEventArgs e) {
        _lastChangedPath = e.FullPath;
        _debounceTimer.Change(_debounceMilliseconds, Timeout.Infinite);
    }

    /// <summary>
    /// The watcher's internal buffer overflowed or it otherwise failed; log it; the next successful
    /// notification still re-arms the debounce, so a lost intermediate event is harmless (the reload
    /// re-reads the whole file anyway).
    /// </summary>
    private void OnWatcherError(object sender, ErrorEventArgs e) {
        Logger.Instance.Log4.Error($"{GetType().Name}: Error watching {_fileWatcher.Path}\\{_fileWatcher.Filter}: {e.GetException()?.Message}");
    }

    private void DebounceElapsed(object? state) {
        if (_disposed) {
            return;
        }
        Logger.Instance.Log4.Info($"{GetType().Name}: {_lastChangedPath} changed");
        TelemetryService.Instance.TrackEvent("Commands file change detected");
        OnChangedEvent();
    }

    internal void Stop() {
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Changed -= OnRawChanged;
        _fileWatcher.Error -= OnWatcherError;
        // Cancel any pending debounce fire.
        _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Logger.Instance.Log4.Info($"{GetType().Name}: Stopped watching {_fileWatcher.Path}\\{_fileWatcher.Filter} for changes");
    }

    #region IDisposable Support

    protected virtual void Dispose(bool disposing) {
        if (_disposed) {
            return;
        }
        if (disposing) {
            Stop();
            _disposed = true;
            _fileWatcher.Dispose();
            _debounceTimer.Dispose();
        }
        _disposed = true;
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
