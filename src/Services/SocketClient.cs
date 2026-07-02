//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
//
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCEControl.Properties;

namespace MCEControl;
/// <summary>
/// SocketClient implements our TCP/IP client ("Act as client"): it connects OUT to a
/// remote host and receives CR/LF/NUL-delimited commands over that connection.
///
/// Rewritten as one async run loop for issue #212. Each <see cref="Start"/> owns one
/// run: a <see cref="CancellationTokenSource"/> plus a Task running
/// <see cref="RunAsync"/>, which optionally sleeps the configured delay, connects, and
/// reads buffered chunks into a <see cref="CommandAccumulator"/>. When the connection
/// drops it reconnects after <see cref="AppSettings.ClientDelayTime"/> (no delay
/// configured means no auto-reconnect — the same contract MainWindow.RestartClient
/// has always enforced). The pre-#212 shape this replaces had four defects:
/// <list type="bullet">
///   <item>Stop() nulled fields (<c>_bw</c>/<c>_tcpClient</c>) that the read loop was
///   dereferencing on another thread, so routine stop-while-receiving threw
///   NullReferenceException on a ThreadPool thread. The loop now only touches locals
///   it owns; Stop() just cancels.</item>
///   <item>A finalizer called Dispose() and touched managed state; the class holds no
///   unmanaged resources, so it was pure hazard. Deleted.</item>
///   <item>The receive loop ran synchronously inside the BeginConnect callback, one
///   ReadByte() per syscall with Thread.Sleep(100) per command — a pinned thread and a
///   10 commands/sec cap. Now: <c>await ReadAsync</c> into a 4 KB buffer.</item>
///   <item>Double-Start() orphaned the previous BackgroundWorker and TcpClient.
///   Start() now cancels (supersedes) any prior run, which closes its own
///   TcpClient on the way out.</item>
/// </list>
///
/// Note this class can be invoked from multiple threads simultaneously and must be
/// threadsafe.
/// </summary>
public sealed class SocketClient : ServiceBase, IDisposable {
    private readonly string _host = "";
    private readonly int _port;
    private readonly int _clientDelayTime;

    // Guards the current-run fields below. A run's CTS is installed under this lock in
    // Start() and detached under it in RunAsync's finally, so Stop()'s Cancel() can
    // never race the run's Dispose() of the same CTS.
    private readonly object _runLock = new();
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    // The TcpClient of the CURRENT connection. Published by the run loop when a
    // connection is established and cleared (compare-exchange, so a superseded run can
    // never clobber its successor's connection) when that connection ends. Send()
    // snapshots it — it is NEVER nulled out from under a dereference (#212).
    private TcpClient? _tcpClient;

    // Test seam (InternalsVisibleTo MCEControl.xUnit): the current run's task, so
    // tests can wait for a stopped/superseded run to complete and assert it did not
    // fault (the pre-#212 stop-while-receiving NRE surfaced exactly here).
    internal Task? RunTask {
        get {
            lock (_runLock) {
                return _runTask;
            }
        }
    }

    public SocketClient(AppSettings settings) {
        if (settings is null) {
            throw new ArgumentNullException(nameof(settings));
        }

        _port = settings.ClientPort;
        _host = settings.ClientHost;
        _clientDelayTime = settings.ClientDelayTime;
    }

    #region IDisposable Members
    // No finalizer (#212): this class owns no unmanaged resources. Dispose just
    // cancels the run; the run closes its own TcpClient and disposes its own CTS.
    public void Dispose() {
        Cancel();
        GC.SuppressFinalize(this);
    }
    #endregion

    /// <summary>
    /// Starts the client. Any prior run is superseded: its token is cancelled and it
    /// closes its own TcpClient on the way out, so double-Start can no longer orphan
    /// a worker or connection (#212).
    /// </summary>
    /// <param name="delay">If true, sleep <see cref="AppSettings.ClientDelayTime"/>
    /// before the first connect attempt (used by MainWindow.RestartClient).</param>
    public void Start(bool delay = false) {
        CancellationTokenSource cts = new();
        lock (_runLock) {
            _cts?.Cancel();
            _cts = cts;
            _runTask = RunAsync(delay, cts);
        }
    }

