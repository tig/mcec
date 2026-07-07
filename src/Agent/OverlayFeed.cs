// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;

namespace MCEControl;

/// <summary>
/// The bounded buffer of recent <see cref="CommandEvent"/>s the overlay (#119) renders. It keeps at most
/// <c>maxLines</c> entries, evicting the oldest when the cap is exceeded, and <see cref="Snapshot"/>
/// returns them newest-first so the overlay lists the most recent action at the top with older actions
/// scrolling down. Entries persist until the line cap (or the screen height, in the renderer) pushes them
/// off; they no longer time out on their own.
/// </summary>
public sealed class OverlayFeed {
    private readonly int _maxLines;
    private readonly Lock _gate = new();
    private readonly LinkedList<CommandEvent> _items = new();

    public OverlayFeed(int maxLines) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);
        _maxLines = maxLines;
    }

    /// <summary>Appends an event, evicting the oldest if over the line cap.</summary>
    public void Add(CommandEvent ev) {
        ArgumentNullException.ThrowIfNull(ev);
        lock (_gate) {
            _items.AddLast(ev);
            while (_items.Count > _maxLines) {
                _items.RemoveFirst();
            }
        }
    }

    /// <summary>
    /// The buffered events, newest first, so the overlay draws the most recent action at the top and
    /// pushes older ones down.
    /// </summary>
    public IReadOnlyList<CommandEvent> Snapshot() {
        lock (_gate) {
            List<CommandEvent> list = new(_items.Count);
            for (LinkedListNode<CommandEvent>? node = _items.Last; node is not null; node = node.Previous) {
                list.Add(node.Value);
            }
            return list;
        }
    }
}
