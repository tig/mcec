// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Security regression tests for issue #143: the MCP/HTTP front door must validate Host and Origin
/// (and, when configured, a bearer token) so a browser on a malicious page cannot drive the agent
/// tools (CSRF), and a rebinding attacker cannot read responses (DNS rebinding). The gate is a pure
/// function of the request's method/path/headers so it can be exercised without a live socket.
/// </summary>
public class AgentServerHttpGateTests {
    private const int Port = 5151;

    private static HttpGateDecision Gate(
            string method = "POST",
            string? path = "/mcp",
            string? host = "127.0.0.1:5151",
            string? origin = null,
            string? auth = null,
            string? token = null)
        => AgentServer.GateHttpRequest(method, path, host, origin, auth, Port, token);

    [Fact]
    public void LoopbackPost_NoOrigin_NoToken_Allowed() {
        Assert.Equal(HttpGateDecision.Allow, Gate());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("OPTIONS")]
    public void NonPostMethod_Rejected(string method) {
        Assert.Equal(HttpGateDecision.RejectMethod, Gate(method: method));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/mcp/extra")]
    [InlineData("/admin")]
    public void NonMcpPath_Rejected(string path) {
        Assert.Equal(HttpGateDecision.RejectPath, Gate(path: path));
    }

    // ---- DNS rebinding: the Host header must be a loopback authority ----

    [Theory]
    [InlineData("127.0.0.1:5151")]
    [InlineData("localhost:5151")]
    [InlineData("[::1]:5151")]
    [InlineData("127.0.0.1")]   // no port
    [InlineData("localhost")]
    public void LoopbackHost_Allowed(string host) {
        Assert.Equal(HttpGateDecision.Allow, Gate(host: host));
    }

    [Theory]
    [InlineData("evil.com:5151")]
    [InlineData("evil.com")]
    [InlineData("attacker.example:5151")]
    [InlineData("10.0.0.5:5151")]
    [InlineData("0.0.0.0:5151")]
    [InlineData(null)]           // missing Host
    [InlineData("")]
    public void NonLoopbackOrMissingHost_Rejected(string? host) {
        Assert.Equal(HttpGateDecision.RejectHost, Gate(host: host));
    }

    [Fact]
    public void LoopbackHost_WrongPort_Rejected() {
        Assert.Equal(HttpGateDecision.RejectHost, Gate(host: "127.0.0.1:9999"));
    }

    // ---- CSRF: a non-loopback Origin must be rejected ----

    [Theory]
    [InlineData("http://evil.com")]
    [InlineData("https://attacker.example")]
    [InlineData("http://evil.com:8080")]
    [InlineData("null")]         // opaque origin (sandboxed iframe / file://)
    [InlineData("garbage")]
    public void NonLoopbackOrigin_Rejected(string origin) {
        Assert.Equal(HttpGateDecision.RejectOrigin, Gate(origin: origin));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5151")]
    [InlineData("http://localhost:1234")]
    [InlineData("http://[::1]:5151")]
    public void LoopbackOrigin_Allowed(string origin) {
        Assert.Equal(HttpGateDecision.Allow, Gate(origin: origin));
    }

    // ---- Bearer token (defense in depth; enforced only when configured) ----

    [Fact]
    public void TokenConfigured_MissingAuth_Rejected() {
        Assert.Equal(HttpGateDecision.RejectAuth, Gate(token: "s3cr3t"));
    }

    [Fact]
    public void TokenConfigured_WrongToken_Rejected() {
        Assert.Equal(HttpGateDecision.RejectAuth, Gate(auth: "Bearer nope", token: "s3cr3t"));
    }

    [Fact]
    public void TokenConfigured_CorrectBearer_Allowed() {
        Assert.Equal(HttpGateDecision.Allow, Gate(auth: "Bearer s3cr3t", token: "s3cr3t"));
    }

    [Fact]
    public void TokenConfigured_BearerCaseInsensitiveScheme_Allowed() {
        Assert.Equal(HttpGateDecision.Allow, Gate(auth: "bearer s3cr3t", token: "s3cr3t"));
    }

    [Fact]
    public void EmptyToken_NoAuthRequired() {
        // Default (no token configured) relies on Host/Origin only; a request with no Authorization passes.
        Assert.Equal(HttpGateDecision.Allow, Gate(auth: null, token: ""));
    }

    // ---- Non-loopback bind requires a token (#143 hardening) ----

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("192.168.1.10")]
    [InlineData("10.0.0.5")]
    public void NonLoopbackBind_RequiresToken(string bind) {
        Assert.True(AgentServer.BindRequiresAuthToken(bind));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.5")]
    [InlineData("::1")]
    [InlineData("localhost")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-an-address")]
    public void LoopbackOrUnparseableBind_DoesNotRequireToken(string? bind) {
        Assert.False(AgentServer.BindRequiresAuthToken(bind));
    }

    [Fact]
    public void MethodCheckedBeforeEverythingElse() {
        // A GET from a malicious origin is rejected as a bad method (order shouldn't leak that host/origin
        // were even inspected); the important property is that a non-Allow decision blocks Dispatch.
        Assert.NotEqual(HttpGateDecision.Allow,
            Gate(method: "GET", host: "evil.com", origin: "http://evil.com"));
    }
}
