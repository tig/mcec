<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# MCEC Documentation

**MCEC**: the **Model Context Environment Controller**; gives an AI agent eyes, hands, and a safe front
door on a Windows PC. It is a small, self-contained native Windows daemon that a computer-use model can
**mount, see through, and drive**: capture a window, read its UI Automation tree, find and wait for
controls, launch apps, and actuate keyboard/mouse/window input; exposed to agents and scripts over the
**Model Context Protocol (MCP)**.

MCEC also remains the same battle-tested **remote control for home-automation systems** it has always been:
a network/serial listener that turns text commands into keystrokes, mouse moves, window messages, and app
launches for Control4, Crestron, iRule, and similar systems.

```
mcec.exe --mcp        # run headless as an MCP stdio server an agent can mount
```

> **Two front doors, one command core.** MCEC 3.0's agent surface is **purely additive** and **off by
> default**. Every classic remote-control feature is unchanged; every agent capability must be explicitly
> enabled. If you do nothing, MCEC behaves exactly as it did before.

**Where to go next**

* **[Agent Server user guide](agent-server.md)**: the full agent/MCP reference: the tools, the result
  envelope, observation hardening, running as an MCP server, and the security gates. Start here for AI/agent
  use.
* **[Agent safety](safety-emergency-stop-and-provisioning.md)**: the emergency-stop hotkey and isolated
  session provisioning.
* **[Home Automation & Remote Control](home-automation.md)**: the classic TCP/serial command surface: the
  command language, the Client/Server/Serial transports, the User Activity Monitor, testing, and examples.
