<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# MCEC 3.0 — The Agent Automation Server

MCEC 3.0 turns the MCE Controller daemon into a small, opt-in automation server for
AI agents and scripts running on a Windows PC. It gives an agent three things:

- **Eyes** — capture a screenshot of a window (or the foreground window) as a PNG.
- **Hands** — invoke any existing MCEC command (the actuation layer you already use).
- **A front door** — query/find windows and UI elements, wait for conditions, and
  drive all of the above over **MCP** (Model Context Protocol) or a tiny **HTTP** floor.

The agent surface is a set of new commands — `capture`, `query`, `find`, `wait-for`,
and `invoke` — that return **structured JSON** (a `CommandResult`) instead of free text.
Those same commands are exposed as tools over MCP/HTTP so an agent can call them
directly.

> **This release is purely additive.** No existing HTPC command, transport, or default
> is changed. If you do nothing, MCEC behaves exactly as it did before — every new
> capability is **off by default** and must be explicitly enabled.

---

## SECURITY — read this first

The agent server is powerful: it can see your screen and drive your PC. It is therefore
locked down by default and uses **layered, independent opt-ins**. Turning one thing on
does **not** turn the others on.

1. **Agent commands are DISABLED by default.**
   The new observation/automation commands require their **own** opt-in,
   `AgentCommandsEnabled`, in `MCEControl.settings`. This is a **separate** switch from
   the existing actuation/command enable — enabling MCEC to run commands does **not**
   enable the agent surface, and vice-versa. Every individual command also remains
   `Enabled=false` until you turn it on, exactly as with all other MCEC commands.

2. **The MCP / HTTP façade is DISABLED by default.**
   The network-facing server (`McpServerEnabled`) is off unless you opt in. Even when
   enabled, the HTTP floor **binds to localhost only** (`McpBindAddress = 127.0.0.1`),
   so it is not reachable from other machines. There is no remote binding default and
   no authentication bypass — if you need off-box access, front it with your own
   reverse proxy and auth.

3. **Every agent action is loudly audited.**
   Each agent command logs an `AGENT-AUDIT:` line (action + target) before it runs.
   These lines are intentionally noisy so that agent activity is impossible to miss in
   the MCEC log window or log files. If you see `AGENT-AUDIT:` lines you did not expect,
   something is driving your machine.

If any one of these switches is off, the corresponding capability simply refuses to run
and returns a JSON failure (for commands) — it never silently proceeds.

---

## How to enable

Edit `MCEControl.settings` (in your MCEC settings directory) and set the opt-ins you
want. At minimum, to use the agent commands at all:

```xml
<AgentCommandsEnabled>true</AgentCommandsEnabled>
```

To additionally expose the MCP / HTTP server so agents can connect over a transport:

```xml
<McpServerEnabled>true</McpServerEnabled>
<!-- Optional; these are the defaults: -->
<McpBindAddress>127.0.0.1</McpBindAddress>
<McpHttpPort>5151</McpHttpPort>
```

Restart MCEC after editing the settings file. Remember you must **also** enable the
individual agent commands you intend to use (they ship `Enabled=false` like every other
command).

---

## The commands

All commands target a window the same way — by `window` (title substring,
case-insensitive), `handle` (HWND), `process` (process name without `.exe`),
`className`, or `foreground` (the current foreground window).

| Command    | What it does                                                                      | Key args |
|------------|-----------------------------------------------------------------------------------|----------|
| `capture`  | Screenshot a window (`PrintWindow` + `PW_RENDERFULLCONTENT`, captures WinUI/WPF surfaces) or a screen region, returned as base64 PNG. | window target, or region `x`/`y`/`width`/`height`; optional `file` |
| `query`    | Dump the **UI Automation tree** of a window: control type, name, automation id, bounds, enabled/offscreen state, value. | window target, `maxDepth` (default 6) |
| `find`     | Find a **UI Automation element** by name / automation id / class.                 | window target, `by` (`name`\|`automationid`\|`classname`), `value`, `timeout` |
| `wait-for` | Same as `find`, but waits up to a timeout for the element to appear (default 5 s). | window target, `by`, `value`, `timeout` |
| `invoke`   | Drive a UI Automation element pattern — far more reliable than coordinate clicks. | window target, `by`, `value`, `action` (`invoke`\|`toggle`\|`setvalue`\|`setfocus`), `text` |

