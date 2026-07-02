// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// The outcome of a capture: the PNG bytes plus the diagnostics observation hardening (#90) needs to
/// avoid returning silent bad images; whether the on-screen-blit fallback was used (which returns
/// black for composited/occluded surfaces) and the blank-frame <see cref="ImageStats"/>.
/// </summary>
public sealed record CaptureResult(byte[] Png, int Width, int Height, bool UsedFallback, ImageStats Stats);
