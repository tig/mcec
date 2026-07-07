// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;

namespace MCEControl;

/// <summary>
/// Process-wide publish/subscribe hub for <see cref="CommandEvent"/>s; the dedicated command-event
/// hook the on-screen overlay (#119) subscribes to. The execution seam (AgentServer for agent tools,
/// later <c>CommandInvoker</c> for the remote-control path) <see cref="Publish"/>es; the overlay
/// window subscribes. Static so the seam can publish without plumbing a reference through every command.
///
/// <para>Publishing is best-effort and decoupled from command execution: a throwing subscriber is
/// caught and logged so a faulty overlay can never break a command. Thread-safe because an
/// <c>invoke</c> can publish from a background worker thread.</para>
/// </summary>
public static class CommandEventHub {
    private static readonly Lock _gate = new();
    private static readonly List<Action<CommandEvent>> _subscribers = [];

    /// <summary>Registers a handler to receive every subsequently-published <see cref="CommandEvent"/>.</summary>
    public static void Subscribe(Action<CommandEvent> handler) {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate) {
            _subscribers.Add(handler);
        }
    }

    /// <summary>Removes a previously-registered handler.</summary>
    public static void Unsubscribe(Action<CommandEvent> handler) {
        lock (_gate) {
            _subscribers.Remove(handler);
        }
    }

    /// <summary>Publishes an event to all current subscribers. Never throws; a faulty subscriber is logged.</summary>
    public static void Publish(CommandEvent ev) {
        Action<CommandEvent>[] snapshot;
        lock (_gate) {
            snapshot = [.. _subscribers];
        }
        foreach (Action<CommandEvent> handler in snapshot) {
            try {
                handler(ev);
            }
            catch (Exception e) {
                Logger.Instance.Log4.Error($"CommandEventHub: subscriber threw, ignoring: {e.Message}");
            }
        }
    }
}
