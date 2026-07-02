// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Threading;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The operator safety surface for headless <c>--mcp</c> mode: a dedicated STA thread running a WinForms
/// message pump so the emergency stop (#135) and the command overlay (#119) work without the
/// <c>MainWindow</c> host. Headless MCEC still drives the INTERACTIVE desktop (SendInput / PrintWindow /
/// UIA), so there usually IS an operator watching; this gives them the same panic hotkey and command
/// narration the GUI host provides.
///
/// <para>Both features are message-loop-bound, which is why they live here and not on the protocol
/// thread (which blocks reading stdin for the process lifetime): the WH_KEYBOARD_LL hook is delivered
/// via the installing thread's message queue, and <see cref="CommandOverlayWindow"/> needs
/// paint/timer/BeginInvoke messages.</para>
///
/// <para>RE-ARM: with no menu to click, the re-arm affordance is <see cref="EmergencyStopRearmDialog"/>,
/// shown when the stop engages and again whenever the operator presses the chord while stopped
/// (<see cref="EmergencyStop.Retriggered"/>). Showing a modal here is safe: the latch refuses every
/// agent tool call while stopped, so only physical input can reach the dialog; and this thread keeps
/// pumping inside ShowDialog, so the hook and the overlay stay live behind the prompt.</para>
///
/// <para>Degrades gracefully: if the desktop refuses the hook or a window (no interactive session), each
/// piece logs a warning and is skipped; the MCP server itself is unaffected.</para>
/// </summary>
internal static class HeadlessOperatorUi {
    private static readonly object Gate = new();
    private static readonly ManualResetEventSlim Ready = new(false);
    private static Thread? _thread;
    private static ApplicationContext? _context;
    private static Control? _marshal;
    private static CommandOverlayWindow? _overlay;
    private static bool _estopArmed;
    private static bool _promptVisible; // pump-thread only

    /// <summary>
    /// TEST SEAM: replaces the modal <see cref="EmergencyStopRearmDialog"/>. Receives the stop reason;
    /// returns true to re-arm. Lets tests exercise the full trigger→prompt→re-arm flow without a
    /// visible dialog (which nothing on CI could dismiss).
    /// </summary>
    internal static Func<string, bool>? RearmPromptForTests { get; set; }

    /// <summary>True while the pump thread is hosting.</summary>
    internal static bool IsRunning {
        get {
            lock (Gate) {
                return _thread is not null;
            }
        }
    }

