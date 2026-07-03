using System;
using System.Net.Sockets;

namespace MCEControl;

public class ServerReplyContext(SocketServer server, Socket socket, int clientNumber) : Reply {
    // Per-connection command accumulation (CR/LF/NUL delimiters + the #148 max-length
    // cap); the one shared implementation, same as SocketClient and SerialServer (#212).
    internal CommandAccumulator Accumulator { get; } = new();
    internal Socket Socket { get; set; } = socket;
    internal int ClientNumber { get; set; } = clientNumber;

    // Buffer to store the data sent by the client.
    // Name is part of the test surface (InternalsVisibleTo); not renamed to _dataBuffer.
    // ReSharper disable once InconsistentNaming
    internal readonly byte[] DataBuffer = new byte[1024];

    public override void Write(String text) {
        server.Send(text, this);
    }
}
