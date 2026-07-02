//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Text;

namespace MCEControl;

/// <summary>
/// Accumulates received characters into commands delimited by CR, LF, or NUL, enforcing
/// <see cref="MaxCommandLength"/> so a peer that streams bytes without a delimiter cannot
/// grow the buffer without bound (issue #148 — memory-exhaustion DoS). On overflow the
/// partial command is dropped and further input is discarded until the next delimiter.
/// Used by <see cref="SerialServer"/> and <see cref="SocketClient"/>; the socket server
/// enforces the same cap in <see cref="SocketServer.ParseReceivedData"/> (it additionally
/// handles telnet negotiation and closes the offending connection).
/// </summary>
internal sealed class CommandAccumulator {
    /// <summary>
    /// Maximum accumulated length, in characters, of a single command (issue #148).
    /// MCEControl commands are short (command names plus modest arguments; typically well
    /// under 100 chars — 4 KB is generous headroom even for long "chars:" payloads) so a
    /// few KB caps per-connection memory at a harmless size while never truncating
    /// legitimate traffic.
    /// </summary>
    public const int MaxCommandLength = 4096;

    private readonly StringBuilder _sb = new();

    // True after an overflow: input is dropped until the next delimiter so the tail of an
    // oversized run can never surface as a (garbage) command.
    private bool _discarding;

    // Test seam (InternalsVisibleTo MCEControl.xUnit).
    internal int Length => _sb.Length;

    /// <summary>
    /// Processes one received character. Returns the completed command when
    /// <paramref name="c"/> is a delimiter (CR/LF/NUL) and a non-empty command has
    /// accumulated; otherwise returns null.
    /// </summary>
    /// <param name="c">The received character.</param>
    /// <param name="onOverflow">Invoked once when the accumulated length would exceed
    /// <see cref="MaxCommandLength"/>; the buffer is reset and input is discarded until
    /// the next delimiter.</param>
    public string? ProcessChar(char c, Action? onOverflow = null) {
        switch (c) {
            case '\r':
            case '\n':
            case '\0':
                if (_discarding) {
                    // End of an oversized run — recover; the next command parses normally.
                    _discarding = false;
                    return null;
                }
                if (_sb.Length == 0) {
                    return null;
                }
                string cmd = _sb.ToString();
                _sb.Clear();
                return cmd;

            default:
                if (_discarding) {
                    return null;
                }
                if (_sb.Length >= MaxCommandLength) {
                    // #148: drop the oversized partial command and notify once.
                    _sb.Clear();
                    _discarding = true;
                    onOverflow?.Invoke();
                    return null;
                }
                _sb.Append(c);
                return null;
        }
    }
}
