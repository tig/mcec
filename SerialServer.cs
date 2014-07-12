//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.IO.Ports;      

namespace MCEControl {
    /// <summary>
    /// Implements the serial port server 
    /// </summary>
    sealed public class SerialServer : ServiceBase, IDisposable {

        #region IDisposable Members

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private Thread _readThread ;
        private SerialPort _serialPort;

        private void Dispose(bool disposing)
        {
            if (disposing) {
                if (_serialPort != null)
                    _serialPort.Close();
                _serialPort = null;
                if (_readThread != null)
                    _readThread.Abort();
                _readThread = null;
            }
        }

        //-----------------------------------------------------------
        // Control functions (Start, Stop, etc...)
        //-----------------------------------------------------------
        public void Start(String portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake) {

            if (_serialPort != null || _readThread != null)
                Stop();

            Debug.Assert(_serialPort == null);
            Debug.Assert(_readThread == null);

            _serialPort = new SerialPort {
                PortName = portName, 
                BaudRate = baudRate, 
                Parity = parity, 
                DataBits = dataBits, 
                StopBits = stopBits,
                Handshake = handshake, 
                ReadTimeout = 500
            };

            try {
                // Set the read/write timeouts
                Debug.WriteLine("Opening serial port: " + GetSettingsDisplayString());
                SetStatus(ServiceStatus.Started, GetSettingsDisplayString());
                _serialPort.Open();

                _readThread = new Thread(Read);
                _readThread.Start();
                SetStatus(ServiceStatus.Waiting);
            }
            catch (IOException ioe) {
                Error(ioe.Message);
                Stop();
            }
            catch (UnauthorizedAccessException uae) {
                Error(String.Format("Port in use? {0} ({1})", uae.Message, GetSettingsDisplayString()));
                Stop();
            }
            catch (Exception e) {
                Error(e.Message);
                Stop();              
            }
        }

        public void Stop() {
            Debug.WriteLine("Serial Server Stop");
            Dispose(true);
            SetStatus(ServiceStatus.Stopped);
        }

        // Returns a string with serial settings, e.g. "COM1 9600 baud N81 Xon/Xoff"
        public String GetSettingsDisplayString()
        {
            if (_serialPort == null)
                return "";
            String p = "";
            switch (_serialPort.Parity)
            {
                case Parity.Space:
                    p = "S";
                    break;
                case Parity.None:
                    p = "N";
                    break;
                case Parity.Mark:
                    p = "M";
                    break;
                case Parity.Odd:
                    p = "O";
                    break;
            }

            String sbits = "";
            switch (_serialPort.StopBits)
            {
                case StopBits.OnePointFive:
                    sbits = "1.5";
                    break;
                case StopBits.Two:
                    sbits = "2";
                    break;
                case StopBits.One:
                    sbits = "1";
                    break;
            }

            String hand = "";
            switch (_serialPort.Handshake)
            {
                case Handshake.RequestToSend:
                    hand = "Hardware";
                    break;
                case Handshake.XOnXOff:
                    hand = "Xon / Xoff";
                    break;
                case Handshake.None:
                    hand = "None";
                    break;
                case Handshake.RequestToSendXOnXOff:
                    hand = "Both";
                    break;
            }

            return String.Format("{0} {1} baud {2}{3}{4} {5}", _serialPort.PortName, _serialPort.BaudRate, p, _serialPort.DataBits, sbits, hand);
        }

        private void Read()
        {
            Debug.WriteLine(String.Format("Serial Read thread starting: {0}", GetSettingsDisplayString()));
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                try
                {
                    if (_serialPort == null) {
                        Debug.WriteLine("_serialPort is null in Read()");
                        break;
                    }
                    char c = (char)_serialPort.ReadChar();
                    if (c == '\r' || c == '\n' || c == '\0')
                    {
                        string cmd = sb.ToString();
                        sb.Length = 0;
                        if (cmd.Length > 0)
                            SendNotification(ServiceNotification.ReceivedData,
                                            CurrentStatus,
                                            new SerialReplyContext(_serialPort),
                                            cmd);
                    }
                    else sb.Append(c);

                }
                catch (TimeoutException) {
                    Debug.WriteLine("SerialServer: TimeoutException");
                }
                catch (IOException ioe) {
                    Debug.WriteLine("SerialServer: IOException: "+ ioe.Message);
                }
                catch (Exception e) {
                    Debug.WriteLine("SerialServer: Exception: " + e.Message);
                }
            }
            Debug.WriteLine("SerialServer: Exiting Read()");
        }

        #region Nested type: SerialReplyContext
        public class SerialReplyContext : Reply {
            private SerialPort _rs232;
            public SerialReplyContext(SerialPort rs232) {
                _rs232 = rs232;
            }
            public override void Write(String text) {
                if (_rs232 != null && _rs232.IsOpen) {
                    _rs232.Write(text);
                }
            }
        }
        #endregion
    }
}