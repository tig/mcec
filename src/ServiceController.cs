//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// Per-transport descriptor for MainWindow's service wiring (#211). One instance describes each
/// command transport (SocketServer, SocketClient, SerialServer): how to create/start/stop it,
/// which status-strip item shows its light, how to format its status log lines, and any
/// transport-specific quirks (the server's wakeup command, the client's restart-on-error).
/// MainWindow holds these in a list and iterates it from ONE generic start/stop/toggle/paint
/// path — before #211 every one of those paths existed as three near-identical copies.
/// UI glue only: the typed events live on <see cref="ServiceBase"/>.
/// </summary>
internal sealed class ServiceController {
    /// <summary>Display/log name; matches the log prefix the old per-transport handlers used
    /// (e.g. "SocketServer", "Client", "SerialServer").</summary>
    public required string Name { get; init; }

    /// <summary>Constructs the service instance (does NOT start it — handlers are wired between
    /// construction and start so no notification is missed).</summary>
    public required Func<ServiceBase> Create { get; init; }

    /// <summary>Starts the constructed instance with its transport-specific arguments. The bool
    /// is the client's "delay before first connect" restart flag; other transports ignore it.</summary>
    public required Action<ServiceBase, bool> StartTransport { get; init; }

    /// <summary>Stops the instance (each transport has its own non-virtual Stop()).</summary>
    public required Action<ServiceBase> StopTransport { get; init; }

    /// <summary>The status-strip item that shows this transport's traffic light.</summary>
    public required ToolStripStatusLabel StatusStripItem { get; init; }

    /// <summary>The status-strip caption, recomputed on every paint (it embeds live settings,
    /// e.g. "Server on port 5150").</summary>
    public required Func<string> StatusStripText { get; init; }

    /// <summary>Formats a status change as a log line (without the "<see cref="Name"/>: " prefix
    /// the generic handler adds), or null to log nothing for that status.</summary>
    public required Func<ServiceStatus, string, string?> FormatStatus { get; init; }

    /// <summary>Transport-specific reaction to a status change (e.g. the server sends the wakeup
    /// command on Started and the closing command on Stopped). Runs on the UI thread, after the
    /// paint and the log.</summary>
    public Action<ServiceStatus>? StatusQuirk { get; init; }

    /// <summary>Transport-specific reaction to an error (e.g. the client restarts itself).
    /// Runs on the UI thread, after the error is logged.</summary>
    public Action<ServiceError>? ErrorQuirk { get; init; }

    /// <summary>Runs right after a successful start (e.g. enable the "Send Awake" menu item).</summary>
    public Action? AfterStart { get; init; }

    /// <summary>Runs right after a stop (e.g. disable the "Send Awake" menu item; hide the
    /// command window — the client's long-standing side effect, kept explicit here).</summary>
    public Action? AfterStop { get; init; }

    /// <summary>Whether the operator has this transport enabled in Settings (drives both
    /// auto-start and whether a status-strip click toggles or opens Settings).</summary>
    public required Func<bool> IsConfigured { get; init; }

    /// <summary>The live service instance, or null when stopped. Owned by MainWindow's generic
    /// start/stop path.</summary>
    public ServiceBase? Instance { get; set; }

    // The exact delegate instances subscribed to Instance's events, kept so the generic stop
    // path can unsubscribe them (the old wiring unsubscribed BEFORE Stop(), which is what keeps
    // an operator-initiated stop from triggering status-change side effects like the server's
    // closing wakeup command — preserved).
    public Action<ServiceStatus, string>? StatusHandler { get; set; }
    public Action<Reply, string>? CommandHandler { get; set; }
    public Action<ServiceError>? ErrorHandler { get; set; }
}
