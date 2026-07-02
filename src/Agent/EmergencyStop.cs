// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Gma.UserActivityMonitor;
using WindowsInput;

namespace MCEControl;

/// <summary>
/// The emergency stop (#135): a global "dead man's switch" that lets the operator instantly halt an agent
/// session from ANY focused window. It reuses MCEC's existing low-level keyboard hook
/// (<see cref="HookManager"/>); reacting to <b>physical</b> input only (injected keys are ignored) so the
/// agent can neither trip nor defeat it; and drives an <see cref="EmergencyStopDetector"/> for the chord.
///
/// <para>When the hotkey fires it: (1) <b>latches</b> the actuation gate (<see cref="AgentRuntime.EmergencyStopped"/>)
/// so no further tool call actuates until the operator explicitly re-arms; (2) aborts in-flight actuation;
/// stops any recording and signals a cooperative drag cancel; (3) releases held input (all mouse buttons up,
/// modifiers reset) so a mid-drag or chord can't leave input stuck; and (4) reports loudly to the overlay,
/// the <c>AGENT-AUDIT:</c> log, and the session record. It stays stopped until <see cref="Rearm"/>.</para>
///
/// <para>Independent of the WinForms message loop's focus; the hook fires regardless of MCEC's window
/// state (including minimized to tray). It does require a message loop on the installing thread, so it is
/// armed in the GUI host, not the headless <c>--mcp</c> process.</para>
/// </summary>
public static class EmergencyStop {
    private static readonly object Gate = new();
    private static EmergencyStopDetector? _detector;
    private static EmergencyStopHotkey _hotkey = EmergencyStopHotkey.Default;
    private static bool _armed;

    /// <summary>Raised (on the hook thread) when the stopped state changes; the overlay/UI subscribe to reflect it.</summary>
    public static event Action<bool>? StateChanged;

    /// <summary>True while the stop is engaged (mirrors <see cref="AgentRuntime.EmergencyStopped"/>).</summary>
    public static bool IsStopped => AgentRuntime.EmergencyStopped;

    /// <summary>When the current stop was engaged (UTC), or null when not stopped.</summary>
    public static DateTime? StoppedAtUtc { get; private set; }

    /// <summary>A short description of what engaged the current stop (e.g. the hotkey), or null when not stopped.</summary>
    public static string? StoppedReason { get; private set; }

    /// <summary>The chord currently armed (for display in feedback and settings).</summary>
    public static EmergencyStopHotkey Hotkey => _hotkey;

    /// <summary>
    /// Arms the global hotkey detector with <paramref name="hotkey"/>. Idempotent; re-arming with a new
    /// hotkey swaps it in place. Safe to call from the UI thread during startup.
    /// </summary>
    public static void Start(EmergencyStopHotkey hotkey) {
        ArgumentNullException.ThrowIfNull(hotkey);
        lock (Gate) {
            _hotkey = hotkey;
            _detector = new EmergencyStopDetector(hotkey);
            if (!_armed) {
                HookManager.KeyDownExt += OnKeyDown;
                HookManager.KeyUpExt += OnKeyUp;
                _armed = true;
            }
        }
        Logger.Instance.Log4.Info($"EmergencyStop: armed; operator panic hotkey is {hotkey.Display} (physical input only).");
    }

    /// <summary>Disarms the hotkey detector (on host shutdown / settings reload).</summary>
    public static void Stop() {
        lock (Gate) {
            if (!_armed) {
                return;
            }
            HookManager.KeyDownExt -= OnKeyDown;
            HookManager.KeyUpExt -= OnKeyUp;
            _armed = false;
            _detector = null;
        }
        Logger.Instance.Log4.Info("EmergencyStop: disarmed.");
    }

    private static void OnKeyDown(object? sender, GlobalKeyEventArgs e) {
        bool fire;
        lock (Gate) {
            fire = _detector?.OnKeyDown(e.KeyCode, e.Injected) ?? false;
        }
        if (fire) {
            Trigger($"hotkey {_hotkey.Display}");
        }
    }

    private static void OnKeyUp(object? sender, GlobalKeyEventArgs e) {
        lock (Gate) {
            _detector?.OnKeyUp(e.KeyCode, e.Injected);
        }
    }

