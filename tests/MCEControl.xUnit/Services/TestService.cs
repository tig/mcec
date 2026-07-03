using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

// Create a concrete implementation for testing
internal class TestService : ServiceBase
{
    public void TestSetStatus(ServiceStatus status, string msg = "")
    {
        SetStatus(status, msg);
    }

    public void TestCommandReceived(Reply reply, string command)
    {
        OnCommandReceived(reply, command);
    }

    public void TestError(string msg)
    {
        Error(msg);
    }

    public void TestError(ServiceError error)
    {
        Error(error);
    }
}
