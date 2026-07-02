// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Net;
using System.Net.Sockets;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for the loopback-only bind enforcement (#152). <c>McpBindAddress</c> used to be interpolated
/// straight into the HttpListener prefix, so a config typo like <c>+</c>, <c>*</c>, or <c>0.0.0.0</c>
/// bound the unauthenticated (#143) MCP endpoint to every interface. These prove the validation seam
/// (<see cref="AgentServer.IsLoopbackBindAddress"/>) accepts only <c>localhost</c> and literal loopback
/// IPs, and that <see cref="AgentServer.StartHttp"/> refuses to open a listener at all for anything else.
/// NOTE: no test here ever actually binds a non-loopback address — the point is that the listener never
/// starts.
/// </summary>
[Collection("AgentSerial")]
public class AgentServerBindAddressTests {
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")] // hostnames are case-insensitive
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")] // any 127/8 literal is loopback
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    [InlineData("[::1]")] // bracketed IPv6 literal, as it appears inside a URL
    [InlineData(" 127.0.0.1 ")] // stray whitespace from hand-edited XML settings
    public void IsLoopbackBindAddress_LoopbackValues_Accepted(string address) {
        Assert.True(AgentServer.IsLoopbackBindAddress(address));
    }

    [Theory]
    [InlineData("+")] // HttpListener wildcard: all interfaces
    [InlineData("*")] // HttpListener weak wildcard: all interfaces
    [InlineData("0.0.0.0")] // IPv4 any
    [InlineData("::")] // IPv6 any
    [InlineData("[::]")]
    [InlineData("192.168.1.5")] // non-loopback literal IP
    [InlineData("10.0.0.1")]
    [InlineData("evil.example.com")] // hostnames other than localhost are never resolved — could point anywhere
    [InlineData("localhost.attacker.com")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsLoopbackBindAddress_NonLoopbackValues_Rejected(string? address) {
        Assert.False(AgentServer.IsLoopbackBindAddress(address));
    }

    /// <summary>Asks the OS for a free loopback TCP port by binding to port 0 and reading the assignment.</summary>
    private static int FindFreeLoopbackPort() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        try {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally {
            listener.Stop();
        }
    }

    [Fact]
    public void StartHttp_NonLoopbackBindAddress_DoesNotOpenAListener() {
        // A settings file with McpBindAddress=0.0.0.0 must NOT start the HTTP transport at all —
        // not on all interfaces, and not "fixed up" to loopback either. Proof: after StartHttp,
        // nothing accepts a connection on the port, even from loopback.
        int port = FindFreeLoopbackPort();
        AgentRuntime.Settings = new AppSettings { McpBindAddress = "0.0.0.0", McpHttpPort = port };
        try {
            AgentServer.StartHttp();

            using TcpClient probe = new();
            SocketException refused = Assert.Throws<SocketException>(() => probe.Connect(IPAddress.Loopback, port));
            Assert.Equal(SocketError.ConnectionRefused, refused.SocketErrorCode);
        }
        finally {
            AgentServer.StopHttp(); // no-op if (correctly) never started; cleanup if the test fails
            AgentRuntime.Settings = null;
        }
    }

    [Fact]
    public void StartHttp_LoopbackLocalhostName_StillStarts() {
        // "localhost" is the one hostname the validator accepts; the listener must still come up.
        int port = FindFreeLoopbackPort();
        AgentRuntime.Settings = new AppSettings { McpBindAddress = "localhost", McpHttpPort = port };
        try {
            AgentServer.StartHttp();

            using TcpClient probe = new();
            probe.Connect(IPAddress.Loopback, port); // throws if the listener did not start
            Assert.True(probe.Connected);
        }
        finally {
            AgentServer.StopHttp();
            AgentRuntime.Settings = null;
        }
    }
}