All five return a `CommandResult` JSON object via `Reply.WriteLine`:

```json
{
  "success": true,
  "command": "query",
  "error": null,
  "data": { /* command-specific payload */ }
}
```

On failure (including when the security gates are off):

```json
{
  "success": false,
  "command": "capture",
  "error": "Agent commands are disabled (AgentCommandsEnabled=false).",
  "data": null
}
```

### `capture` result example

`capture` renders the target window (using `PrintWindow`, so it works even when the
window is occluded) and returns the image inline as base64-encoded PNG plus the window
geometry:

```json
{
  "success": true,
  "command": "capture",
  "data": {
    "handle": 1576490,
    "width": 1024,
    "height": 768,
    "encoding": "png",
    "bytes": 48213,
    "base64": "iVBORw0KGgoAAAANSUhEUgAA...",
    "window": {
      "handle": 1576490,
      "title": "Untitled - Notepad",
      "className": "Notepad",
      "processName": "notepad",
      "processId": 21344,
      "x": 120, "y": 80, "width": 1024, "height": 768
    }
  }
}
```

(Over MCP, `capture` additionally returns the PNG as an `image` content block so the
model can view it directly, in addition to the JSON text above.)

### `query` result example

`query` returns the window descriptor plus its UI Automation tree (depth-limited):

```json
{
  "success": true,
  "command": "query",
  "data": {
    "window": {
      "handle": 1576490, "title": "Untitled - Notepad",
      "className": "Notepad", "processName": "notepad", "processId": 21344,
      "x": 120, "y": 80, "width": 1024, "height": 768
    },
    "tree": {
      "controlType": "Window",
      "name": "Untitled - Notepad",
      "x": 120, "y": 80, "width": 1024, "height": 768,
      "isEnabled": true,
      "isOffscreen": false,
      "children": [
        { "controlType": "Edit", "automationId": "15", "name": "Text editor",
          "x": 122, "y": 110, "width": 1020, "height": 720, "isEnabled": true }
      ]
    }
  }
}
```

---

## Using MCEC as an MCP server

MCEC can run **headless** as an MCP **stdio** server — no UI, no tray icon — so an MCP
client (such as a desktop AI assistant) can spawn it on demand and talk to it over
standard input/output:

```
MCEControl.exe --mcp
```

Wire it into your MCP client config (the `claude_desktop_config.json` / `mcp.json`
style used by most clients):

```json
{
  "mcpServers": {
    "mcec": {
      "command": "C:/Program Files/Kindel Systems/MCE Controller/MCEControl.exe",
      "args": ["--mcp"]
    }
  }
}
```

> The agent commands still obey the security gates above. Running `--mcp` does **not**
> bypass `AgentCommandsEnabled` or the per-command `Enabled` flags — set those in
> `MCEControl.settings` first.

### Tools exposed over MCP

When connected, the server advertises these tools:

| Tool           | Maps to                                                        |
|----------------|----------------------------------------------------------------|
| `capture`      | The `capture` command (window screenshot → base64 PNG).        |
| `query`        | The `query` command (describe a window).                       |
| `find`         | The `find` command (enumerate / match windows).                |
| `invoke`       | The `invoke` command (run an existing MCEC command).           |
| `send_command` | Generic raw-command passthrough — send any MCEC command line.  |

---

## HTTP floor

When `McpServerEnabled = true`, MCEC also accepts a single JSON-RPC request per `POST`
over HTTP, bound to localhost only:

```
POST http://127.0.0.1:5151/mcp
Content-Type: application/json

{ "jsonrpc": "2.0", "id": 1, "method": "tools/call",
  "params": { "name": "query", "arguments": { "foreground": true } } }
```

The address and port come from `McpBindAddress` (default `127.0.0.1`) and `McpHttpPort`
(default `5151`). This is a deliberately minimal floor for local scripts and agents; it
is not a general-purpose web API and is not exposed off-box by default.

---

## Summary

- New, opt-in agent surface: `capture`, `query`, `find`, `wait-for`, `invoke`.
- Structured JSON results; same commands exposed as MCP/HTTP tools.
- **Three independent off-by-default gates:** `AgentCommandsEnabled`, per-command
  `Enabled`, and `McpServerEnabled` (localhost-bound).
- Loud `AGENT-AUDIT:` logging for every agent action.
- Fully additive — nothing about the existing HTPC behavior changes.
