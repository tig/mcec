// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text;

namespace MCEControl;

/// <summary>
/// A <see cref="Reply"/> that accumulates everything a command writes into an in-memory buffer
/// instead of sending it over a socket/serial transport. The MCP/HTTP façade uses this to run a
/// command synchronously and capture its (structured JSON) output as the tool-call result.
/// </summary>
public sealed class CapturingReply : Reply {
    private readonly StringBuilder _buffer = new();

    public override void Write(string text) => _buffer.Append(text);

    public string Captured => _buffer.ToString();
}
