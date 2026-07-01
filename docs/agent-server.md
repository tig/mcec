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

The agent surface is a set of new commands — `capture`, `query`, `displays`, `find`,
`wait-for`, `invoke`, `drag`, and `click` — that return **structured JSON** (a
`CommandResult`) instead of free text.
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
   `AgentCommandsEnabled`, in `mcec.settings`. This is a **separate** switch from
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

**Which gate applies where.** The agent *tools* — `capture`/`query`/`displays`/`find`/`wait-for`/`invoke`/
`record`/`drag`/`click` — are gated by **both** `AgentCommandsEnabled` **and** the per-command `Enabled`
flag, over **both** MCP transports (`mcec.exe --mcp` stdio and the HTTP floor): a `tools/call` for a
command whose `Enabled=false` is refused (`error.code: command-disabled`) even when
`AgentCommandsEnabled=true`. **`send_command` is the exception** — it is a raw pass-through to the existing
command engine and does **not** require `AgentCommandsEnabled`; the raw command it runs is still subject to
that command's own `Enabled` flag in `mcec.commands` (the normal MCEC gate). `McpServerEnabled` gates only
the HTTP floor; it has no bearing on stdio or on which individual tools may run.

---

## How to enable

Edit `mcec.settings` (in your MCEC settings directory) and set the opt-ins you
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
| `capture`  | Screenshot a window (`PrintWindow` + `PW_RENDERFULLCONTENT`, captures WinUI/WPF surfaces) or a screen region, returned as base64 PNG. Blank/black frames are detected and flagged (see [Observation hardening](#observation-hardening--known-limitations)). | window target, or region `x`/`y`/`width`/`height`; optional `file` |
| `query`    | Dump the **UI Automation tree** of a window: control type, name, automation id, bounds, enabled/offscreen state, value. | window target, `maxDepth` (default 6), `maxNodes` (default 1000) |
| `displays` | Report **display geometry** — every monitor's pixel `bounds`, `workingArea`, `primary` flag, and `dpi`/`scale`, plus the union `virtualBounds`. Lets an agent interpret the absolute-pixel bounds `query`/`find` return and place pixel clicks/drags without measuring the screen itself. | *(none)* |
| `find`     | Find a **UI Automation element** by name / automation id / class.                 | window target, `by` (`name`\|`automationid`\|`classname`), `value`, `timeout` |
| `wait-for` | Same as `find`, but waits up to a timeout for the element to appear (default 5 s). | window target, `by`, `value`, `timeout` |
| `invoke`   | Drive a UI Automation element pattern (incl. select for SelectionItem) — far more reliable than coordinate clicks. | window target, `by`, `value`, `action` (`invoke`\|`toggle`\|`setvalue`\|`setfocus`\|`expand`\|`collapse`\|`select`), `text` |
| `drag`     | Press → move along a path → release, dispatched **atomically** (nothing interleaves). Each endpoint is a UI Automation element (dragged from/to its centre) or an absolute screen pixel; add `path` waypoints for a curved/multi-stop drag. Covers window resize/move by chrome, sliders, marquee-select, drag-reorder. | window target (needed when an endpoint is an element); `from`/`to` each `{ by, value }` or `{ x, y }`; optional `path` `[{ x, y }, …]` |
| `click`    | Click at a point — a UI Automation element (clicked at its centre) or an absolute screen pixel — with move+click dispatched **atomically**. For element types `invoke` can't drive, or when you must target a pixel. Prefer `invoke` for ordinary buttons/menus. | window target (needed when `at` is an element); `at` = `{ by, value }` or `{ x, y }`; `button` (`left`\|`right`\|`middle`, default `left`); `count` (`1`\|`2`, default `1`) |
| `record`   | Record a window or region to an **animated GIF** over time (start/stop or a bounded one-shot). | window target, or region `x`/`y`/`width`/`height`; `action` (`start`\|`stop`\|`oneshot`), `fps`, `durationMs`, `maxWidth`, `file` |

All return a `CommandResult` JSON object via `Reply.WriteLine`:

```json
{
  "success": true,
  "command": "query",
  "error": null,
  "data": { /* command-specific payload */ },
  "warnings": [ { "code": "tree-truncated", "detail": "…" } ]
}
```

`warnings` is present only when there are non-fatal conditions to report. On failure the
result additionally carries `errorCode` (a stable, fine-grained string) and `errorCategory`
(a coarse class from the closed taxonomy: `timeout`, `ambiguous-selector`, `stale-element`,
`no-target`, `capture-blank`, `focus`, `elevation`, `foreground`, `internal`). These fields
track the shared agent result contract in
[`docs/design/agent-tool-result-contract.md`](design/agent-tool-result-contract.md) (#101);
they are additive over the legacy `success`/`error`/`data` shape, which still works.

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
    "blankCheck": { "blank": false, "dominantFraction": 0.34, "dominantIsDark": false },
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

`blankCheck` reports the blank-frame analysis (see
[Observation hardening](#observation-hardening--known-limitations)). When a **window** capture
comes back blank the result is a failure with `errorCategory: "capture-blank"` — but the PNG
stays in `data` so it is never a *silent* bad image. A blank **region** capture is reported as
a `capture-blank` warning instead, since a user-specified region can legitimately be empty.

(Over MCP, `capture` additionally returns the PNG as an `image` content block so the
model can view it directly, in addition to the JSON text above — including for a blank-frame
failure, so the agent can see what was grabbed.)

### `record` — capturing change over time

`capture` answers "what does this look like **now**". When you need to show change *over
time* — an animation for a demo or issue report, or a repro of a transient/flicker — use
`record`, which writes an **animated GIF**.

> **⚠️ Privacy:** a recording captures whatever is on screen for its *entire* duration,
> not just one instant — it is a louder disclosure than a still `capture`. Only record
> what you mean to, keep recordings short, and be aware the GIF may contain sensitive
> content (credentials, messages, other windows). Recording is off unless the operator has
> enabled the agent commands, and every start/stop/write is `AGENT-AUDIT:`-logged.

Two ways to bound a recording:

- **One-shot:** give `durationMs` (and optional `fps`); MCEC records that long, then writes
  the GIF and returns metadata in a single call.
- **Segment:** `action: "start"` begins recording and returns immediately; `action: "stop"`
  ends it, encodes, writes the file, and returns metadata. Only one recording runs at a time.

Safety limits (operator-configurable in `mcec.settings`, requests above them are *clamped*,
not failed) keep an agent from producing an unbounded file:

| Setting                    | Default  | Meaning |
|----------------------------|----------|---------|
| `AgentRecordMaxFps`        | 30       | Max frames per second (`fps` default is 5). |
| `AgentRecordMaxDurationMs` | 60000    | Max recording length (60 s). |
| `AgentRecordMaxFrames`     | 600      | Hard cap on captured frames. |
| `AgentRecordMaxWidth`      | 1280     | Frames are downscaled so width fits this. |

A finished `record` (one-shot or `stop`) returns the output path and metadata:

```json
{
  "success": true,
  "command": "record",
  "data": {
    "file": "C:\\Users\\me\\AppData\\Local\\Temp\\mcec-rec-20260629-141503.gif",
    "frames": 73,
    "durationMs": 14600,
    "fps": 5,
    "width": 1280,
    "height": 824,
    "bytes": 1048576,
    "target": {
      "handle": 1576490, "title": "Untitled - Notepad", "className": "Notepad",
      "processName": "notepad", "processId": 21344,
      "x": 120, "y": 80, "width": 1024, "height": 768
    }
  }
}
```

`action: "start"` returns `{ "recording": true, "fps": 5, "maxDurationMs": 60000, "target": { … } }`.
If `file` is omitted, MCEC writes to a timestamped path under the system temp directory and
reports it in `file`.

No extra dependency is used: each frame is quantized + LZW-compressed to a GIF by GDI+, and
the frames are stitched into one GIF89a (Netscape loop extension + per-frame delays). See
`docs/design/gif-recording.md` for the full design.

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
    "nodeCount": 7,
    "truncated": false,
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

## Observation hardening & known limitations

An agent can only act on what it can reliably see, so `capture` and `query` are built to fail
loudly rather than hand back a plausible-looking but wrong observation. This section documents
what is trustworthy and what is not.

### Blank / black frame detection

Every capture is analyzed for blank content: the frame is sampled on a bounded grid, each pixel
is quantized to 5 bits per channel, and the share held by the single most common color is
measured. A real application window is busy and scores low; a failed grab is a flat fill and
scores ~1.0. When the dominant color covers ≥ 99% of the frame it is flagged blank, and a
near-black dominant color is distinguished from a legitimately empty (e.g. white) surface.

- A **window** capture that comes back blank is a failure (`errorCategory: "capture-blank"`,
  `errorCode: "frame-all-black"` or `"frame-uniform"`). The PNG is still returned in `data` (and
  as an MCP image block) so it is never a *silent* bad image and can serve as the failure's last
  observation.
- A **region** capture that comes back blank is reported as a `capture-blank` **warning**, since
  a user-specified region can legitimately be empty.
- The raw numbers are always in `data.blankCheck` (`blank`, `dominantFraction`, `dominantIsDark`).

### `PrintWindow` and the on-screen-blit fallback

Window capture uses `PrintWindow(PW_RENDERFULLCONTENT)`, which renders DirectComposition / WinUI 3
/ WPF surfaces that a plain screen grab returns black for, and captures windows even when occluded.
Known limits:

- **Fallback is degraded.** If the driver refuses `PrintWindow`, capture falls back to an
  on-screen blit (`Graphics.CopyFromScreen`). That blit grabs whatever pixels are physically on
  screen, so it returns black for composited/occluded surfaces and cannot see a window that is
  behind another. When the fallback runs, the result carries a `capture-fallback` warning.
- **Minimized windows** have no on-screen pixels; `PrintWindow` typically yields a blank frame
  (caught by blank detection). Restore the window before capturing.
- **Cloaked windows** (e.g. background virtual-desktop or some UWP states) may not render.
- **Hardware-overlay/protected content** (some video players, DRM surfaces) renders black by
  design and cannot be captured.

### Locked sessions and UAC

- **Locked / disconnected sessions:** when the workstation is locked or the session is detached,
  the desktop cannot be rendered and captures are blank. This is detected (blank frame) but cannot
  be worked around from user space. Live validation of locked-session behavior is tracked
  separately in [#78].
- **Elevation (UAC):** MCEC running at medium integrity cannot read the UIA tree of, drive, or
  reliably capture a window owned by an elevated (high-integrity) process. Such targets surface as
  empty/failed observations; run MCEC elevated only if you explicitly need to automate elevated
  apps, and understand the security trade-off.

### UIA tree size & stability

`query` is bounded on two axes so its output stays stable for agent reasoning even on pathological
trees (e.g. a virtualized list with thousands of items):

- `maxDepth` (default 6) bounds tree depth.
- `maxNodes` (default 1000) bounds the total node count. When the cap clips the walk, the result
  reports `truncated: true` and a `tree-truncated` warning rather than silently returning a partial
  tree; `nodeCount` always reports how many nodes were captured. Raise `maxNodes` or narrow the
  target (deeper `window`/`handle` selector) for a complete tree.

Individual stale or unsupported UIA nodes never abort the whole walk — they are skipped and the
rest of the tree is returned.

[#78]: https://github.com/tig/mcec/issues/78

---

## Using MCEC as an MCP server

MCEC can run **headless** as an MCP **stdio** server — no UI, no tray icon — so an MCP
client (such as a desktop AI assistant) can spawn it on demand and talk to it over
standard input/output:

```
mcec.exe --mcp
```

Wire it into your MCP client config (the `claude_desktop_config.json` / `mcp.json`
style used by most clients):

```json
{
  "mcpServers": {
    "mcec": {
      "command": "C:/Program Files/Kindel Systems/MCEC/mcec.exe",
      "args": ["--mcp"]
    }
  }
}
```

> The agent commands still obey the security gates above. Running `--mcp` does **not**
> bypass `AgentCommandsEnabled` or the per-command `Enabled` flags — set those in
> `mcec.settings` first.

### Tools exposed over MCP

When connected, the server advertises these tools:

| Tool           | Maps to                                                        |
|----------------|----------------------------------------------------------------|
| `capture`      | The `capture` command (window screenshot → base64 PNG).        |
| `query`        | The `query` command (describe a window).                       |
| `displays`     | The `displays` command (per-monitor bounds + DPI/scale, virtual bounds). |
| `find`         | The `find` command (match a UI element, one-shot).             |
| `wait-for`     | The `wait-for` command (poll for a UI element until a timeout). |
| `invoke`       | The `invoke` command (run an existing MCEC command, incl. select for tabs etc). |
| `drag`         | The `drag` command (atomic press → move-path → release, element or pixel endpoints). |
| `click`        | The `click` command (atomic click at an element centre or pixel). |
| `record`       | The `record` command (window/region → animated GIF over time). |
| `send_command` | Generic raw-command passthrough — send any MCEC command line.  |

---

## Concurrency

Agent tool calls follow a simple contract so one slow call never stalls the others:

- **Observation runs concurrently.** `query`, `capture`, `find`, `wait-for`, and `record` take **no
  shared lock** — a deep `query`, a large `capture`, or a long `wait-for` never blocks another tool call,
  even one from a different session. They snapshot state (each UIA read uses its own automation instance;
  screen capture is stateless) and don't mutate the desktop.
- **Global-input actuation serializes.** `drag` and `send_command` synthesize physical mouse/keyboard —
  the one input stream is a shared resource, so they run one-at-a-time under a single `InputLock`
  (concurrent requests can't interleave keystrokes/mouse).
- **`invoke` is UIA-pattern actuation**, dispatched on a worker with a short *modal grace*: because
  invoking a control can open a modal dialog that blocks synchronously, `invoke` never holds the input
  lock for the dialog's lifetime — otherwise the agent couldn't `query`/`capture`/`invoke` to dismiss the
  very dialog it opened (see the `invoke` notes above).
- **The legacy TCP/serial command pipeline is untouched.** Home-automation commands still run in order,
  synchronously, on the UI thread (`CommandInvoker.ExecuteNext`) — this contract governs only the agent
  (MCP) tools.

Both MCP transports honor this by dispatching each request on a worker: the HTTP floor serves every
`POST` on a thread-pool task, and the stdio loop dispatches each line concurrently (writes are serialized;
JSON-RPC responses carry the request `id`, so out-of-order completion is fine). So a slow call from one
client/session never blocks another's requests — not just callers that invoke `Dispatch` on their own
threads.

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

- New, opt-in agent surface: `capture`, `query`, `displays`, `find`, `wait-for`, `invoke`, `drag`, `click`, `record`.
- Structured JSON results; same commands exposed as MCP/HTTP tools.
- **Three independent off-by-default gates:** `AgentCommandsEnabled`, per-command
  `Enabled`, and `McpServerEnabled` (localhost-bound).
- Loud `AGENT-AUDIT:` logging for every agent action.
- Fully additive — nothing about the existing HTPC behavior changes.

## Agent safety features

Two operator-safety features build on the gates above — see
[`safety-emergency-stop-and-provisioning.md`](safety-emergency-stop-and-provisioning.md):

- **Emergency stop (#135):** a global panic hotkey (default `Ctrl+Alt+Shift+S`, set via
  `EmergencyStopHotkey`) that instantly halts a session from any window — latching the actuation gate
  (`emergency-stopped` refusals until re-armed), aborting in-flight actuation, and releasing held input. It
  reacts to physical input only, so the agent can never trip or defeat it.
- **Isolated session provisioning (#138):** `provision-session` (gated by `AllowSessionProvisioning`) hands
  an agent a disposable, isolated MCEC directory instead of it mutating the installed config; `end-session`
  (or launch-time reaping) tears it down.
