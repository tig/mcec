//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace MCEControl;
/// <summary>
/// Holds all active Commands. Uses a hash-table for lookup.
/// Is the Invoker in the Commands pattern.
///
/// THREADING (#195): each invoker owns ONE long-running dispatcher thread that is the ONLY consumer
/// of its execute queue. Producers (the legacy TCP/serial/client pipeline via
/// <c>MainWindow.ReceivedData</c>, the agent's <c>send_command</c>, activity monitoring) only ever
/// <see cref="Enqueue"/>; nothing else dequeues. The dispatcher serializes every queued command's
/// <c>Execute</c> under <see cref="AgentRuntime.InputGate"/> so queue-driven synthetic input can never
/// interleave with a <c>drag</c> gesture (the #113 invariant), applies <c>CommandPacing</c> between
/// commands, and wraps each <c>Execute</c> in try/catch so one throwing command cannot strand the
/// queue. The dispatcher starts lazily on first enqueue and is a background thread, so it never keeps
/// the process alive; <see cref="Shutdown"/> stops it deliberately (settings reload, app exit).
/// </summary>
#pragma warning disable CA1010 // Collections should implement generic interface
#pragma warning disable CA1710 // Identifiers should have correct suffix
public class CommandInvoker : Hashtable {
#pragma warning restore CA1710 // Identifiers should have correct suffix
#pragma warning restore CA1010 // Collections should implement generic interface
    // The execute queue. Producers Add; ONLY the dispatcher thread consumes (#195).
    private readonly BlockingCollection<ICommand> _executeQueue = new(new ConcurrentQueue<ICommand>());

    private readonly Lock _dispatcherGate = new();
    private Thread? _dispatcherThread;
    private volatile bool _shutdown;

    // PRODUCER-side gate making the #154 bounds check + enqueue atomic: with genuinely concurrent
    // producers (socket/serial threads, MCP workers) an unlocked check-then-act could admit N trees
    // that each saw room, overshooting MaxQueueDepth. LOCK ORDERING: held only across the depth
    // check and the queue Adds; the DISPATCHER never takes it, and nothing is acquired while holding
    // it (EnsureDispatcherStarted/_dispatcherGate happen outside), so it can never interact with
    // InputGate or _dispatcherGate.
    private readonly object _enqueueGate = new();

    /// <summary>
    /// TEST SEAM (#195): what the shutdown drop runs to release possibly-held input. A Shutdown that
    /// drops the queued tail of a command tree can sever paired input (shiftdown: ran, shiftup:
    /// dropped) and leave a modifier latched host-wide; the same hazard the emergency stop
    /// compensates for; so the drop paths invoke the SAME release the stop uses. Tests swap in a
    /// probe (the default injects real input).
    /// </summary>
    internal static Action ReleaseHeldInputOnDrop { get; set; } = EmergencyStop.ReleaseHeldInput;

    /// <summary>
    /// SECURITY (#154): Maximum number of commands the execute queue will hold. The queue is drained
    /// by the single dispatcher thread with `CommandPacing` sleeps between items, so a remote client
    /// that enqueues faster than the paced drain would otherwise grow the queue without bound
    /// (memory DoS) and build a minutes-deep actuation backlog. A command whose tree does not fit in
    /// the remaining capacity is dropped WHOLE (all-or-nothing, see `EnqueueCommand`) and logged.
    /// </summary>
    internal const int MaxQueueDepth = 200;

    /// <summary>
    /// SECURITY (#154): Maximum size of a single command's whole tree; the command itself plus all
    /// recursively embedded commands. A single received command string can otherwise amplify ~10x or
    /// more (see #145), letting one packet flood the queue. A tree over this bound is dropped WHOLE
    /// (all-or-nothing, see `EnqueueCommand`) and logged. 50 leaves generous headroom for authored
    /// macros (the largest shipped built-in, `type_into_notepad`, expands to 10) while still
    /// bounding the amplification.
    /// </summary>
    internal const int MaxEmbeddedExpansion = 50;

    /// <summary>
    /// Current number of commands waiting to be executed. Exposed for tests.
    /// </summary>
    internal int QueuedCommandCount => _executeQueue.Count;

