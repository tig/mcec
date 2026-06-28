using System;
using System.Collections.Generic;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

internal class TestCommand : Command
{
    public bool ExecuteCalled { get; private set; }

    public TestCommand()
    {
        Enabled = true; // Enable for testing
    }

    public override ICommand Clone(Reply? reply)
    {
        return base.Clone(reply!, new TestCommand());
    }

    public override bool Execute()
    {
        // Don't call base.Execute() to avoid TelemetryService dependency
        if (!Enabled)
        {
            return false;
        }

        ExecuteCalled = true;
        return true;
    }
}
