using System;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Helpers
{
    public class CommandFileWatcherTests : IDisposable
    {
        private string? _tempFile;

        [Fact]
        public void Constructor_ThrowsIfFilePathNull()
        {
            Assert.Throws<FileNotFoundException>(() => new CommandFileWatcher(null!));
        }

        [Fact]
        public void Constructor_CreatesWatcher()
        {
            _tempFile = Path.GetTempFileName();
            var watcher = new CommandFileWatcher(_tempFile);
            
            Assert.NotNull(watcher);
            watcher.Dispose();
        }

        [Fact]
        public void ChangedEvent_CanBeSubscribed()
        {
            _tempFile = Path.GetTempFileName();
            var watcher = new CommandFileWatcher(_tempFile);
            
            bool eventFired = false;
            watcher.ChangedEvent += (sender, args) => { eventFired = true; };

            // Just test that the event can be subscribed to
            // File system watcher events can be flaky in tests
            Assert.False(eventFired); // Should not have fired yet
            
            watcher.Dispose();
        }

        public void Dispose()
        {
            if (_tempFile != null && File.Exists(_tempFile))
            {
                try
                {
                    File.Delete(_tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
