using System;
using System.Net.Sockets;

namespace MCEControl;

internal class ClientReplyContext(TcpClient tcpClient) : Reply {
    public override void Write(String text) {
        ArgumentNullException.ThrowIfNull(text);

        if (!tcpClient.Connected) {
            return;
        }

        // #212: UTF-8, matching the server's outbound encoding (was ASCII, which
        // flattened any non-ASCII char to '?').
        byte[] buf = TelnetProtocol.EncodeOutbound(text);
        tcpClient.GetStream().Write(buf, 0, buf.Length);
    }
}
