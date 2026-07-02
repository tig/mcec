//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System;

namespace MCEControl;

/// <summary>
/// Telnet protocol helpers shared by the outbound socket paths
/// (<see cref="SocketClient"/> and <see cref="ClientReplyContext"/>).
/// </summary>
internal static class TelnetProtocol {
    /// <summary>
    /// Escapes the telnet IAC byte (0xFF) in outbound text by doubling it, per RFC 854:
    /// a literal 0xFF in the data stream must be sent as 0xFF 0xFF so the peer does not
    /// interpret it as the start of a telnet command.
    /// (#203: both call sites used <c>Replace("\0xFF", "\0xFF\0xFF")</c> — the four-char
    /// string NUL+'x'+'F'+'F', not byte 0xFF — so the escaping was dead code.)
    /// </summary>
    /// <param name="text">The text to escape. Must not be null.</param>
    /// <returns>The text with every '\xFF' character doubled.</returns>
    public static string EscapeIac(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        return text.Replace("\xFF", "\xFF\xFF", StringComparison.Ordinal);
    }
}
