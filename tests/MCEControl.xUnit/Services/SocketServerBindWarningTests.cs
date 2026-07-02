//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Net;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Issue #149 (MAJOR-1): the unauthenticated command server keeps its long-standing all-interfaces
/// default for backward compatibility, so — unlike the MCP HTTP door, which refuses an exposed bind — it
/// must at least WARN loudly at startup when the resolved bind is non-loopback. These tests exercise the
/// warning decision against a resolved address (no listener is opened), asserting via a log4net
/// MemoryAppender exactly like the CommandInvoker queue-cap tests. Serial because the log4net hierarchy
/// and Logger are process-global.
/// </summary>
[Collection("AgentSerial")]
public class SocketServerBindWarningTests {
    private static MemoryAppender AttachLogCapture() {
        _ = Logger.Instance.Log4; // ensure the hierarchy is configured
        MemoryAppender appender = new() { Name = "SocketBindWarnTestCapture" };
        appender.ActivateOptions();
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
        hierarchy.Root.AddAppender(appender);
        return appender;
    }

    private static void DetachLogCapture(MemoryAppender appender) {
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
        hierarchy.Root.RemoveAppender(appender);
    }

    private static bool IsExposedBindWarning(LoggingEvent e) =>
        e.Level >= Level.Warn &&
        e.RenderedMessage!.Contains("SocketServer", StringComparison.Ordinal) &&
        e.RenderedMessage!.Contains("SocketServerBindAddress=127.0.0.1", StringComparison.Ordinal);

    [Theory]
    [InlineData("0.0.0.0")]   // IPAddress.Any
    [InlineData("any")]
    [InlineData("::")]        // IPAddress.IPv6Any
    [InlineData("192.168.1.50")]
    public void WarnIfBindAddressExposed_NonLoopback_LogsWarningNamingAddress(string bindAddress) {
        MemoryAppender capture = AttachLogCapture();
        try {
            IPAddress resolved = SocketServer.ResolveBindAddress(bindAddress);
            SocketServer.WarnIfBindAddressExposed(resolved);

            LoggingEvent warning = Assert.Single(capture.GetEvents(), IsExposedBindWarning);
            Assert.Contains(resolved.ToString(), warning.RenderedMessage!, StringComparison.Ordinal);
        }
        finally {
            DetachLogCapture(capture);
        }
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("loopback")]
    [InlineData("localhost")]
    [InlineData("::1")]
    public void WarnIfBindAddressExposed_Loopback_DoesNotWarn(string bindAddress) {
        MemoryAppender capture = AttachLogCapture();
        try {
            SocketServer.WarnIfBindAddressExposed(SocketServer.ResolveBindAddress(bindAddress));

            Assert.DoesNotContain(capture.GetEvents(), IsExposedBindWarning);
        }
        finally {
            DetachLogCapture(capture);
        }
    }
}
