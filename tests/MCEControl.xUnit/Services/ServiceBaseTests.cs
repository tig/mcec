using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services
{
    public class ServiceBaseTests
    {
        // Create a concrete implementation for testing
        private class TestService : ServiceBase
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
        public void SetStatus_RaisesNotificationEvent()
        {
            var service = new TestService();
            bool eventFired = false;
            ServiceStatus receivedStatus = ServiceStatus.Stopped;

            service.Notifications += (notification, status, reply, msg) =>
            {
                if (notification == ServiceNotification.StatusChange)
                {
                    eventFired = true;
                    receivedStatus = status;
                }
            };

            service.TestSetStatus(ServiceStatus.Connected);

            Assert.True(eventFired);
            Assert.Equal(ServiceStatus.Connected, receivedStatus);
        }

        [Fact]
        public void SendNotification_RaisesEvent()
        {
            var service = new TestService();
            bool eventFired = false;
            string receivedMsg = "";

            service.Notifications += (notification, status, reply, msg) =>
            {
                eventFired = true;
                receivedMsg = msg;
            };

            service.TestSendNotification(ServiceNotification.ReceivedData, ServiceStatus.Connected, null, "test data");

            Assert.True(eventFired);
            Assert.Equal("test data", receivedMsg);
        }

        [Fact]
        public void Error_SendsErrorNotification()
        {
            var service = new TestService();
            bool errorFired = false;
            string errorMsg = "";

            service.Notifications += (notification, status, reply, msg) =>
            {
                if (notification == ServiceNotification.Error)
                {
                    errorFired = true;
                    errorMsg = msg;
                }
            };

            service.TestError("test error");

            Assert.True(errorFired);
            Assert.Equal("test error", errorMsg);
        }

        [Fact]
        public void Send_ThrowsOnNullText()
        {
            var service = new TestService();
            Assert.Throws<ArgumentNullException>(() => service.Send(null!));
        }
    }
}
