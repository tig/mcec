using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Integration;

internal class TestReply : Reply
{
    public string? LastWrittenText { get; private set; }
    public int WriteCallCount { get; private set; }

    public override void Write(string text)
    {
        LastWrittenText = text;
        WriteCallCount++;
    }
}