    /// <summary>
    /// Creaates a `Commands` instance of default & built-in commands. 
    /// </summary>
    /// <returns></returns>
    private static CommandInvoker CreateBuiltIns(bool disableInternalCommands = false) {
        // Debug backstop (#204): a concrete Command subclass without a CommandRegistry entry would
        // silently ship no built-ins and be unserializable; fail loudly at startup in DEBUG builds.
        // The real gate is CommandRegistryTests, which reds the build for the same drift.
        CommandRegistry.DebugAssertComplete();

        CommandInvoker commands = [];

        // SECURITY: Note, by default `Enabled` is set to `false` for all of these.
        if (disableInternalCommands)
            return commands;

        // One explicit registry (#204): each command type's built-in prototypes come from its
        // registry entry; no assembly scan instantiating every type, no magic static discovered
        // by reflection that silently registers nothing when misdeclared.
        foreach (CommandRegistryEntry entry in CommandRegistry.Entries) {
            foreach (Command builtinCmd in entry.BuiltIns()) {
                commands.Add(builtinCmd);
            }
        }
        Logger.Instance.Log4.Info($"{commands.GetType().Name}: {commands.Count} built-in commands defined");
        return commands;
    }
    /// <summary>
    /// Creaates a `Commands` instance from a combination of an external .commands file and the built-in commands.
    /// </summary>
    /// <param name="userCommandsFile">Path to mcec.commands file.</param>
    /// <param name="currentVersion">Version of the running app; used to upgrade older .commands files.</param>
    /// <param name="disableInternalCommands">If true, internal commands will not be added to created instance.</param>
    /// <returns></returns>
    public static CommandInvoker Create(string userCommandsFile, string currentVersion, bool disableInternalCommands) {
        CommandInvoker commands = CreateBuiltIns(disableInternalCommands);

        // Load external .commands file.
        SerializedCommands serializedCmds = SerializedCommands.LoadCommands(userCommandsFile, currentVersion);
        if (serializedCmds is { commandArray: not null }) {
            foreach (Command cmd in serializedCmds.commandArray) {
                if (!string.IsNullOrWhiteSpace(cmd.Cmd)) {
                    // TELEMETRY: Mark user defined commands as such so they don't get collected
                    if (!commands.ContainsKey(cmd.Cmd)) {
                        cmd.UserDefined = true;
                    }

                    if (cmd.Enabled) {
                        // The branches differ under #if DEBUG; a ternary transform would be lossy.
                        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                        if (cmd.UserDefined) {
#if DEBUG
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: User defined command enabled: '{cmd}'");
#else
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: User defined command enabled: '****'");
#endif
                        }
                        else {
                            Logger.Instance.Log4.Debug($"{commands.GetType().Name}: Builtin command enabled: '{cmd}'");
                        }
                    }
                    commands.Add(cmd);
                }
                else {
                    Logger.Instance.Log4.Error($"{commands.GetType().Name}: Cmd name can't be blank or whitespace ({cmd})");
                }
            }
            Logger.Instance.Log4.Info($"{commands.GetType().Name}: {serializedCmds.Count} commands loaded");
        }

        // TELEMETRY: 
        // what: Collect number of user defined commands created. 
        // why: To determine how often users actaully create user commands, if at all.
        TelemetryService.Instance.TrackEvent("User Commands Created",
            properties: new Dictionary<string, string> {
                {"userCommands", $"{commands.Values.Cast<Command>().GroupBy(o => o.UserDefined)}" }
                });

        return commands;
    }

    internal void Save(string userCommandsFile) {
        Logger.Instance.Log4.Info($@"{GetType().Name}: Saving {Program.ConfigPath}mcec.commands...");
        SerializedCommands sc = new SerializedCommands();

        Command[] values = [.. Values.Cast<Command>()];

        // SECURITY (#307/#308 review): a consent-granted enable (request-command-access) is
        // process-lifetime ONLY; "nothing is written to any config file" is the promise the consent
        // dialog makes the operator. Serialize such commands as DISABLED stand-in clones so a later
        // Commands-window Save cannot quietly persist an agent's grant into mcec.commands (where it
        // would survive restart with no consent gate). The live table is untouched; the operator's
        // own hand-made enables persist as always. (A consent-granted key stays shielded for the
        // process lifetime even if the operator re-toggles it in the window; persist it by editing
        // mcec.commands directly, or restart first.)
        sc.commandArray = [.. values
            .Select(c => c.Enabled && AgentConsent.WasGrantedByConsent(c.Cmd) ? DisabledCloneForSave(c) : c)
            .OrderBy(c => c.Cmd)];

        SerializedCommands.SaveCommands(userCommandsFile, sc, System.Windows.Forms.Application.ProductVersion);
    }

