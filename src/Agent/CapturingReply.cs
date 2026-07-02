// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text;

namespace MCEControl;

/// <summary>
/// A <see cref="Reply"/> that accumulates everything a command writes into an in-memory buffer
/// instead of sending it over a socket/serial transport. The MCP/HTTP façade uses this to run a
/// command synchronously and capture its output as the tool-call result.
///
/// <para>Agent commands additionally hand their structured <see cref="CommandResult"/> over as an
/// OBJECT via <see cref="Result"/> (#206): <see cref="AgentCommand"/>'s sealed template sets it when
/// the reply is a <see cref="CapturingReply"/> instead of serializing to text, so the MCP server
/// consumes the result without a serialize → parse round-trip (which used to materialize a capture's
/// base64 PNG three to four times). <see cref="Captured"/> lazily serializes the object for callers
/// that still want the legacy JSON text (<c>send_command</c>'s output, tests), so the observable
/// text is unchanged.</para>
/// </summary>
public sealed class CapturingReply : Reply {
    private readonly StringBuilder _buffer = new();

    public override void Write(string text) => _buffer.Append(text);

    /// <summary>
    /// The typed result an <see cref="AgentCommand"/> produced, or null when the executed command is
    /// not an agent command (legacy commands write free text into the buffer instead).
    /// </summary>
    public CommandResult? Result { get; set; }

    /// <summary>
    /// Everything written to this reply as text. When nothing was written but a typed
    /// <see cref="Result"/> was set, serializes it on demand — same bytes the legacy TCP/serial
    /// transport would have carried.
    /// </summary>
    public string Captured => _buffer.Length > 0 ? _buffer.ToString() : Result?.ToJson() ?? "";
}
