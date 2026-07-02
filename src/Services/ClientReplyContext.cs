using System;
using System.Net.Sockets;

namespace MCEControl;

internal class ClientReplyContext : Reply {
    private readonly TcpClient _tcpClient;
    // Constructor which takes a Socket and a client number
    public ClientReplyContext(TcpClient tcpClient) {
        _tcpClient = tcpClient;
    }

    public override void Write(String text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        if (!_tcpClient.Connected) {
            return;
        }

        byte[] buf = System.Text.Encoding.ASCII.GetBytes(TelnetProtocol.EscapeIac(text));
        _tcpClient.GetStream().Write(buf, 0, buf.Length);
    }
}