* **[AGENTS.md](https://github.com/tig/mcec/blob/main/AGENTS.md)**: the connect-time guidance an agent
  gets, plus the "dogfood" recipe (MCEC driving MCEC).

## What MCEC does

An AI agent runs a loop: **observe → target → act → observe**. MCEC gives it all four:

* **Observe**: `capture` a window as a PNG (renders composited WinUI/WPF surfaces via `PrintWindow`, with
  blank-frame detection); `query` the UI Automation tree (control type, name, automation id, bounds, state,
  value); `displays` for per-monitor geometry and DPI; `record` a window/region to an animated GIF over
  time.
* **Target**: resolve a window by title substring, process name, class name, handle, or "the foreground
  window"; `find` / `wait-for` a specific UI element by name / automation id / class.
* **Act**: `invoke` a UI Automation pattern (invoke/toggle/setvalue/setfocus/expand/collapse/select),
  `launch` an app directly, `click` a point or element, `drag` (atomic press → move-path → release), or
  `send_command` to run any raw MCEC command.

Understand the trade before enabling any of it: MCEC drives the desktop with real user input, so
**everything a user can do, an agent can do**; the gates decide *whether* an agent gets that power,
not *how much*. Every capability is off by default, localhost-bound, narrated by an on-by-default
on-screen overlay, and loudly audit-logged. See the **[Agent Server user guide](agent-server.md)**
for the complete tool reference and the security model.

For the classic remote-control role (driving a Windows PC from a home-automation controller over TCP/IP or
serial), see **[Home Automation & Remote Control](home-automation.md)**.

## Installation

MCEC 3.0 targets Windows 10 and later. Submit an [Issue](https://github.com/tig/mcec/issues) to request
support for another version.

**[Download and install the latest version](https://github.com/tig/mcec/releases)**

If **Collect Telemetry** is checked during setup, anonymous usage information is sent to help improve MCEC.
Telemetry is controlled via the `HKEY_LOCAL_MACHINE\SOFTWARE\Kindel\MCE Controller` `Telemetry` DWORD
registry value (`1` enables, `0` disables). See the [telemetry page](telemetry.md) for details.

Uninstall MCEC via Add/Remove Programs.

## Running

Setup installs MCEC under Program Files and adds it to the **Start Menu**; after installation, launch it
from there. MCEC runs as a normal windowed app that can minimize to a taskbar (tray) icon. Closing the
main window minimizes it to the tray; double-click the tray icon to show it again, or right-click for a
menu. To start hidden, check **Hide Window at Startup** in **Settings**.

To run headless as an **MCP server** (no main window, no tray icon; the command overlay and the
emergency-stop hotkey still work), launch it with `mcp` (or the equivalent `--mcp`); an MCP client can
spawn it on demand and talk JSON-RPC over stdio:

```
mcec.exe mcp
```

The **installed** copy (under Program Files) refuses to run as an MCP server or to serve the MCP/HTTP
endpoint: enabling agent gates in the installed configuration would leak them enabled if a session
crashed. Use [session provisioning](safety-emergency-stop-and-provisioning.md) to get a disposable,
isolated copy, or copy the install directory somewhere writable and run from there.

`mcec.exe` also has a command-line surface (built on
[Terminal.Gui.Cli](https://github.com/gui-cs/cli)): `--help`, `--version`, `--opencli` (machine-readable
command metadata for tools and agents), and `agent-guide` (prints the same agent guidance the MCP server
hands connecting clients). Run these from a terminal; with no arguments `mcec.exe` starts the GUI as
always.

Run multiple instances by copying the installation directory elsewhere; each copy gets its own independent
`.settings`, `.commands`, and `.log` files. (This directory-per-instance isolation is exactly what the
agent [session-provisioning](safety-emergency-stop-and-provisioning.md) feature automates.)

Use **File ▸ Exit** to shut down.

## Settings

![Settings](settings_general.png "Settings")

Settings are stored as XML in `mcec.settings`, in the `%APPDATA%\Kindel\MCEC` directory. Most settings
are edited from the **File ▸ Settings…** dialog; the agent gates (except the provisioning opt-in on the
**Agent** tab, below) are edited directly in `mcec.settings`.

The **General** tab:

* **Hide Window at Startup**: start minimized to the tray icon.
* **Log Threshold**: how much is shown in the main window (`INFO`, `DEBUG`, or `ALL`). Log *files* always
  contain `ALL` events.
* **Default command pacing (ms)**: delay MCEC applies before executing each received command (default 0).

The **Client**, **Server**, **Serial Server**, and **Activity Monitor** tabs configure the classic
remote-control transports and are documented in
**[Home Automation & Remote Control](home-automation.md)**.

The **Agent** tab is where you let an agent (a desktop assistant or computer-use tool) work with MCEC via
[session provisioning](safety-emergency-stop-and-provisioning.md):

* **Allow agents to provision disposable instances**: the one switch to turn on. A connected agent then
  gets a fresh, throwaway copy of MCEC to drive, deleted when it finishes. It never opens up this installed
  copy.
* **Provisioned instances**: lists those copies (age, size, running or not), with **Delete** / **Delete
  all** to clear any an agent left behind. MCEC also cleans up stale ones on its own.

### Agent settings (in `mcec.settings`)

The agent surface is configured by these keys. All are off/safe by default; see the
**[Agent Server user guide](agent-server.md)** for the full security model.

| Setting | Default | Meaning |
|---------|---------|---------|
| `AgentCommandsEnabled` | `false` | Master opt-in for the agent observation/actuation commands. Separate from the classic command enable. |
| `McpServerEnabled` | `false` | Enables the localhost HTTP/JSON-RPC floor (`POST /mcp`). |
| `McpBindAddress` | `127.0.0.1` | Address the HTTP floor binds to (localhost only by default). |
| `McpHttpPort` | `5151` | Port for the HTTP floor. |
| `CommandOverlayEnabled` | `true` | Shows an on-screen overlay narrating each agent command as it runs, so anyone watching can see MCEC is driving. |
| `CommandOverlayPosition` | `Right` | Which side of the primary screen the overlay docks to. |
| `EmergencyStopEnabled` | `true` | Arms the global emergency-stop hotkey while the agent front door could be driving. |
| `EmergencyStopHotkey` | `Ctrl+Alt+Shift+S` | The panic-hotkey chord (a `+`-separated spec). |
| `AllowSessionProvisioning` | `false` | Operator opt-in that lets an agent request a fresh, isolated MCEC instance via `provision-session`. |
| `AgentRecordMaxFps` / `AgentRecordMaxDurationMs` / `AgentRecordMaxFrames` / `AgentRecordMaxWidth` | 30 / 60000 / 600 / 1280 | Safety limits for the `record` tool (requests above them are clamped, not failed). |

Restart MCEC (or relaunch `--mcp`) after editing `mcec.settings`.

## Enabling or Disabling Commands

For security, **every** command is disabled by default; this reduces the surface area MCEC exposes. This
applies to both the classic commands and the agent commands: an agent command runs only when
`AgentCommandsEnabled=true` **and** that individual command is enabled.

Use the **Commands Window** (**Commands ▸ Enable and Test Commands…**) to enable/disable commands and test
them. Details, including the `mcec.commands` XML format, are in
**[Home Automation & Remote Control](home-automation.md#enabling-or-disabling-commands)**.

## Agent safety

An enabled agent has your hands: everything you can do at the keyboard, it can do, and no gate can
meter that down. So beyond the off-by-default gates, two operator-safety features keep you in control
(details in **[Agent safety](safety-emergency-stop-and-provisioning.md)**):

* **Emergency stop**: a global "dead-man's-switch" hotkey (default `Ctrl+Alt+Shift+S`) you can hit from
  *any* window to instantly halt a session. It latches the actuation gate (every tool call is refused with
  `emergency-stopped` until you re-arm), aborts in-flight actuation, and releases held input. It reacts to
  **physical input only**: an agent's injected keystrokes can never trip or defeat it. Re-arm from the
  **⛔ Re-arm** menu item, or, running headless (`--mcp`), from the prompt that opens when the stop
  engages (press the hotkey again to reopen it).
* **Isolated session provisioning**: instead of enabling agent commands in your installed MCEC (a crash
  could leave those gates enabled), an authorized agent calls `provision-session` to get a disposable,
  isolated copy with its own agent-ready config. Teardown is just deleting the directory; the installed
  config is never touched.

## Logging

Informational, debug, and diagnostic events are logged to `mcec.log` and shown in the main window. Installs
under Program Files write the log to `%APPDATA%\Kindel\MCEC\mcec.log`. Otherwise the log is written to the
directory MCEC is started from.
Every agent action is additionally logged with a loud `AGENT-AUDIT:` line so agent activity is impossible
to miss.
