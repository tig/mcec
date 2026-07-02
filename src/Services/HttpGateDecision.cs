// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl;

/// <summary>
/// Outcome of validating an inbound MCP/HTTP request against the localhost front-door policy
/// (issue #143). Anything other than <see cref="Allow"/> means the request is refused before the
/// body is read or any tool is dispatched.
/// </summary>
public enum HttpGateDecision {
    /// <summary>Request passed all checks and may be dispatched.</summary>
    Allow,
    /// <summary>Not a POST; only POST is accepted.</summary>
    RejectMethod,
    /// <summary>Path is not exactly <c>/mcp</c>.</summary>
    RejectPath,
    /// <summary>Host header is missing or not a loopback authority (DNS-rebinding defense).</summary>
    RejectHost,
    /// <summary>Origin header is present and not loopback (cross-site request forgery defense).</summary>
    RejectOrigin,
    /// <summary>A bearer token is configured and the request's token is missing or wrong.</summary>
    RejectAuth,
}
