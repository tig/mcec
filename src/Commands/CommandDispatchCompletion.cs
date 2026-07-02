// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading.Tasks;

namespace MCEControl;

/// <summary>
/// A completion marker (#195) the <see cref="CommandInvoker"/> dispatcher recognizes in its queue.
/// Enqueued AFTER a command tree, it completes when the single dispatcher thread reaches it;
/// i.e. when everything enqueued ahead of it (the tree included) has finished executing; giving a
/// producer (<c>AgentServer.RunSendCommand</c>, tests) an awaitable "my command actually ran" signal
/// without a second drain path.
///
/// Deliberately implements <see cref="ICommand"/> directly rather than deriving from
/// <see cref="Command"/>: it is dispatcher bookkeeping, not a command; it must never appear in the
/// command table or in <see cref="CommandRegistry.Entries"/> (whose completeness test sweeps
/// concrete <see cref="Command"/> subclasses, #204), is never gated on <c>Enabled</c>, and is
/// exempt from the #154 queue bounds (one per completion-tracked enqueue, bounded by the caller's
/// own concurrency).
/// </summary>
internal sealed class CommandDispatchCompletion : ICommand {
    // RunContinuationsAsynchronously so completing on the dispatcher thread never inlines an
    // awaiter's continuation there (which could block the dispatcher on foreign code).
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes <c>true</c> when the dispatcher executed everything ahead of the marker;
    /// <c>false</c> when the queue was dropped before reaching it (emergency stop or shutdown).
    /// </summary>
    public Task<bool> Task => _tcs.Task;

    /// <summary>Dispatcher reached the marker: everything enqueued ahead of it has executed.</summary>
    internal void SignalExecuted() => _tcs.TrySetResult(true);

    /// <summary>The marker was dropped without the queue ahead of it executing (emergency stop / shutdown).</summary>
    internal void SignalDropped() => _tcs.TrySetResult(false);

    /// <summary>Never called; the dispatcher special-cases this type before casting to <see cref="Command"/>.</summary>
    bool ICommand.Execute() {
        SignalExecuted();
        return true;
    }

    /// <summary>Never called; markers are created fresh per enqueue and never live in the command table.</summary>
    ICommand ICommand.Clone(Reply reply) =>
        throw new NotSupportedException($"{nameof(CommandDispatchCompletion)} is dispatcher bookkeeping and cannot be cloned.");
}
