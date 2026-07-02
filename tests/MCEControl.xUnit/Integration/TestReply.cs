using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Integration;

internal class TestReply : Reply
{
    public string? LastWrittenText { get; private set; }
    private int WriteCallCount { get; set; }

    public override void Write(string text)
    {
        LastWrittenText = text;
        WriteCallCount++;
    }
}