    /// <summary>
    /// Stops the client: cancels the current run — aborting any in-flight
    /// delay/connect/read, after which the run closes its own TcpClient — and reports
    /// Stopped. Deliberately does NOT null any field the run loop dereferences; the
    /// pre-#212 Stop() did, and routine stop-while-receiving (toggling "Act as
    /// client") threw NullReferenceException on a ThreadPool thread.
    /// </summary>
    public void Stop() {
        Log4.Debug("SocketClient: Stop");
        Cancel();
        SetStatus(ServiceStatus.Stopped);
    }

    private void Cancel() {
        lock (_runLock) {
            _cts?.Cancel();
        }
    }

    /// <summary>
    ///  Send text to remote connection
    /// </summary>
    /// <param name="text"></param>
    /// <param name="replyContext"></param>
    public override void Send(string text, Reply? replyContext = null) {
        base.Send(text, replyContext);

        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        // Snapshot the current connection; the run loop may retire it concurrently.
        TcpClient? tcpClient = Volatile.Read(ref _tcpClient);
        if (tcpClient == null) {
            return;
        }

        try {
            if (!tcpClient.Connected) {
                return;
            }

            byte[] buf = TelnetProtocol.EncodeOutbound(text);
            tcpClient.GetStream().Write(buf, 0, buf.Length);
        }
        catch (IOException ioe) {
            Error(ioe.Message);
        }
        catch (ObjectDisposedException) {
            // The connection was torn down (stop/supersede/drop) between the snapshot
            // and the write — equivalent to "not connected"; nothing to report.
        }
        catch (InvalidOperationException) {
            // GetStream() on a socket that just disconnected — same benign race.
        }

        // TODO: Implement notifications
    }