    /// <summary>
    /// Engages the stop: latches the gate, aborts in-flight actuation, releases held input, and reports it.
    /// Idempotent; a held-key auto-repeat or a second trigger while already stopped is a no-op. Callable
    /// directly (e.g. from a UI panic button) as well as from the hotkey.
    /// </summary>
    public static void Trigger(string source) {
        lock (Gate) {
            if (AgentRuntime.EmergencyStopped) {
                return; // already latched; ignore repeats until re-armed
            }
            AgentRuntime.SetEmergencyStopped(true);
            StoppedAtUtc = DateTime.UtcNow;
            StoppedReason = source;
        }

        AgentRuntime.Audit("emergency-stop", $"ENGAGED by operator ({source}); halting actuation, dropping queue, releasing held input");

        // Abort in-flight actuation. Recording stops cleanly here; an in-flight drag observes the latch and
        // bails out of its move loop (see MouseCommand.PerformDrag). The invoke modal-grace worker runs a
        // synchronous UIA call we cannot safely abort mid-flight, but the latch refuses every follow-up call
        // and the input release below neutralizes anything it left held.
        try {
            if (GifRecorder.IsRecording) {
                GifRecorder.Stop();
            }
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"EmergencyStop: stopping recording failed: {ex.Message}");
        }

        ReleaseHeldInput();

        // Loud feedback: overlay (#119) STOPPED banner + session record. Audit already logged above.
        try {
            AgentSession session = AgentRuntime.Session;
            session.RecordEmergencyStop(source, StoppedAtUtc ?? DateTime.UtcNow);
            CommandEventHub.Publish(new CommandEvent("emergency-stop", "⛔ STOPPED by operator", CommandOutcome.Failed, session.SessionId));
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"EmergencyStop: publishing stop feedback failed: {ex.Message}");
        }

        StateChanged?.Invoke(true);
    }

    /// <summary>
    /// Re-arms after a stop: clears the latch so actuation is permitted again. This is the deliberate
    /// operator action the latch waits for; it is never cleared automatically. No-op when not stopped.
    /// </summary>
    public static void Rearm() {
        lock (Gate) {
            if (!AgentRuntime.EmergencyStopped) {
                return;
            }
            AgentRuntime.SetEmergencyStopped(false);
            StoppedAtUtc = null;
            StoppedReason = null;
            // Drop any physically-held modifier state so a key still down from the panic press can't
            // immediately re-fire the chord.
            _detector?.Reset();
        }

        AgentRuntime.Audit("emergency-stop", "RE-ARMED by operator; actuation re-enabled");
        try {
            AgentSession session = AgentRuntime.Session;
            session.ClearEmergencyStop();
            CommandEventHub.Publish(new CommandEvent("emergency-stop", "✔ Re-armed; agent may resume", CommandOutcome.Info, session.SessionId));
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"EmergencyStop: publishing re-arm feedback failed: {ex.Message}");
        }

        StateChanged?.Invoke(false);
    }

    /// <summary>
    /// Releases any input the agent may have left held: lifts every mouse button and resets the shift /
    /// ctrl / alt / win modifiers (the same reset <see cref="MainWindow"/> runs on exit). These are
    /// injected events, so the hook flags them <c>LLKHF_INJECTED</c> and the detector ignores them; the
    /// release can never re-trigger the stop.
    /// </summary>
    private static void ReleaseHeldInput() {
        try {
            InputSimulator sim = new();
            sim.Mouse.LeftButtonUp();
            sim.Mouse.RightButtonUp();
            sim.Mouse.MiddleButtonUp();
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"EmergencyStop: releasing mouse buttons failed: {ex.Message}");
        }

        try {
            SendInputCommand.ShiftKey("shift", false);
            SendInputCommand.ShiftKey("ctrl", false);
            SendInputCommand.ShiftKey("alt", false);
            SendInputCommand.ShiftKey("lwin", false);
            SendInputCommand.ShiftKey("rwin", false);
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"EmergencyStop: resetting modifiers failed: {ex.Message}");
        }
    }
}
