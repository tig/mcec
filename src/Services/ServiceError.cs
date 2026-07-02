//-------------------------------------------------------------------
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System.Net.Sockets;

namespace MCEControl;

/// <summary>
/// The payload of <see cref="ServiceBase.ErrorOccurred"/> (#211): a human-readable message plus,
/// when the error came from a <see cref="SocketException"/>, the typed
/// <see cref="System.Net.Sockets.SocketError"/> and HResult that the old stringly
/// <c>NotificationCallback</c> flattened into the message text. Consumers that only log can use
/// <see cref="ToString"/> (which reproduces the legacy "<c>message, HRESULT (SocketErrorCode)</c>"
/// shape); consumers that care about the error kind read the typed properties.
/// </summary>
public sealed record ServiceError(string Message, SocketError? SocketError = null, int? HResult = null) {
    /// <summary>Builds a <see cref="ServiceError"/> from a <see cref="SocketException"/>, preserving
    /// the typed error code instead of flattening it into the message.</summary>
    public static ServiceError FromSocketException(string message, SocketException se) =>
        new(message, se.SocketErrorCode, se.HResult);

    public override string ToString() =>
        SocketError is null ? Message : $"{Message}, {HResult:X} ({SocketError})";
}
