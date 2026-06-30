// DISCLAIMER: Use this code at your own risk.
// No support is provided and this code has NOT been tested.

using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Timers;
using Timer = System.Timers.Timer;

namespace menelabs.core;

/// <summary>
/// This class wraps FileSystemEventArgs and RenamedEventArgs objects and detection of duplicate events.
/// </summary>
public class DelayedEvent {
    private readonly FileSystemEventArgs _args;

    /// <summary>
    /// Only delayed events that are unique will be fired.
    /// </summary>
    private bool _delayed;


    public DelayedEvent(FileSystemEventArgs args) {
        _delayed = false;
        _args = args;
    }

    public FileSystemEventArgs Args {
        get {
            return _args;
        }
    }

    public bool Delayed {
        get {
            return _delayed;
        }
        set {
            _delayed = value;
        }
    }

#pragma warning disable CA1720 // Identifier contains type name
    public virtual bool IsDuplicate(object obj) {
#pragma warning restore CA1720 // Identifier contains type name
        DelayedEvent? delayedEvent = obj as DelayedEvent;
        if (delayedEvent == null) {
            return false; // this is not null so they are different
        }

        FileSystemEventArgs eO1 = _args;
        RenamedEventArgs? reO1 = _args as RenamedEventArgs;
        FileSystemEventArgs eO2 = delayedEvent._args;
        RenamedEventArgs? reO2 = delayedEvent._args as RenamedEventArgs;
        // The events are equal only if they are of the same type (reO1 and reO2
        // are both null or NOT NULL) and have all properties equal.
        // We also eliminate Changed events that follow recent Created events
        // because many apps create new files by creating an empty file and then
        // they update the file with the file content.
        return ((eO1 != null && eO2 != null && eO1.ChangeType == eO2.ChangeType
            && eO1.FullPath == eO2.FullPath && eO1.Name == eO2.Name) &&
            ((reO1 == null && reO2 == null) || (reO1 != null && reO2 != null &&
            reO1.OldFullPath == reO2.OldFullPath && reO1.OldName == reO2.OldName))) ||
            (eO1 != null && eO2 != null && eO1.ChangeType == WatcherChangeTypes.Created
            && eO2.ChangeType == WatcherChangeTypes.Changed
            && eO1.FullPath == eO2.FullPath && eO1.Name == eO2.Name);
    }
}
