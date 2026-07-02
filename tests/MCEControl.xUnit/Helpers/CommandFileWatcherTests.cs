using System;
using System.IO;
using System.Threading;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Helpers;

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

    /// <summary>
    /// The #214 debounce contract: a burst of rapid writes to the watched file collapses to exactly
    /// ONE ChangedEvent once the writes settle (trailing edge), and a later, separate write fires
    /// another. Uses a short debounce window (test-seam ctor) to keep the test fast.
    /// </summary>
    [Fact]
    public void ChangedEvent_FiresOncePerSettledBurst() {
        string dir = Path.Combine(Path.GetTempPath(), $"mcec-watch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "mcec.commands");
        File.WriteAllText(file, "initial");

        const int debounceMs = 250;
        try {
            using ManualResetEventSlim fired = new(false);
            int count = 0;
            using (CommandFileWatcher watcher = new(file, debounceMs)) {
                watcher.ChangedEvent += (_, _) => {
                    Interlocked.Increment(ref count);
                    fired.Set();
                };

                // Several writes in quick succession; well inside one debounce window each.
                for (int i = 0; i < 5; i++) {
                    File.WriteAllText(file, $"write {i}");
                    Thread.Sleep(20);
                }

                Assert.True(fired.Wait(TimeSpan.FromSeconds(10)), "ChangedEvent did not fire after the write burst");
                // Let several further debounce windows elapse: the burst must have collapsed to ONE event.
                Thread.Sleep(debounceMs * 3);
                Assert.Equal(1, Volatile.Read(ref count));

                // A later write is a new burst: exactly one more event.
                fired.Reset();
                File.WriteAllText(file, "later write");
                Assert.True(fired.Wait(TimeSpan.FromSeconds(10)), "ChangedEvent did not fire for the later write");
                Thread.Sleep(debounceMs * 3);
                Assert.Equal(2, Volatile.Read(ref count));
            }
        }
        finally {
            try {
                Directory.Delete(dir, recursive: true);
            }
            catch {
                // Ignore cleanup errors
            }
        }
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
