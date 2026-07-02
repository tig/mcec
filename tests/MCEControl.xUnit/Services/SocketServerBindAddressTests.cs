//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Net;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for issue #149: the TCP/IP command server; which turns received strings into
/// keyboard/mouse/process actions with NO socket authentication (by design, trusted-network model);
/// must let the operator choose which interface it binds to instead of being hard-wired to
/// <see cref="IPAddress.Any"/> (reachable from every host on the LAN/VPN/port-forward).
///
/// The bind interface is a security control, so it is resolved through a pure seam
/// (<see cref="SocketServer.ResolveBindAddress"/>) that can be unit-tested without opening a listener:
///   - "any"/"0.0.0.0"/"*"/empty  -> IPAddress.Any (all interfaces; the long-standing default, kept for
///                                    backward compatibility)
///   - "localhost"/"loopback"      -> IPAddress.Loopback (single-machine only)
///   - a parseable IP              -> that address (IPv4 or IPv6)
///   - junk                        -> IPAddress.Loopback (fail closed to the safe interface), logged
/// The single bind-behavior test binds loopback on an ephemeral port only.
/// </summary>
public class SocketServerBindAddressTests {
    [Theory]
    [InlineData("any")]
    [InlineData("ANY")]
    [InlineData("0.0.0.0")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveBindAddress_AllInterfacesTokens_ReturnsAny(string? value) {
        Assert.Equal(IPAddress.Any, SocketServer.ResolveBindAddress(value));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LocalHost")]
    [InlineData("loopback")]
    [InlineData("LOOPBACK")]
    public void ResolveBindAddress_LoopbackTokens_ReturnsLoopback(string value) {
        Assert.Equal(IPAddress.Loopback, SocketServer.ResolveBindAddress(value));
    }

    [Fact]
    public void ResolveBindAddress_Ipv4Loopback_ReturnsLoopback() {
        Assert.Equal(IPAddress.Loopback, SocketServer.ResolveBindAddress("127.0.0.1"));
    }

    [Fact]
    public void ResolveBindAddress_Ipv6Loopback_ReturnsIpv6Loopback() {
        Assert.Equal(IPAddress.IPv6Loopback, SocketServer.ResolveBindAddress("::1"));
    }

    [Fact]
    public void ResolveBindAddress_SpecificIp_ReturnsThatAddress() {
        Assert.Equal(IPAddress.Parse("192.168.1.50"), SocketServer.ResolveBindAddress("192.168.1.50"));
    }

    [Fact]
    public void ResolveBindAddress_SpecificIp_TrimsWhitespace() {
        Assert.Equal(IPAddress.Parse("10.0.0.7"), SocketServer.ResolveBindAddress("  10.0.0.7  "));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("999.999.999.999")]
    [InlineData("localhostx")]
    [InlineData("::gg")]
    public void ResolveBindAddress_Junk_FailsClosedToLoopback(string value) {
        // A misconfigured (unparseable) bind address must not silently expose the command server on
        // all interfaces. Fail closed to loopback; the safe, single-machine interface.
        Assert.Equal(IPAddress.Loopback, SocketServer.ResolveBindAddress(value));
    }

    [Fact]
    public void Start_WithLoopbackBindAddress_BindsAndListens() {
        using SocketServer server = new();
        // Bind loopback on an ephemeral port (0) so the test never opens a LAN-reachable listener.
        server.Start(0, "loopback");
        try {
            Assert.Equal(ServiceStatus.Waiting, server.CurrentStatus);
        }
        finally {
            server.Stop();
        }
    }
}
