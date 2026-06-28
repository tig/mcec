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

    public void TestSendNotification(ServiceNotification notification, ServiceStatus status, Reply? replyContext = null, string msg = "")
    {
        SendNotification(notification, status, replyContext, msg);
    }

    public void TestError(string msg)
    {
        Error(msg);
    }

    public override void Send(string text, Reply? replyContext = null)
    {
        base.Send(text, replyContext);
    }
}
