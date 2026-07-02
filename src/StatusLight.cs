//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
namespace MCEControl;

/// <summary>
/// The status-strip traffic light a <see cref="ServiceStatus"/> maps to (#211). The pure
/// status→light mapping is separated from the bitmap lookup so it can be unit tested.
/// <see cref="Unchanged"/> means "leave the current light" (the client's Sleeping
/// between-reconnects state was never repainted by the old per-transport painters either).
/// </summary>
public enum StatusLight {
    Unchanged,
    Red,
    Green,
    Gray,
}
