// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>A <see cref="Reply"/> that records what a legacy (TCP/serial) transport would receive.</summary>
internal sealed class RecordingReply : Reply {
    private readonly StringBuilder _text = new();

    public string Text => _text.ToString();

    public override void Write(string text) => _text.Append(text);
}
