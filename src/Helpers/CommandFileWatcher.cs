using System;
using System.IO;
using menelabs.core;

namespace MCEControl {
    public class CommandFileWatcher : IDisposable {

#pragma warning disable IDE0052 // Remove unread private members
        private FileSystemSafeWatcher fileWatcher;
#pragma warning restore IDE0052 // Remove unread private members

        public CommandFileWatcher(string file) {
            fileWatcher = CreateFileWatcher(file);
        }

        public event EventHandler ChangedEvent;
        /// <summary>
        /// OnChangeEvent is raised whenever the CommandTable is updated due to
        /// user commands file changes
        /// </summary>
        protected virtual void OnChangedEvent() {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            var handler = ChangedEvent;

            // Event will be null if there are no subscribers
            if (handler != null) {
                handler(this, null);
            }
        }

        private FileSystemSafeWatcher CreateFileWatcher(string path) {

            // Create a new FileSystemSafeWatcher and set its properties.
            var watcher = new FileSystemSafeWatcher();
            watcher.Path = Path.GetDirectoryName(path);
            /* Watch for changes in LastAccess and LastWrite times, and 
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = Path.GetFileName(path);

            // Add event handlers.
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            // Begin watching.
            watcher.EnableRaisingEvents = true;
            Logger.Instance.Log4.Info($"Commands: Watching {watcher.Path}\\{watcher.Filter} for changes.");
            return watcher;

        }

        private void OnChanged(object source, FileSystemEventArgs e) {
            Logger.Instance.Log4.Info($"Commands:{e.FullPath} changed");
            TelemetryService.Instance.TrackEvent("Commands file change detected");
            OnChangedEvent();
        }

        private void OnRenamed(object source, RenamedEventArgs e) {
            // Specify what is done when a file is renamed.
            Logger.Instance.Log4.Info($"Commands:{e.OldFullPath} renamed to {e.FullPath}");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (fileWatcher != null) {
                        fileWatcher.Changed -= OnChanged;
                        fileWatcher.Created -= OnChanged;
                        fileWatcher.Deleted -= OnChanged;
                        fileWatcher.Renamed -= OnRenamed;
                        fileWatcher = null;
                    }
                }
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CommandTable()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Stop() {
            throw new NotImplementedException();
        }
        #endregion
    }
}
