//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System.Net.Sockets;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// <see cref="ServiceError"/> (#211) carries the typed SocketError/HResult that the old
/// stringly notifications flattened into message text, and its ToString reproduces the
/// legacy "message, HRESULT (SocketErrorCode)" log shape.
/// </summary>
public class ServiceErrorTests {
    [Fact]
    public void ToString_PlainMessage_IsJustTheMessage() {
        var error = new ServiceError("something broke");

        Assert.Equal("something broke", error.ToString());
    }

    [Fact]
    public void ToString_WithSocketError_ReproducesLegacyShape() {
        var se = new SocketException((int)SocketError.ConnectionReset);
        var error = ServiceError.FromSocketException($"OnDataReceived: {se.Message}", se);

        // The legacy stringly notification flattened this as "msg, HRESULT-hex (SocketErrorCode)".
        Assert.Equal($"OnDataReceived: {se.Message}, {se.HResult:X} (ConnectionReset)", error.ToString());
    }

    [Fact]
    public void FromSocketException_CarriesTypedFields() {
        var se = new SocketException((int)SocketError.NetworkDown);

        var error = ServiceError.FromSocketException("ctx", se);

        Assert.Equal("ctx", error.Message);
        Assert.Equal(SocketError.NetworkDown, error.SocketError);
        Assert.Equal(se.HResult, error.HResult);
    }
}
