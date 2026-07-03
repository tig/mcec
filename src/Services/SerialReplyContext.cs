using System;
using System.IO.Ports;

namespace MCEControl;

public class SerialReplyContext(SerialPort rs232) : Reply {
    public override void Write(String text) {
        if (rs232.IsOpen) {
            rs232.Write(text);
        }
    }
}
