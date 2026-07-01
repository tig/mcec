// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Linq;

namespace MCEControl;

/// <summary>
/// The bounded, self-pruning buffer of recent <see cref="CommandEvent"/>s the overlay (#119) renders.
/// It keeps at most <c>maxLines</c> entries and drops any older than <c>lifetime</c>, so the on-screen
/// feed stays small and old lines fade out on their own (no scrollbars). Time is passed in (<c>nowUtc</c>)
/// rather than read from the clock so aging is deterministic and unit-testable.
/// </summary>
public sealed class OverlayFeed {
    private readonly int _maxLines;
    private readonly TimeSpan _lifetime;
    private readonly object _gate = new();
    private readonly LinkedList<(DateTime At, CommandEvent Ev)> _items = new();

    public OverlayFeed(int maxLines, TimeSpan lifetime) {
        if (maxLines <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxLines));
        }
        if (lifetime <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
        _maxLines = maxLines;
        _lifetime = lifetime;
    }

    /// <summary>Appends an event at <paramref name="nowUtc"/>, evicting the oldest if over the line cap.</summary>
    public void Add(CommandEvent ev, DateTime nowUtc) {
        ArgumentNullException.ThrowIfNull(ev);
        lock (_gate) {
            _items.AddLast((nowUtc, ev));
            while (_items.Count > _maxLines) {
                _items.RemoveFirst();
            }
        }
    }

    /// <summary>The currently-visible events, oldest first, after pruning any older than the lifetime.</summary>
    public IReadOnlyList<CommandEvent> Visible(DateTime nowUtc) {
        lock (_gate) {
            while (_items.First is not null && nowUtc - _items.First.Value.At > _lifetime) {
                _items.RemoveFirst();
            }
            return _items.Select(e => e.Ev).ToList();
        }
    }
}