    // A serialization stand-in for a consent-granted command: identical, but Enabled=false on disk.
    // Clone just carries the Reply through (null stays null; Reply is never serialized).
    private static Command DisabledCloneForSave(Command cmd) {
        Command clone = cmd.Clone(cmd.Reply!);
        clone.Enabled = false;
        return clone;
    }

    // Adds a command to the hashtable. Optionally logs. Ensures case insenstiitivy. 
    private void Add(Command cmd, bool log = false) {
        if (!string.IsNullOrEmpty(cmd.Cmd)) {
            if (this.ContainsKey(cmd.Cmd.ToLowerInvariant())) {
                this.Remove(cmd.Cmd.ToLowerInvariant());
            }
            this.Add(cmd.Cmd.ToLowerInvariant(), cmd);
            if (log) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Command added: {cmd.Cmd}");
            }
        }
        else {
            Logger.Instance.Log4.Error($"{this.GetType().Name}: Error parsing command: {cmd}");
        }
    }

    // The 'prefix:args' shape Enqueue splits (group 1 is the table key, group 2 the Args). ONE
    // compiled pattern shared with ResolveGateKey so the gate and its consumers can never drift.
    private static readonly Regex _prefixCommandPattern = new(@"(\w+:)(.+)", RegexOptions.Compiled);

    /// <summary>
    /// The loaded-table key <see cref="Enqueue"/> will gate and execute for <paramref name="cmdString"/>
    /// (#308 review): a single character rides <c>chars:</c> when that is enabled (else a same-named
    /// single-character entry when one exists, else <c>chars:</c> itself when present, so callers
    /// report the entry whose disablement actually blocks it); a <c>prefix:args</c> spelling resolves
    /// to its bare prefix entry (the table's full spellings like <c>mcec:exit</c> are never resolved
    /// by Enqueue); anything else is looked up whole. Null when nothing in the table matches.
    /// Consumers: <c>request-command-access</c> (the key a grant must enable) and <c>send_command</c>'s
    /// <c>command-disabled</c> pre-check; sharing this resolver is what keeps a grant and the gate
    /// agreeing on the same key.
    /// </summary>
    internal string? ResolveGateKey(string cmdString) {
        if (string.IsNullOrWhiteSpace(cmdString)) {
            return null;
        }
        string trimmed = cmdString.Trim();
        if (trimmed.Length == 1) {
            if (this["chars:"] is Command { Enabled: true }) {
                return "chars:"; // Enqueue's single-character SendInput path
            }
            string single = trimmed.ToLowerInvariant();
            if (this[single] is Command) {
                return single;
            }
            return this["chars:"] is Command ? "chars:" : null;
        }
        Match match = _prefixCommandPattern.Match(trimmed);
        string key = (match.Success ? match.Groups[1].Value : trimmed).ToLowerInvariant();
        return this[key] is Command ? key : null;
    }

    /// <summary>
    /// Decodes a command string and enqueues the associated Command for execution. Returns what
    /// happened (#195): <see cref="CommandEnqueueResult.Enqueued"/>, an unknown command, or a
    /// bounds/shutdown drop; so a caller with an error channel (the agent's <c>send_command</c>)
    /// can report failure instead of pretending success. The legacy TCP/serial path ignores the
    /// result (its behavior, log and continue, is unchanged).
    /// </summary>
    /// <param name="reply">Reply context</param>
    /// <param name="cmdString">The command string that was received</param>
    public CommandEnqueueResult Enqueue(Reply reply, String cmdString) {
        ArgumentNullException.ThrowIfNull(cmdString);

        string cmd;
        string args = "";

        // parse cmd and args (eg. char vs "shutdown" vs "mouse:<action>[,<parameter>,<parameter>]"
        // and "mouse:<action>[,<parameter>,<parameter>]"
        // These commands are handled internally as Cmd="cmd:" Args="<args>"
        Match match = _prefixCommandPattern.Match(cmdString);
        if (match.Success) {
            cmd = match.Groups[1].Value;
            args = match.Groups[2].Value;
        }
        else {
            cmd = cmdString;
        }
        cmd = cmd.ToLowerInvariant();

        // TODO: Implement ignoreInternalCommands?

        // #203: `this["chars:"]` is null when built-ins are disabled (DisableInternalCommands)
        // or a user .commands file omits it; pattern-match instead of casting so a missing
        // "chars:" falls through to normal unknown-command handling rather than throwing.
        if (cmdString.Length == 1 && this["chars:"] is Command { Enabled: true }) {
            // Sending a single character is equivalent to a single key press of a key on the keyboard. 
            // For example sending a will result in the A key being pressed. 
            // 1 will result in the 1 key being pressed. There is no difference between sending a and A. 
            // Use shiftdown:/shiftup: to simulate the pressing of the shift, control, alt, and windows keys.
            // Cmd is set so a drop of this command (queue full, #154) logs something identifiable.
            SendInputCommand siCmd = new SendInputCommand() { Cmd = cmdString, Vk = cmdString, Enabled = true, Reply = reply };
            return EnqueueCommand(siCmd) ? CommandEnqueueResult.Enqueued : CommandEnqueueResult.Dropped;
        }

        // See if we know about this Command - case insensitive
        if (this[cmd.ToLowerInvariant()] != null) {
            // Always create a clone for enqueing (so Reply context can be independent)
            Command clone = ((Command)this[cmd.ToLowerInvariant()]!).Clone(reply);

            // This supports commands of the form 'chars:args'; these
            // commands do not need to originate in CommandTable
            if (string.IsNullOrEmpty(clone.Args)) {
                clone.Args = args;
            }

            return EnqueueCommand(clone) ? CommandEnqueueResult.Enqueued : CommandEnqueueResult.Dropped;
        }

        Logger.Instance.Log4.Info($"{this.GetType().Name}: Unknown command: {cmdString}");
        return CommandEnqueueResult.UnknownCommand;
    }

    /// <summary>
    /// Enques a Command for execution. Recursively enques embedded commands. Returns true when the
    /// tree entered the queue, false when it was dropped whole.
    /// SECURITY (#154): enqueue is ALL-OR-NOTHING per command tree. The whole tree (this command
    /// plus all recursively embedded commands) is counted first; if it exceeds
    /// `MaxEmbeddedExpansion` or does not fit in the queue's remaining capacity (`MaxQueueDepth`),
    /// the ENTIRE tree is dropped with a warning and nothing is enqueued. A partial enqueue must
    /// never happen: it could split paired input commands (e.g. shiftdown:/shiftup:) and leave a
    /// modifier key latched host-wide. The depth check and the Adds happen atomically under the
    /// producer-side <see cref="_enqueueGate"/> (#195): concurrent producers must not each pass the
    /// check and jointly overshoot the cap. (The dispatcher draining concurrently only FREES
    /// capacity, so the check remains conservative.)
    /// </summary>
    /// <param name="cmd">Command to enqueue</param>
    internal bool EnqueueCommand(ICommand cmd) {
        int treeSize = CountCommandTree(cmd);

        if (treeSize > MaxEmbeddedExpansion) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: command expands to {treeSize} commands, over the {MaxEmbeddedExpansion} bound; whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
            return false;
        }

        lock (_enqueueGate) {
            if (_executeQueue.Count + treeSize > MaxQueueDepth) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: execute queue cannot hold {treeSize} more command(s) ({_executeQueue.Count}/{MaxQueueDepth} queued); whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
                return false;
            }

            EnqueueCommandTree(cmd);
        }
        if (_shutdown) {
            // Lost the race with Shutdown; AddToQueue already dropped/logged whatever didn't make it.
            return false;
        }
        EnsureDispatcherStarted();
        return true;
    }

    // Counts a command's whole tree: itself plus all recursively embedded commands.
    private static int CountCommandTree(ICommand cmd) {
        int count = 1;
        if (((Command)cmd).EmbeddedCommands is not { } embeddedCommands) {
            return count;
        }

        foreach (Command embedded in embeddedCommands) {
            count += CountCommandTree(embedded);
        }
        return count;
    }

    // Recursively enqueues `cmd` and its EmbeddedCommands. Only called after EnqueueCommand has
    // verified the whole tree fits both bounds (#154); never call this directly.
    private void EnqueueCommandTree(ICommand cmd) {
        AddToQueue(cmd);
        Command command = (Command)cmd;
        if (command.EmbeddedCommands is null) {
            return;
        }

        // SECURITY (#145): a disabled parent must suppress its entire embedded subtree. Embedded
        // commands are flattened into the execute queue as independent siblings and each is gated
        // only on its OWN Enabled flag, so descending into a disabled parent would let its
        // Enabled=true children run; bypassing the per-command gate that "disabled by default"
        // depends on. Stop descending unless this command is itself enabled. (An enabled parent
        // with a disabled child still stops at that child, because the recursion re-checks here.)
        if (!command.Enabled) {
            return;
        }

        foreach (Command embedded in command.EmbeddedCommands) {
            EnqueueCommandTree(embedded);
        }
    }


    /// <summary>
    /// Adds one item to the execute queue, tolerating a racing <see cref="Shutdown"/> (whose
    /// <c>CompleteAdding</c> makes further Adds throw). A drop during shutdown is logged; a dropped
    /// completion marker is signalled so no awaiter hangs.
    /// </summary>
    private void AddToQueue(ICommand cmd) {
        if (!_shutdown) {
            try {
                _executeQueue.Add(cmd);
                return;
            }
            catch (InvalidOperationException) {
                // Lost the race with Shutdown's CompleteAdding; fall through to the drop path.
            }
        }
        (cmd as CommandDispatchCompletion)?.SignalDropped();
        Logger.Instance.Log4.Warn($"{GetType().Name}: invoker is shut down; command dropped (nothing enqueued): {(cmd as Command)?.Cmd ?? cmd.GetType().Name}");
    }

    // -------------------------------------------------------------------------------------------
    // Dispatcher (#195): the single consumer of the execute queue.
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// TEST SEAM: when set (BEFORE the first enqueue), no dispatcher thread is started and tests
    /// drain the queue deterministically with <see cref="PumpQueueForTests"/>. Production never sets
    /// this; there must be exactly ONE drain path (the dispatcher); a second concurrent drain is
    /// the very bug #195 fixed.
    /// </summary>
    internal bool SuppressDispatcherForTests { get; set; }

    /// <summary>
    /// Starts the dispatcher thread on first use. Lazy (first enqueue) so an invoker that never
    /// executes anything (many test-constructed instances) doesn't spin a thread; a background
    /// thread so it can never keep the process alive after the host exits.
    /// </summary>
    private void EnsureDispatcherStarted() {
        if (SuppressDispatcherForTests || _dispatcherThread is not null) {
            return;
        }
        lock (_dispatcherGate) {
            if (_dispatcherThread is not null || _shutdown) {
                return;
            }
            _dispatcherThread = new Thread(DispatcherLoop) {
                IsBackground = true,
                Name = "MCEC-CommandDispatcher",
            };
            _dispatcherThread.Start();
        }
    }

    /// <summary>
    /// Stops the dispatcher: no further commands are accepted, and anything still queued is dropped
    /// (completion markers are signalled as dropped so no <c>send_command</c> awaiter hangs; they
    /// are failed IMMEDIATELY from here, not when the dispatcher gets around to them, so a pending
    /// awaiter never waits out a long-running in-flight command). Because a drop can sever a command
    /// tree mid-execution (shiftdown: ran, shiftup: still queued), the drop paths release held input
    /// (<see cref="ReleaseHeldInputOnDrop"/>). Called when the invoker is replaced (mcec.commands
    /// reload) and on app exit.
    /// </summary>
    /// <param name="joinTimeoutMs">
    /// When &gt; 0, waits up to this long for the dispatcher thread to finish its in-flight command
    /// and exit; exit sites pass ~2s so the current command usually completes cleanly. The thread
    /// is background either way, so it can never keep the process alive.
    /// </param>
    internal void Shutdown(int joinTimeoutMs = 0) {
        lock (_dispatcherGate) {
            if (_shutdown) {
                return;
            }
            _shutdown = true;
            _executeQueue.CompleteAdding();
        }

        // Fail every pending completion marker NOW (#195 review): the dispatcher may be mid-Execute
        // of a long command; awaiters must not wait behind it. The dispatcher's own later
        // SignalDropped/SignalExecuted on the same marker is a no-op (TrySetResult).
        foreach (ICommand icmd in _executeQueue.ToArray()) {
            (icmd as CommandDispatchCompletion)?.SignalDropped();
        }

        if (_dispatcherThread is { } dispatcher) {
            if (joinTimeoutMs > 0 && dispatcher != Thread.CurrentThread) {
                dispatcher.Join(joinTimeoutMs);
            }
        }
        else {
            // No dispatcher ever started (nothing was enqueued, or a test suppressed it): drain any
            // leftovers here so nothing is orphaned.
            DropRemainingQueue("invoker shut down");
        }
    }

    private void DispatcherLoop() {
        Logger.Instance.Log4.Debug($"{GetType().Name}: dispatcher thread started.");
        try {
            // GetConsumingEnumerable blocks until an item arrives and ends after Shutdown's
            // CompleteAdding once the queue is empty (DispatchOne drops rather than executes
            // anything consumed after shutdown).
            foreach (ICommand icmd in _executeQueue.GetConsumingEnumerable()) {
                DispatchOne(icmd);
            }
        }
        catch (Exception e) {
            // Should be unreachable (DispatchOne catches per-command faults); but a dead dispatcher
            // silently stranding the queue would be worse than a loud log.
            Logger.Instance.Log4.Error($"{GetType().Name}: dispatcher thread terminated unexpectedly: {e}");
        }
        Logger.Instance.Log4.Debug($"{GetType().Name}: dispatcher thread exited.");
    }

    /// <summary>
    /// Executes one dequeued item: the emergency-stop drain, completion-marker signalling, the
    /// per-command try/catch, the <see cref="AgentRuntime.InputGate"/> serialization, and pacing.
    /// Factored out of <see cref="DispatcherLoop"/> so <see cref="PumpQueueForTests"/> exercises the
    /// EXACT production logic rather than a parallel implementation.
    /// </summary>
    private void DispatchOne(ICommand icmd) {
        // Shutdown drop: discard this item and drain the remainder in one pass. Dropping can sever a
        // command tree whose head already executed (shiftdown: ran, shiftup: dropped), so when any
        // real command is discarded, release held input; the same compensation the emergency stop
        // performs (see ReleaseHeldInputOnDrop).
        if (_shutdown) {
            int dropped = 0;
            if (icmd is CommandDispatchCompletion first) {
                first.SignalDropped();
            }
            else {
                dropped = 1;
            }
            while (_executeQueue.TryTake(out ICommand? leftover)) {
                if (leftover is CommandDispatchCompletion completion) {
                    completion.SignalDropped();
                }
                else {
                    dropped++;
                }
            }
            if (dropped > 0) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: invoker shut down; dropped {dropped} queued command(s) without executing; releasing held input in case a command tree was severed.");
                ReleaseHeldInputOnDrop();
            }
            return;
        }

        // Emergency stop (#135): if the operator engaged the panic hotkey, drop the rest of the queue
        // instead of actuating it. A paced/embedded command sequence (a macro, or commands after a
        // `pause`) must not keep firing after the stop; checking the latch BETWEEN commands is what
        // makes "the queue is dropped" true rather than only latching future tool calls. Pending
        // send_command completions are signalled as dropped so their awaiters fail fast instead of
        // timing out.
        if (AgentRuntime.EmergencyStopped) {
            int dropped = 0;
            if (icmd is CommandDispatchCompletion first) {
                first.SignalDropped();
            }
            else {
                dropped = 1; // the command we just dequeued and are NOT running
            }
            while (_executeQueue.TryTake(out ICommand? leftover)) {
                if (leftover is CommandDispatchCompletion completion) {
                    completion.SignalDropped();
                }
                else {
                    dropped++;
                }
            }
            if (dropped > 0) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: emergency stop engaged; dropped {dropped} queued command(s) without executing.");
            }
            return;
        }

        if (icmd is CommandDispatchCompletion marker) {
            // Everything enqueued ahead of the marker has executed; wake its awaiter. No pacing;
            // it is bookkeeping, not actuation.
            marker.SignalExecuted();
            return;
        }

        // #113/#195: a queue-driven command that can synthesize physical input executes under the
        // input gate so it can never interleave with a drag gesture actuating on an MCP worker.
        // Commands that provably touch no input (Command.SynthesizesInput == false, e.g. pause)
        // run outside the gate; a pause:60000 must not starve a concurrent drag for a minute.
        // InputGate is a leaf lock; Execute must not wait on the dispatcher/queue.
        Command command = (Command)icmd;
        if (command.SynthesizesInput) {
            lock (AgentRuntime.InputGate) {
                ExecuteIsolated(command);
            }
        }
        else {
            ExecuteIsolated(command);
        }

        // Read pacing via the UI-agnostic AgentRuntime seam so the engine works headless
        // (--mcp) where there is no MainWindow. In GUI mode this is the same settings object.
        Thread.Sleep(AgentRuntime.Settings?.CommandPacing ?? 0);
    }

    // Per-command isolation (#195): a throwing command must not strand the rest of the queue (the
    // old drain aborted on a throw, leaving leftovers to fire at a surprising later time).
    private void ExecuteIsolated(Command command) {
        try {
            command.Execute();
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: command '{command.Cmd}' threw during Execute: {e}");
        }
    }

    /// <summary>
    /// Enqueues a completion marker and returns its task: it completes <c>true</c> once the
    /// dispatcher has executed everything enqueued ahead of it, or <c>false</c> if the queue was
    /// dropped first (emergency stop / shutdown). The marker bypasses the #154 bounds; it is
    /// bookkeeping (one per tracked enqueue, bounded by the caller's concurrency), and dropping it
    /// would hang its awaiter.
    /// </summary>
    internal Task<bool> SignalWhenQueueDrained() {
        CommandDispatchCompletion completion = new();
        AddToQueue(completion);
        EnsureDispatcherStarted();
        return completion.Task;
    }

    /// <summary>
    /// The agent <c>send_command</c> entry point (#195): decodes and enqueues <paramref name="cmdString"/>
    /// exactly like <see cref="Enqueue"/>. When the tree entered the queue, <paramref name="completion"/>
    /// is a task that completes only after the dispatcher has executed it (so the caller can read the
    /// command's <see cref="Reply"/> output without racing the execution; the pre-#195 bug);
    /// <c>false</c> from that task means the queue was dropped before the command ran (emergency
    /// stop / shutdown). When nothing was enqueued (unknown command, bounds drop), the returned
    /// result says why and <paramref name="completion"/> is null; the caller reports the failure
    /// instead of pretending success.
    /// </summary>
    internal CommandEnqueueResult TryEnqueueWithCompletion(Reply reply, string cmdString, out Task<bool>? completion) {
        CommandEnqueueResult result = Enqueue(reply, cmdString);
        completion = result == CommandEnqueueResult.Enqueued ? SignalWhenQueueDrained() : null;
        return result;
    }

    /// <summary>
    /// TEST SEAM: synchronously drains whatever is queued, running the production
    /// <see cref="DispatchOne"/> logic on the calling thread. Only valid while no dispatcher thread
    /// exists (use <see cref="SuppressDispatcherForTests"/> before the first enqueue); beside a
    /// live dispatcher this would be a second concurrent drain, the exact #195 hazard, so it throws.
    /// </summary>
    internal void PumpQueueForTests() {
        if (_dispatcherThread is not null) {
            throw new InvalidOperationException("PumpQueueForTests requires that no dispatcher thread was started (set SuppressDispatcherForTests before the first enqueue); a second drain beside the live dispatcher is the #195 bug.");
        }
        while (_executeQueue.TryTake(out ICommand? icmd)) {
            DispatchOne(icmd);
        }
    }

    // Shutdown-drop helper for when no dispatcher thread exists to consume leftovers. Mirrors
    // DispatchOne's shutdown branch: dropping queued commands can sever a partially executed tree,
    // so any real drop also releases held input.
    private void DropRemainingQueue(string reason) {
        int dropped = 0;
        while (_executeQueue.TryTake(out ICommand? leftover)) {
            if (leftover is CommandDispatchCompletion completion) {
                completion.SignalDropped();
            }
            else {
                dropped++;
            }
        }
        if (dropped > 0) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: {reason}; dropped {dropped} queued command(s) without executing; releasing held input in case a command tree was severed.");
            ReleaseHeldInputOnDrop();
        }
    }
}
