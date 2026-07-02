// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// A test command whose behavior is injected per-test (#195 dispatcher tests): records how many
/// times it ran and on which thread, then invokes <see cref="OnExecute"/> (which may append to an
/// order log, block on an event, throw, or write to <see cref="Command.Reply"/>). Clones carry the
/// same delegate, so instances registered in an invoker's table behave identically when Enqueue
/// clones them.
/// </summary>
internal sealed class DelegateTestCommand : Command {
    private int _executeCount;

    /// <summary>Behavior to run inside Execute; receives the executing instance (the clone).</summary>
    public Action<DelegateTestCommand>? OnExecute { get; set; }

    /// <summary>How many times Execute ran (across this instance only).</summary>
    public int ExecuteCount => _executeCount;

    /// <summary>Managed thread id Execute last ran on (0 = never ran).</summary>
    public int LastExecuteThreadId { get; private set; }

    public DelegateTestCommand() {
        Enabled = true; // enable for testing
    }

    public override ICommand Clone(Reply reply) =>
        base.Clone(reply, new DelegateTestCommand { OnExecute = OnExecute });

    public override bool Execute() {
        // Don't call base.Execute() to avoid the TelemetryService dependency.
        if (!Enabled) {
            return false;
        }
        Interlocked.Increment(ref _executeCount);
        LastExecuteThreadId = Environment.CurrentManagedThreadId;
        OnExecute?.Invoke(this);
        return true;
    }
}
