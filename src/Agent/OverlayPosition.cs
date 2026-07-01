// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Which side of the primary screen the on-screen command overlay (#119) docks to. Default
/// <see cref="Right"/>; <see cref="Left"/> is useful when the app being driven (or a recording) sits on
/// the right, or to keep the overlay over a left-docked window so a capture region can stay compact.
/// </summary>
public enum OverlayPosition {
    /// <summary>Dock the overlay to the right edge of the primary screen (default).</summary>
    Right,

    /// <summary>Dock the overlay to the left edge of the primary screen.</summary>
    Left,
}