    /// <summary>
    /// The single owner of the client's lifecycle (#212): optional delay →
    /// connect → receive until the connection ends, looping to reconnect (with the
    /// configured delay) until cancelled. Never faults: cancellation is swallowed as
    /// normal shutdown and anything unexpected is reported via Error().
    /// </summary>
    private async Task RunAsync(bool delay, CancellationTokenSource cts) {
        CancellationToken ct = cts.Token;
        try {
            bool firstAttempt = true;
            while (!ct.IsCancellationRequested) {
                // The configured delay applies before a delayed first attempt
                // (Start(delay: true)) and before every reconnect attempt.
                if ((delay || !firstAttempt) && _clientDelayTime > 0) {
                    SetStatus(ServiceStatus.Sleeping);
                    await Task.Delay(_clientDelayTime, ct).ConfigureAwait(false);
                }

                firstAttempt = false;

                await ConnectAndReceiveAsync(ct).ConfigureAwait(false);

                if (_clientDelayTime <= 0) {
                    // No reconnect delay configured means no auto-reconnect — the
                    // same contract MainWindow.RestartClient has always enforced.
                    break;
                }
            }

            if (!ct.IsCancellationRequested) {
                SetStatus(ServiceStatus.Stopped);
            }
        }
        catch (OperationCanceledException) {
            // Normal Stop()/supersede — not an error.
            Log4.Debug("SocketClient: run cancelled");
        }
        catch (Exception e) {
            // Last-ditch: never let the run task fault (pre-#212 this shape was an
            // unhandled exception on a ThreadPool thread).
            Error($"SocketClient: Generic Exception: {e.GetType().Name} {e.Message}");
        }
        finally {
            lock (_runLock) {
                if (ReferenceEquals(_cts, cts)) {
                    _cts = null;
                }
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// One connection attempt: resolve, connect, then receive until the peer drops or
    /// the run is cancelled. Owns its TcpClient — created and disposed here, so a
    /// superseded or stopped run always cleans up its own connection. Connection
    /// errors are reported (via Error(), which MainWindow reacts to) and swallowed so
    /// the run loop can retry; only cancellation propagates.
    /// </summary>
    private async Task ConnectAndReceiveAsync(CancellationToken ct) {
        Log4.Debug($"SocketClient: Connect - {_host}:{_port}");
        using TcpClient tcpClient = new();
        try {
            // See if we've just been handed a straight IPv4 address, if so don't bother with DNS
            if (!IPAddress.TryParse(_host, out IPAddress? hostIp)) {
                // GetHostEntry returns a list. We need to pick the IPv4 entry.
                // TODO: Support ipv6
                IPHostEntry hostEntry = await Dns.GetHostEntryAsync(_host, ct).ConfigureAwait(false);
                IPAddress[] ipv4Addresses = Array.FindAll(hostEntry.AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
                Log4.Debug($"SocketClient: {ipv4Addresses.Length} IP v4 addresses found");

                if (ipv4Addresses.Length == 0) {
                    throw new IOException($"{_host}:{_port} didn't resolve to a valid address");
                }

                hostIp = ipv4Addresses[0];
            }

            // TELEMETRY: Do not pass _host to SetStatus to avoid collecting PII
            SetStatus(ServiceStatus.Started, $"{hostIp}:{_port}");

            Log4.Debug($"SocketClient: ConnectAsync({hostIp}, {_port})");
            await tcpClient.ConnectAsync(hostIp, _port, ct).ConfigureAwait(false);

            Volatile.Write(ref _tcpClient, tcpClient);
            SetStatus(ServiceStatus.Connected, $"{_host}:{_port}");

            await ReceiveUntilClosedAsync(tcpClient, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // Stop()/supersede — RunAsync treats this as normal shutdown.
            throw;
        }
        catch (SocketException e) {
            Log4.Debug($"SocketClient: {e.GetType().Name}: {e.Message}");
            CatchSocketException(e);
        }
        catch (IOException e) {
            if (e.InnerException is SocketException sockExcept) {
                CatchSocketException(sockExcept);
            }
            else {
                Error($"SocketClient: {e.GetType().Name}: {e.Message}");
            }
        }
        catch (Exception e) {
            // Got this when endPoint = new IPEndPoint(Dns.GetHostEntry(_host).AddressList[0], _port)
            // resolved to an ipv6 address
            Log4.Debug($"SocketClient: Generic Exception: {e.GetType().Name}: {e.Message}");
            Error($"SocketClient: Generic Exception: {e.GetType().Name} {e.Message}");
        }
        finally {
            // Retire this run's connection — but only if it is still the current one:
            // a superseding Start() may already have published its own (#212).
            _ = Interlocked.CompareExchange(ref _tcpClient, null, tcpClient);
        }
    }

    /// <summary>
    /// The receive loop: buffered async reads (no more one-ReadByte-per-syscall, no
    /// more Thread.Sleep(100) per command — #212) decoded as UTF-8 and fed to a
    /// <see cref="CommandAccumulator"/>, which owns the CR/LF/NUL delimiter and #148
    /// max-length logic. The server sends UTF-8; a stateful Decoder handles multi-byte
    /// characters split across reads (the old loop cast each byte to char, mangling
    /// anything non-ASCII).
    /// </summary>
    private async Task ReceiveUntilClosedAsync(TcpClient tcpClient, CancellationToken ct) {
        NetworkStream stream = tcpClient.GetStream();

        // #148: cap accumulated command length so a peer streaming bytes without
        // a delimiter cannot grow the buffer until OOM. On overflow the partial
        // command is dropped, an error is logged, and input is discarded until
        // the next delimiter.
        CommandAccumulator accumulator = new();
        Action onOverflow = () => Error($"SocketClient: command exceeded maximum length ({CommandAccumulator.MaxCommandLength} chars); discarding input until next delimiter");

        Decoder decoder = Encoding.UTF8.GetDecoder();
        byte[] buffer = new byte[4096];
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];

        while (!ct.IsCancellationRequested) {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (bytesRead == 0) {
                Error("No more data.");
                return;
            }

            int charCount = decoder.GetChars(buffer, 0, bytesRead, chars, 0);
            for (int i = 0; i < charCount; i++) {
                string? cmd = accumulator.ProcessChar(chars[i], onOverflow);
                if (cmd != null) {
                    SendNotification(ServiceNotification.ReceivedData, ServiceStatus.Connected, new ClientReplyContext(tcpClient), cmd);
                }
            }
        }
    }

    private void CatchSocketException(SocketException e) {
        switch (e.ErrorCode) {
            case 10004: // WSAEINTR - Interrupted function call
                // Not an error - this means the client has shut down
                break;

            default:
                string? s = Resources.ResourceManager.GetString($"WSA_{e.ErrorCode}", System.Globalization.CultureInfo.InvariantCulture);
                if (s == null) {
                    Error($"{e.Message} ({e.ErrorCode})");
                }
                else {
                    Error($"{e.Message}. {s} ({e.ErrorCode})");
                }
                break;
        }
    }

}
