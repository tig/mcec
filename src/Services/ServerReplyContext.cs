using System;
using System.Net.Sockets;
using System.Text;

namespace MCEControl;

public class ServerReplyContext : Reply {
    internal StringBuilder CmdBuilder { get; set; }
    internal Socket Socket { get; set; }
    internal int ClientNumber { get; set; }

    // Buffer to store the data sent by the client
    internal byte[] DataBuffer = new byte[1024];

    private readonly SocketServer _server;

    // Constructor which takes a Socket and a client number
    public ServerReplyContext(SocketServer server, Socket socket, int clientNumber) {
        CmdBuilder = new StringBuilder();
        _server = server;
        Socket = socket;
        ClientNumber = clientNumber;
    }

    protected string Command {
        get { return CmdBuilder.ToString(); }
        set { }
    }

    public override void Write(String text) {
        _server.Send(text, this);
    }
}
