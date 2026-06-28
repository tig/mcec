using System;
using System.IO.Ports;

namespace MCEControl;

public class SerialReplyContext : Reply {
    private readonly SerialPort _rs232;
    public SerialReplyContext(SerialPort rs232) {
        _rs232 = rs232;
    }
    public override void Write(String text) {
        if (_rs232 != null && _rs232.IsOpen) {
            _rs232.Write(text);
        }
    }
}
