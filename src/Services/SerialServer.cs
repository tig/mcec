//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCEControl; 
/// <summary>
/// Implements the serial port server 
/// </summary>
public sealed class SerialServer : ServiceBase, IDisposable {

    #region IDisposable Members

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion

    private Thread? _readThread;
    private SerialPort? _serialPort;
    private CancellationTokenSource? _readCancellationTokenSource;

    private void Dispose(bool disposing) {
        if (disposing) {
            _serialPort?.Close();
            _serialPort = null;
            if (_readCancellationTokenSource != null) {
                _readCancellationTokenSource.Cancel();
                _readCancellationTokenSource.Dispose();
                _readCancellationTokenSource = null;
            }
            _readThread = null;
        }
    }

    //-----------------------------------------------------------
    // Control functions (Start, Stop, etc...)
    //-----------------------------------------------------------
    public void Start(String portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake) {
        if (_serialPort != null || _readThread != null) {
            Stop();
        }

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
            Log4.Debug("Opening serial port: " + GetSettingsDisplayString());
            SetStatus(ServiceStatus.Started, GetSettingsDisplayString());
            _serialPort.Open();

            _readCancellationTokenSource = new CancellationTokenSource();
            _readThread = new Thread(() => Read(_readCancellationTokenSource.Token));
            _readThread.Start();
            SetStatus(ServiceStatus.Waiting);
        }
        catch (IOException ioe) {
            Error(ioe.Message);
            Stop();
        }
        catch (UnauthorizedAccessException uae) {
            Error($"Port in use? {uae.Message} ({GetSettingsDisplayString()})");
            Stop();
        }
        catch (Exception e) {
            Error(e.Message);
            Stop();
        }
    }

    public void Stop() {
        Log4.Debug("Serial Server Stop");

        Dispose(true);
        SetStatus(ServiceStatus.Stopped);
    }

    // Returns a string with serial settings, e.g. "COM1 9600 baud N81 Xon/Xoff"
    public string GetSettingsDisplayString() {
        if (_serialPort == null) {
            return "";
        }

        string p = "";
        switch (_serialPort.Parity) {
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

        string sbits = "";
        switch (_serialPort.StopBits) {
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

        string hand = "";
        switch (_serialPort.Handshake) {
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

        return $"{_serialPort.PortName} {_serialPort.BaudRate} baud {p}{_serialPort.DataBits}{sbits} {hand}";
    }

    // Update Read method to accept a CancellationToken
    private void Read(CancellationToken cancellationToken) {
        Log4.Debug($"Serial Read thread starting: {GetSettingsDisplayString()}");
        StringBuilder sb = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested) {
            try {
                if (_serialPort == null) {
                    Log4.Debug("_serialPort is null in Read()");
                    break;
                }
                char c = (char)_serialPort.ReadChar();
                if (c == '\r' || c == '\n' || c == '\0') {
                    string cmd = sb.ToString();
                    sb.Length = 0;
                    if (cmd.Length > 0) {
                        SendNotification(ServiceNotification.ReceivedData,
                                        CurrentStatus,
                                        new SerialReplyContext(_serialPort),
                                        cmd);
                    }
                }
                else {
                    sb.Append(c);
                }
            }
            catch (TimeoutException) {
                Log4.Debug("SerialServer: TimeoutException");
            }
            catch (IOException ioe) {
                Log4.Debug("SerialServer: IOException: " + ioe.Message);
            }
            catch (Exception e) {
                Log4.Debug("SerialServer: Exception: " + e.Message);
            }
        }
        Log4.Debug("SerialServer: Exiting Read()");
    }

    // Send text on serial port. 
    // BUGBUG: This function has never been tested.
    public override void Send(string text, Reply? replyContext = null) {
        base.Send(text, replyContext);

        if (_serialPort != null && _serialPort.IsOpen) {
            _serialPort.Write(text);
        }

        // TODO: Implement notifications
        //if (_mainSocket.Send(Encoding.UTF8.GetBytes(text)) > 0) {
        //    SendNotification(ServiceNotification.Write, CurrentStatus, replyContext, text.Trim());
        //}
        //else {
        //    SendNotification(ServiceNotification.WriteFailed, CurrentStatus, replyContext, text);
        //}

    }
}
