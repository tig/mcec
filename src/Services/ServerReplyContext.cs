using System;
using System.Net.Sockets;

namespace MCEControl;

public class ServerReplyContext : Reply {
    // Per-connection command accumulation (CR/LF/NUL delimiters + the #148 max-length
    // cap); the one shared implementation, same as SocketClient and SerialServer (#212).
    internal CommandAccumulator Accumulator { get; } = new();
    internal Socket Socket { get; set; }
    internal int ClientNumber { get; set; }

    // Buffer to store the data sent by the client
    internal byte[] DataBuffer = new byte[1024];

    private readonly SocketServer _server;

    // Constructor which takes a Socket and a client number
    public ServerReplyContext(SocketServer server, Socket socket, int clientNumber) {
        _server = server;
        Socket = socket;
        ClientNumber = clientNumber;
    }

    public override void Write(String text) {
        _server.Send(text, this);
    }
}