    /// <summary>
    /// Starts the pump thread and arms whatever the settings enable: the emergency-stop hotkey
    /// (<c>EmergencyStopEnabled</c>; the stdio transport is always a live agent front door, so there is
    /// no <c>McpServerEnabled</c> qualifier like the GUI host's) and the overlay
    /// (<c>CommandOverlayEnabled</c>). No-op when neither is enabled or when already started. Returns
    /// once arming has finished (bounded), so "serving MCP" implies "panic hotkey live".
    /// </summary>
    public static void Start(AppSettings settings) {
        ArgumentNullException.ThrowIfNull(settings);
        bool armEstop = settings.EmergencyStopEnabled;
        bool showOverlay = settings.CommandOverlayEnabled;
        if (!armEstop && !showOverlay) {
            Logger.Instance.Log4.Info("HeadlessOperatorUi: emergency stop and overlay both disabled; not starting.");
            return;
        }

        lock (Gate) {
            if (_thread is not null) {
                return;
            }
            Ready.Reset();
            string hotkeySpec = settings.EmergencyStopHotkey;
            _thread = new Thread(() => Pump(armEstop, hotkeySpec, showOverlay)) {
                Name = "MCEC-HeadlessOperatorUi",
                IsBackground = true, // must never keep the process alive past the protocol loop
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        if (!Ready.Wait(TimeSpan.FromSeconds(10))) {
            Logger.Instance.Log4.Warn("HeadlessOperatorUi: pump thread did not finish arming within 10s; continuing.");
        }
    }

    /// <summary>
    /// Stops the pump thread: exits its message loop (which also dismisses an open re-arm prompt as
    /// "leave stopped"), disarms the hotkey, and disposes the overlay. Bounded join; the thread is
    /// background so a wedged loop can never hold the process open.
    /// </summary>
    public static void Stop() {
        Thread? thread;
        lock (Gate) {
            thread = _thread;
            _thread = null;
        }
        if (thread is null) {
            return;
        }

        Ready.Wait(TimeSpan.FromSeconds(2)); // let a mid-arming thread reach its pump first
        try {
            // ExitThread must execute ON the pump thread (it ends that thread's message loop).
            Control? marshal = _marshal;
            if (marshal is not null && marshal.IsHandleCreated) {
                marshal.BeginInvoke(new Action(static () => _context?.ExitThread()));
            }
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Warn($"HeadlessOperatorUi: stopping pump thread: {ex.Message}");
        }
        if (!thread.Join(TimeSpan.FromSeconds(3))) {
            Logger.Instance.Log4.Warn("HeadlessOperatorUi: pump thread did not exit within 3s; abandoned (background thread).");
        }
    }

    private static void Pump(bool armEstop, string hotkeySpec, bool showOverlay) {
        try {
            // The BeginInvoke target for cross-thread prompt/shutdown requests; force handle creation
            // HERE so marshaled calls land on this thread.
            _marshal = new Control();
            _ = _marshal.Handle;

            if (armEstop) {
                try {
                    EmergencyStop.Start(EmergencyStopHotkey.ParseOrDefault(hotkeySpec));
                    EmergencyStop.StateChanged += OnStateChanged;
                    EmergencyStop.Retriggered += OnRetriggered;
                    _estopArmed = true;
                }
                catch (Exception ex) {
                    // Typically SetWindowsHookEx refused (no interactive desktop). The protocol still
                    // serves; actuation just has no in-process panic hotkey, as before this feature.
                    Logger.Instance.Log4.Warn($"HeadlessOperatorUi: could not arm the emergency stop: {ex.Message}");
                }
            }

            if (showOverlay) {
                try {
                    _overlay = new CommandOverlayWindow();
                    _overlay.Show();
                }
                catch (Exception ex) {
                    Logger.Instance.Log4.Warn($"HeadlessOperatorUi: could not show the command overlay: {ex.Message}");
                }
            }

            _context = new ApplicationContext();
            Ready.Set();
            Application.Run(_context);
        }
        catch (Exception ex) {
            Logger.Instance.Log4.Error($"HeadlessOperatorUi: pump thread failed: {ex.Message}");
        }
        finally {
            Ready.Set(); // a setup failure must not leave Start waiting out its full bound
            if (_estopArmed) {
                EmergencyStop.StateChanged -= OnStateChanged;
                EmergencyStop.Retriggered -= OnRetriggered;
                EmergencyStop.Stop(); // unhooks on this thread, symmetric with the arm above
                _estopArmed = false;
            }
            _overlay?.Dispose();
            _overlay = null;
            _marshal?.Dispose();
            _marshal = null;
            _context?.Dispose();
            _context = null;
        }
    }

    private static void OnStateChanged(bool stopped) {
        if (stopped) {
            RequestPrompt();
        }
    }

    private static void OnRetriggered() => RequestPrompt();

    private static void RequestPrompt() {
        // Fires on a thread-pool thread (see EmergencyStop); marshal to the pump thread.
        Control? marshal = _marshal;
        if (marshal is null || !marshal.IsHandleCreated) {
            return;
        }
        try {
            marshal.BeginInvoke(new Action(ShowPrompt));
        }
        catch (InvalidOperationException) {
            // Handle torn down between the check and the post (shutdown); the stop stays latched.
        }
    }

    private static void ShowPrompt() {
        // Pump-thread only. One prompt at a time: the chord auto-repeating while held produces a burst
        // of Retriggered dispatches that must not stack dialogs; and re-check the latch because a
        // queued request may arrive after a re-arm already happened.
        if (_promptVisible || !EmergencyStop.IsStopped) {
            return;
        }
        _promptVisible = true;
        try {
            string reason = EmergencyStop.StoppedReason ?? "operator";
            bool rearm;
            Func<string, bool>? prompt = RearmPromptForTests;
            if (prompt is not null) {
                rearm = prompt(reason);
            }
            else {
                using EmergencyStopRearmDialog dialog = new(reason, EmergencyStop.Hotkey.Display);
                rearm = dialog.ShowDialog() == DialogResult.Yes;
            }

            if (rearm) {
                EmergencyStop.Rearm();
            }
            else {
                AgentRuntime.Audit("emergency-stop",
                    $"operator chose to leave the stop engaged; press {EmergencyStop.Hotkey.Display} to reopen the re-arm prompt");
            }
        }
        finally {
            _promptVisible = false;
        }
    }
}
