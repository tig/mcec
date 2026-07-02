using System;
using System.Net.Sockets;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

public class ServiceBaseTests
{
    [Fact]
    public void Constructor_InitializesCurrentStatus()
    {
        var service = new TestService();
        Assert.Equal(ServiceStatus.Stopped, service.CurrentStatus);
    }

    [Fact]
    public void SetStatus_UpdatesCurrentStatus()
    {
        var service = new TestService();
        service.TestSetStatus(ServiceStatus.Started);
        Assert.Equal(ServiceStatus.Started, service.CurrentStatus);
    }

    [Fact]
    public void SetStatus_RaisesStatusChanged_WithStatusAndDetail()
    {
        var service = new TestService();
        bool eventFired = false;
        ServiceStatus receivedStatus = ServiceStatus.Stopped;
        string receivedDetail = "";

        service.StatusChanged += (status, detail) =>
        {
            eventFired = true;
            receivedStatus = status;
            receivedDetail = detail;
        };

        service.TestSetStatus(ServiceStatus.Connected, "127.0.0.1:5150");

        Assert.True(eventFired);
        Assert.Equal(ServiceStatus.Connected, receivedStatus);
        Assert.Equal("127.0.0.1:5150", receivedDetail);
    }

    [Fact]
    public void OnCommandReceived_RaisesCommandReceived_WithReplyAndCommand()
    {
        var service = new TestService();
        var reply = new TestReply();
        Reply? receivedReply = null;
        string receivedCommand = "";

        service.CommandReceived += (r, command) =>
        {
            receivedReply = r;
            receivedCommand = command;
        };

        service.TestCommandReceived(reply, "mute");

        Assert.Same(reply, receivedReply);
        Assert.Equal("mute", receivedCommand);
    }

    [Fact]
    public void Error_RaisesErrorOccurred_WithMessage()
    {
        var service = new TestService();
        ServiceError? received = null;

        service.ErrorOccurred += error => received = error;

        service.TestError("test error");

        Assert.NotNull(received);
        Assert.Equal("test error", received!.Message);
        Assert.Null(received.SocketError);
        Assert.Equal("test error", received.ToString());
    }

    [Fact]
    public void Error_TypedPayload_CarriesSocketErrorAndHResult()
    {
        var service = new TestService();
        ServiceError? received = null;

        service.ErrorOccurred += error => received = error;

        service.TestError(new ServiceError("boom", SocketError.NetworkDown, unchecked((int)0x80004005)));

        Assert.NotNull(received);
        Assert.Equal("boom", received!.Message);
        Assert.Equal(SocketError.NetworkDown, received.SocketError);
        Assert.Equal(unchecked((int)0x80004005), received.HResult);
    }

    [Fact]
    public void Send_ThrowsOnNullText()
    {
        var service = new TestService();
        Assert.Throws<ArgumentNullException>(() => service.Send(null!));
    }
}
