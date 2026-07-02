//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
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
using System.Reflection;
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
    private readonly BlockingCollection<ICommand> executeQueue = new(new ConcurrentQueue<ICommand>());

    private readonly object dispatcherGate = new();
    private Thread? dispatcherThread;
    private volatile bool shutdown;

    /// <summary>
    /// SECURITY (#154): Maximum number of commands the execute queue will hold. The queue is drained
    /// by the single dispatcher thread with `CommandPacing` sleeps between items, so a remote client
    /// that enqueues faster than the paced drain would otherwise grow the queue without bound
    /// (memory DoS) and build a minutes-deep actuation backlog. A command whose tree does not fit in
    /// the remaining capacity is dropped WHOLE (all-or-nothing, see `EnqueueCommand`) and logged.
    /// </summary>
    internal const int MaxQueueDepth = 200;

    /// <summary>
    /// SECURITY (#154): Maximum size of a single command's whole tree — the command itself plus all
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
    internal int QueuedCommandCount => executeQueue.Count;

    /// <summary>
    /// Creaates a `Commands` instance of default & built-in commands. 
    /// </summary>
    /// <returns></returns>
    private static CommandInvoker CreateBuiltIns(bool disableInternalCommands = false) {
        CommandInvoker commands = [];

        // Add the built-ins that are defiend in the `Command`-derived classes
        // SECURITY: Note, by default `Enabled` is set to `false` for all of these.
        if (disableInternalCommands)
            return commands;

        // Add the built-ins defined in the Command-derived classes
        foreach (Command cmdType in Command.GetDerivedClassesCollection()) {
            PropertyInfo? propertyInfo = cmdType.GetType().GetProperty("BuiltInCommands", BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null) {
                // Use the PropertyInfo to retrieve the value from the type by not passing in an instance
                foreach (Command builtinCmd in (List<Command>)propertyInfo.GetValue(null, null)!) {
                    commands.Add(builtinCmd);
                }
            }
        }
        Logger.Instance.Log4.Info($"{commands.GetType().Name}: {commands.Count} built-in commands defined");
        return commands;
    }
    /// <summary>
    /// Creaates a `Commands` instance from a combination of an external .commands file and the built-in commands.
    /// </summary>
    /// <param name="userCommandsFile">Path to mcec.commands file.</param>
    /// <param name="disableInternalCommands">If true, internal commands will not be added to created instance.</param>
    /// <returns></returns>
    public static CommandInvoker Create(string userCommandsFile, string currentVersion, bool disableInternalCommands) {
        CommandInvoker commands = null!;
        SerializedCommands serializedCmds;

        commands = CreateBuiltIns(disableInternalCommands);
        int nBuiltIn = commands.Count;

        // Load external .commands file. 
        serializedCmds = SerializedCommands.LoadCommands(userCommandsFile, currentVersion);
        if (serializedCmds != null && serializedCmds.commandArray != null) {
            foreach (Command cmd in serializedCmds.commandArray) {
                if (!string.IsNullOrWhiteSpace(cmd.Cmd)) {
                    // TELEMETRY: Mark user defined commands as such so they don't get collected
                    if (!commands.ContainsKey(cmd.Cmd)) {
                        cmd.UserDefined = true;
                    }

                    if (cmd.Enabled) {
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

        // Sort 
        sc.commandArray = [.. values.OrderBy(c => c.Cmd)];

        SerializedCommands.SaveCommands(userCommandsFile, sc, System.Windows.Forms.Application.ProductVersion);
    }

    // Adds a command to the hashtable. Optionally logs. Ensures case insenstiitivy. 
    private void Add(Command cmd, bool log = false) {
        if (!string.IsNullOrEmpty(cmd.Cmd)) {
            if (this.ContainsKey(cmd.Cmd.ToLowerInvariant())) {
                this.Remove(cmd.Cmd.ToLowerInvariant());
            }
            this.Add(cmd.Cmd.ToLowerInvariant(), (ICommand)cmd);
            if (log) {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Command added: {cmd.Cmd}");
            }
        }
        else {
            Logger.Instance.Log4.Error($"{this.GetType().Name}: Error parsing command: {cmd}");
        }
    }

    /// <summary>
    /// Decodes a commands tring and enqueues the associated Command for execution.
    /// </summary>
    /// <param name="reply">Reply context</param>
    /// <param name="cmdString">The command string that was received</param>
    public void Enqueue(Reply reply, String cmdString) {
        if (cmdString == null) {
            throw new ArgumentNullException(nameof(cmdString));
        }

        string cmd;
        string args = "";

        // parse cmd and args (eg. char vs "shutdown" vs "mouse:<action>[,<parameter>,<parameter>]"
        // and "mouse:<action>[,<parameter>,<parameter>]"
        // These commands are handled internally as Cmd="cmd:" Args="<args>"
        Match match = Regex.Match(cmdString, @"(\w+:)(.+)");
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
        // or a user .commands file omits it — pattern-match instead of casting so a missing
        // "chars:" falls through to normal unknown-command handling rather than throwing.
        if (cmdString.Length == 1 && this["chars:"] is Command charsCommand && charsCommand.Enabled) {
            // Sending a single character is equivalent to a single key press of a key on the keyboard. 
            // For example sending a will result in the A key being pressed. 
            // 1 will result in the 1 key being pressed. There is no difference between sending a and A. 
            // Use shiftdown:/shiftup: to simulate the pressing of the shift, control, alt, and windows keys.
            // Cmd is set so a drop of this command (queue full, #154) logs something identifiable.
            SendInputCommand siCmd = new SendInputCommand() { Cmd = cmdString, Vk = cmdString, Enabled = true, Reply = reply };
            EnqueueCommand(siCmd);
        }
        else {
            // See if we know about this Command - case insensitive
            if (this[cmd.ToLowerInvariant()] != null) {
                // Always create a clone for enqueing (so Reply context can be independent)
                Command clone = (Command)((Command)this[cmd.ToLowerInvariant()]!).Clone(reply);

                // This supports commands of the form 'chars:args'; these
                // commands do not need to originate in CommandTable
                if (string.IsNullOrEmpty(clone.Args)) {
                    clone.Args = args;
                }

                EnqueueCommand(clone);
            }
            else {
                Logger.Instance.Log4.Info($"{this.GetType().Name}: Unknown command: {cmdString}");
            }
        }
    }

    /// <summary>
    /// Enques a Command for execution. Recursively enques embedded commands.
    /// SECURITY (#154): enqueue is ALL-OR-NOTHING per command tree. The whole tree (this command
    /// plus all recursively embedded commands) is counted first; if it exceeds
    /// `MaxEmbeddedExpansion` or does not fit in the queue's remaining capacity (`MaxQueueDepth`),
    /// the ENTIRE tree is dropped with a warning and nothing is enqueued. A partial enqueue must
    /// never happen: it could split paired input commands (e.g. shiftdown:/shiftup:) and leave a
    /// modifier key latched host-wide.
    /// </summary>
    /// <param name="cmd">Command to enqueue</param>
    internal void EnqueueCommand(ICommand cmd) {
        int treeSize = CountCommandTree(cmd);

        if (treeSize > MaxEmbeddedExpansion) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: command expands to {treeSize} commands, over the {MaxEmbeddedExpansion} bound; whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
            return;
        }

        if (executeQueue.Count + treeSize > MaxQueueDepth) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: execute queue cannot hold {treeSize} more command(s) ({executeQueue.Count}/{MaxQueueDepth} queued); whole command dropped (nothing enqueued): {((Command)cmd).Cmd}");
            return;
        }

        EnqueueCommandTree(cmd);
        EnsureDispatcherStarted();
    }

    // Counts a command's whole tree: itself plus all recursively embedded commands.
    private static int CountCommandTree(ICommand cmd) {
        int count = 1;
        if (((Command)cmd).EmbeddedCommands is null) {
            return count;
        }

        foreach (Command embedded in ((Command)cmd).EmbeddedCommands) {
            count += CountCommandTree(embedded);
        }
        return count;
    }

    // Recursively enqueues `cmd` and its EmbeddedCommands. Only called after EnqueueCommand has
    // verified the whole tree fits both bounds (#154) — never call this directly.
    private void EnqueueCommandTree(ICommand cmd) {
        AddToQueue(cmd);
        Command command = (Command)cmd;
        if (command.EmbeddedCommands is null) {
            return;
        }

        // SECURITY (#145): a disabled parent must suppress its entire embedded subtree. Embedded
        // commands are flattened into the execute queue as independent siblings and each is gated
        // only on its OWN Enabled flag, so descending into a disabled parent would let its
        // Enabled=true children run — bypassing the per-command gate that "disabled by default"
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
        if (!shutdown) {
            try {
                executeQueue.Add(cmd);
                return;
            }
            catch (InvalidOperationException) {
                // Lost the race with Shutdown's CompleteAdding — fall through to the drop path.
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
    /// this — there must be exactly ONE drain path (the dispatcher); a second concurrent drain is
    /// the very bug #195 fixed.
    /// </summary>
    internal bool SuppressDispatcherForTests { get; set; }

    /// <summary>
    /// Starts the dispatcher thread on first use. Lazy (first enqueue) so the many transiently
    /// constructed invokers (settings dialogs, tests) don't each spin a thread; a background thread
    /// so it can never keep the process alive after the host exits.
    /// </summary>
    private void EnsureDispatcherStarted() {
        if (SuppressDispatcherForTests || dispatcherThread is not null) {
            return;
        }
        lock (dispatcherGate) {
            if (dispatcherThread is not null || shutdown) {
                return;
            }
            dispatcherThread = new Thread(DispatcherLoop) {
                IsBackground = true,
                Name = "MCEC-CommandDispatcher",
            };
            dispatcherThread.Start();
        }
    }

    /// <summary>
    /// Stops the dispatcher: no further commands are accepted, and anything still queued is dropped
    /// (completion markers are signalled as dropped so no <c>send_command</c> awaiter hangs). Called
    /// when the invoker is replaced (mcec.commands reload) and on app exit. Does NOT join the thread —
    /// a command mid-<c>Execute</c> (e.g. a long <c>pause</c>) finishes on the background thread,
    /// which cannot keep the process alive.
    /// </summary>
    internal void Shutdown() {
        lock (dispatcherGate) {
            if (shutdown) {
                return;
            }
            shutdown = true;
            executeQueue.CompleteAdding();
        }
        // If no dispatcher ever started (nothing was enqueued, or a test suppressed it), drain any
        // leftovers here so completion markers can't be orphaned.
        if (dispatcherThread is null) {
            DropRemainingQueue("invoker shut down");
        }
    }

    private void DispatcherLoop() {
        Logger.Instance.Log4.Debug($"{GetType().Name}: dispatcher thread started.");
        try {
            // GetConsumingEnumerable blocks until an item arrives and ends after Shutdown's
            // CompleteAdding once the queue is empty (DispatchOne drops rather than executes
            // anything consumed after shutdown).
            foreach (ICommand icmd in executeQueue.GetConsumingEnumerable()) {
                DispatchOne(icmd);
            }
        }
        catch (Exception e) {
            // Should be unreachable (DispatchOne catches per-command faults) — but a dead dispatcher
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
        if (shutdown) {
            (icmd as CommandDispatchCompletion)?.SignalDropped();
            if (icmd is not CommandDispatchCompletion) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: invoker shut down — dropped queued command without executing: {(icmd as Command)?.Cmd}");
            }
            return;
        }

        // Emergency stop (#135): if the operator engaged the panic hotkey, drop the rest of the queue
        // instead of actuating it. A paced/embedded command sequence (a macro, or commands after a
        // `pause`) must not keep firing after the stop — checking the latch BETWEEN commands is what
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
            while (executeQueue.TryTake(out ICommand? leftover)) {
                if (leftover is CommandDispatchCompletion completion) {
                    completion.SignalDropped();
                }
                else {
                    dropped++;
                }
            }
            if (dropped > 0) {
                Logger.Instance.Log4.Warn($"{GetType().Name}: emergency stop engaged — dropped {dropped} queued command(s) without executing.");
            }
            return;
        }

        if (icmd is CommandDispatchCompletion marker) {
            // Everything enqueued ahead of the marker has executed; wake its awaiter. No pacing —
            // it is bookkeeping, not actuation.
            marker.SignalExecuted();
            return;
        }

        // #113/#195: queue-driven commands synthesize physical input; holding the input gate for
        // each Execute means they can never interleave with a drag gesture actuating on an MCP
        // worker. InputGate is a leaf lock — Execute must not wait on the dispatcher/queue.
        lock (AgentRuntime.InputGate) {
            try {
                ((Command)icmd).Execute();
            }
            catch (Exception e) {
                // Per-command isolation (#195): a throwing command must not strand the rest of the
                // queue (the old drain aborted here, leaving leftovers to fire at a surprising later
                // time). Log and keep dispatching.
                Logger.Instance.Log4.Error($"{GetType().Name}: command '{(icmd as Command)?.Cmd}' threw during Execute: {e}");
            }
        }

        // Read pacing via the UI-agnostic AgentRuntime seam so the engine works headless
        // (--mcp) where there is no MainWindow. In GUI mode this is the same settings object.
        Thread.Sleep(AgentRuntime.Settings?.CommandPacing ?? 0);
    }

    /// <summary>
    /// Enqueues a completion marker and returns its task: it completes <c>true</c> once the
    /// dispatcher has executed everything enqueued ahead of it, or <c>false</c> if the queue was
    /// dropped first (emergency stop / shutdown). The marker bypasses the #154 bounds — it is
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
    /// exactly like <see cref="Enqueue"/>, then returns a task that completes only after the
    /// dispatcher has executed it (so the caller can read the command's <see cref="Reply"/> output
    /// without racing the execution — the pre-#195 bug). <c>false</c> means the queue was dropped
    /// before the command ran (emergency stop / shutdown).
    /// </summary>
    internal Task<bool> EnqueueWithCompletion(Reply reply, string cmdString) {
        Enqueue(reply, cmdString);
        return SignalWhenQueueDrained();
    }

    /// <summary>
    /// TEST SEAM: synchronously drains whatever is queued, running the production
    /// <see cref="DispatchOne"/> logic on the calling thread. Only valid with
    /// <see cref="SuppressDispatcherForTests"/> set — with a live dispatcher this would be a second
    /// concurrent drain, the exact #195 hazard, so it throws instead.
    /// </summary>
    internal void PumpQueueForTests() {
        if (!SuppressDispatcherForTests && dispatcherThread is not null) {
            throw new InvalidOperationException("PumpQueueForTests requires SuppressDispatcherForTests — a second drain beside the live dispatcher is the #195 bug.");
        }
        while (executeQueue.TryTake(out ICommand? icmd)) {
            DispatchOne(icmd);
        }
    }

    // Emergency-drop helper for Shutdown when no dispatcher thread exists to consume leftovers.
    private void DropRemainingQueue(string reason) {
        int dropped = 0;
        while (executeQueue.TryTake(out ICommand? leftover)) {
            if (leftover is CommandDispatchCompletion completion) {
                completion.SignalDropped();
            }
            else {
                dropped++;
            }
        }
        if (dropped > 0) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: {reason} — dropped {dropped} queued command(s) without executing.");
        }
    }
}
