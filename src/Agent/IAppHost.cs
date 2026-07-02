// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;

namespace MCEControl;

/// <summary>
/// The host-capability half of the <see cref="AgentRuntime"/> seam (#209): the few things engine
/// code (commands, services) needs from whatever is hosting it that are genuinely host-specific.
/// The GUI host is <c>MainWindow</c> (registered in its settings-apply path); the headless
/// <c>--mcp</c> host is <see cref="HeadlessAppHost"/> (registered by <c>Program</c>'s bootstrap).
/// Engine code calls the <see cref="AgentRuntime"/> wrappers (<see cref="AgentRuntime.SendLine"/>,
/// <see cref="AgentRuntime.RequestShutdown"/>, <see cref="AgentRuntime.MessageWindowHandle"/>)
/// rather than holding an <see cref="IAppHost"/>; nothing below the UI layer may touch
/// <c>MainWindow</c> directly (touching it used to lazily construct the Form headless).
/// </summary>
public interface IAppHost {
    /// <summary>
    /// Sends a line of text to every connected transport (TCP client/server, serial). GUI:
    /// <c>MainWindow.SendLine</c>. Headless: there are no legacy transports, so this is a logged no-op.
    /// </summary>
    void SendLine(string line);

    /// <summary>
    /// Requests an orderly application shutdown (<c>mcec:exit</c>, the updater's install-and-restart).
    /// GUI: <c>MainWindow.ShutDown()</c> (self-marshals to the UI thread). Headless: schedules a clean
    /// process exit after in-flight protocol replies flush; the same net effect as the MCP client
    /// closing stdin.
    /// </summary>
    void RequestShutdown();

    /// <summary>
    /// A window handle engine code may register OS notifications against
    /// (<c>RegisterPowerSettingNotification</c> for <c>UserActivityMonitorService</c>'s
    /// power-broadcast detection, which needs the host's WndProc to receive
    /// <c>WM_POWERBROADCAST</c>). GUI: the <c>MainWindow</c> handle. Headless: throws; the activity
    /// monitor is only ever started by the GUI host, so no headless message window exists.
    /// </summary>
    IntPtr MessageWindowHandle { get; }
}
