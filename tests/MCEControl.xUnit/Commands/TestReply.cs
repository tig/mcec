using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

internal class TestReply : Reply
{
    public override void Write(string text)
    {
        // No-op for testing
    }
}
