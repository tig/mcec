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
`wait-for`, `invoke`, `launch`, `drag`, and `click` — exposed as **tools over MCP/HTTP**
so an agent can call them directly. Each tool call returns a **structured JSON result
envelope** (`{ ok, result, … }`) instead of free text, so an agent can reason about
success and failure uniformly.

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
   enabled, the HTTP floor **binds to localhost by default**, and a **loopback** bind is
   the only configuration that needs no authentication. A loopback `McpBindAddress`
   (`localhost`, or a literal loopback IP — any `127.x.y.z`, `::1` / `[::1]`) is
   **canonicalized** before it reaches the listener ([#152]) — so obfuscated loopback
   spellings the OS parser still reads as loopback (e.g. `127.1`, `0x7f.0.0.1`,
   `2130706433`, `::ffff:127.0.0.1`) are normalized to a plain loopback literal
   (`127.0.0.1` / `[::1]`) rather than passed through raw, closing a path where the
   underlying HTTP stack could treat the raw form as a wildcard binding. A **non-loopback**
   bind (a specific LAN IP, or the all-interfaces `0.0.0.0` / `::`) is a deliberate
   off-box exposure and is allowed **only when `McpAuthToken` is set** ([#143]); without a
   token MCEC **refuses to start the HTTP listener**, logging a loud error, so a config
   typo can never silently expose unauthenticated UI automation to the network. (The
   `HttpListener` wildcards `+` / `*` and other hostnames are not loopback and are never
   DNS-resolved, so they too require a token — and generally fail to bind.)

3. **Every agent action is loudly audited.**
   Each agent command logs an `AGENT-AUDIT:` line (action + target) before it runs.
   These lines are intentionally noisy so that agent activity is impossible to miss in
   the MCEC log window or log files. If you see `AGENT-AUDIT:` lines you did not expect,
   something is driving your machine.

If any one of these switches is off, the corresponding capability simply refuses to run
and returns a JSON failure (for commands) — it never silently proceeds.

**Which gate applies where.** The agent *tools* — `capture`/`query`/`displays`/`find`/`wait-for`/`invoke`/
`record`/`launch`/`drag`/`click` — are gated by **both** `AgentCommandsEnabled` **and** the per-command `Enabled`
flag, over **both** MCP transports (`mcec.exe --mcp` stdio and the HTTP floor): a `tools/call` for a
command whose `Enabled=false` is refused (`error.code: command-disabled`) even when
`AgentCommandsEnabled=true`.

**`send_command` is transport-sensitive (#153).** It is a raw pass-through to the existing command engine,
so it is a command-injection surface. Over the **local stdio** transport (`mcec.exe --mcp`, launched by its
client — no network/CSRF surface) it keeps the documented pass-through and does **not** require
`AgentCommandsEnabled`. Over the **network-facing HTTP floor** it honors the **same `AgentCommandsEnabled`
gate as every other tool**: with `McpServerEnabled=true` but `AgentCommandsEnabled=false`, a `send_command`
`tools/call` is refused (`error.code: agent-commands-disabled`) and never executed. This is deliberate
secure-by-default hardening — enabling the HTTP floor alone must not expose a raw-command surface with no
agent opt-in. Before [#143]'s front-door validation landed, such a request was reachable by browser CSRF /
DNS-rebinding; with #143's `Host`/`Origin`/token gate now in place **and** this `AgentCommandsEnabled` gate,
that surface is closed. In **both** cases the raw command it runs is still subject to that command's own `Enabled`
flag in `mcec.commands` (the normal MCEC gate). `McpServerEnabled` gates only the HTTP floor; it has no
bearing on stdio or on which individual tools may run.

[#143]: https://github.com/tig/mcec/issues/143
[#153]: https://github.com/tig/mcec/issues/153

**The command queue is bounded.** Commands (from any client — network, serial, or an agent's
`send_command`) are queued and executed paced (`CommandPacing` delay between items). To prevent a remote
memory/CPU DoS the queue is capped at **200** pending commands, and a single command's whole tree — the
command itself plus all recursively embedded commands — at **50**. Enqueue is **all-or-nothing**: a command
that breaks either bound, or whose tree doesn't fit in the queue's remaining capacity, is **dropped whole
and logged** (a `CommandInvoker` warning in the MCEC log) — never partially enqueued, since a split tree
could separate paired input commands (e.g. `shiftdown:`/`shiftup:`) and leave a modifier key latched.
Agents should batch or pace long input sequences (e.g. prefer `drag`/`mouse:drag` over long `mouse:mt`
streams) rather than flooding the queue.

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
| `launch`   | Launch an app directly (path + args + working dir); gated. Returns pid and primary window handle/info when the window appears. Preferred over Win+R composition. | `path` (required), `arguments`, `workingDirectory`, `timeout` |
| `click`    | Click at a point — a UI Automation element (clicked at its centre) or an absolute screen pixel — with move+click dispatched **atomically**. For element types `invoke` can't drive, or when you must target a pixel. Prefer `invoke` for ordinary buttons/menus. | window target (needed when `at` is an element); `at` = `{ by, value }` or `{ x, y }`; `button` (`left`\|`right`\|`middle`, default `left`); `count` (`1`\|`2`, default `1`) |
| `record`   | Record a window or region to an **animated GIF** over time (start/stop or a bounded one-shot). | window target, or region `x`/`y`/`width`/`height`; `action` (`start`\|`stop`\|`oneshot`), `fps`, `durationMs`, `maxWidth`, `file` |

Every MCP **tool call** returns one result envelope. An agent branches on `ok` first; on
success it reads `result`, on failure it reads `error`:

```json
{
  "ok": true,
  "result": { /* tool-specific payload */ },
  "warnings": [ { "code": "tree-truncated", "detail": "…" } ],
  "sessionId": "5f19c9c01a3f"
}
```

A result is **either** a success (`ok: true`, `result` present, no `error`) **or** a failure
(`ok: false`, `error` present, no `result`) — never both. `warnings` (non-fatal conditions)
may appear on either. `sessionId` is present when the call ran inside a mounted session.
Over MCP, the transport's `isError` flag mirrors the envelope (`isError = !ok`).

On failure the `error` object carries a stable, fine-grained `code`, a coarse `category` from
the closed taxonomy (`timeout`, `ambiguous-selector`, `stale-element`, `no-target`,
`capture-blank`, `focus`, `elevation`, `foreground`, `internal`), a human-readable `detail`,
and — when available — a `lastObservation` (the last good state before the failure, so a
failed call is debuggable without rerunning it):

```json
{
  "ok": false,
  "error": {
    "code": "window-not-found",
    "category": "no-target",
    "detail": "No matching window for selector window='Settings'.",
    "lastObservation": { /* the last good query/capture */ }
  },
  "sessionId": "5f19c9c01a3f"
}
```

> **Where the shape comes from.** Internally each agent command still emits the thinner legacy
> `CommandResult` (`{ success, command, error, data, warnings }`, in `src/Commands/CommandResult.cs`).
> The `AgentServer` translates that into the `{ ok, result, error, … }` envelope at the MCP
> boundary (`AgentToolResult.FromLegacy`), which is the shape an MCP client actually receives and
> the one specified by the shared result contract in
> [`docs/design/agent-tool-result-contract.md`](design/agent-tool-result-contract.md). A
> couple of feature-specific refusals ride in `error.code` while `error.category` stays `internal`:
> `emergency-stopped` (the operator engaged the [emergency stop](safety-emergency-stop-and-provisioning.md)),
> `provisioning-not-authorized` (`AllowSessionProvisioning` is off), and `command-disabled` (the
> per-command `Enabled` gate).

### `capture` result example

`capture` renders the target window (using `PrintWindow`, so it works even when the
window is occluded) and returns the image inline as base64-encoded PNG plus the window
geometry:

```json
{
  "ok": true,
  "result": {
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
comes back blank the result is a failure with `error.category: "capture-blank"`, so an agent
never trusts a silent bad image. A blank **region** capture is reported as a `capture-blank`
warning instead, since a user-specified region can legitimately be empty.

**Region size limits.** Region `width`/`height` are agent-controlled, so they are capped — an
unbounded region (e.g. `40000x40000` ≈ 6.4 GB of raw ARGB, before PNG encoding and base64) could
otherwise exhaust the host's memory. A region may be at most **16384 px per side** and
**64,000,000 px total** (64 MP ≈ 256 MB raw — roughly eight 4K frames). An oversized region is
**rejected before anything is allocated or captured** — the call fails with
`errorCode: "region-too-large"` (`errorCategory: "no-target"`) and a detail stating the limit,
and the rejection is `AGENT-AUDIT:`-logged. The same caps apply to `record` regions (window
targets need no cap: they are bounded by the window's own size). These limits are fixed, not
settings: they are an anti-DoS bound sized well beyond real desktop geometry, not a tuning knob.

On a successful `capture`, MCEC additionally returns the PNG as an MCP `image` content block so
the model can view it directly, alongside the JSON envelope above.

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

**Recording lifecycle.** An open `start` is never unbounded: the capture loop *auto-stops*
when it hits the operator's max duration or max frames (or the target vanishes mid-record).
An auto-stopped recording is **completed, not lost**:

- `action: "stop"` still returns the buffered GIF — exactly once. A second `stop` fails with
  "No recording is in progress or awaiting fetch", and fetching releases the buffered frames.
- A new recording (`start` **or** a one-shot) is allowed after an auto-stop. If the
  auto-stopped GIF was never fetched, the new recording **replaces** it: the discarded output
  is gone, and that command's result carries an `unfetched-recording-discarded` warning (for
  a one-shot, on its single final reply) — also audit-logged. Fetch with `stop` promptly if
  you want the output.

Safety limits (operator-configurable in `mcec.settings`, requests above them are *clamped*,
not failed) keep an agent from producing an unbounded file:

| Setting                    | Default  | Meaning |
|----------------------------|----------|---------|
| `AgentRecordMaxFps`        | 30       | Max frames per second (`fps` default is 5). |
| `AgentRecordMaxDurationMs` | 60000    | Max recording length (60 s). |
| `AgentRecordMaxFrames`     | 600      | Hard cap on captured frames. |
| `AgentRecordMaxWidth`      | 1280     | Frames are downscaled so width fits this. |

A `record` **region** target is additionally subject to the fixed capture region size limits
(max 16384 px per side, 64,000,000 px total — see
[Region size limits](#capture-result-example)): an oversized region fails fast with
`errorCode: "region-too-large"` before any recording starts, rather than being clamped.

A finished `record` (one-shot or `stop`) returns the output path and metadata:

```json
{
  "ok": true,
  "result": {
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
  "ok": true,
  "result": {
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

- A **window** capture that comes back blank is a failure (`error.category: "capture-blank"`,
  `error.code: "frame-all-black"` or `"frame-uniform"`), so it is never a *silent* bad image — an
  agent branches on the failure rather than trusting the frame.
- A **region** capture that comes back blank is reported as a `capture-blank` **warning**, since
  a user-specified region can legitimately be empty.
- The raw numbers are in the success payload's `blankCheck` (`blank`, `dominantFraction`, `dominantIsDark`).

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
  be worked around from user space.
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
| `launch`       | Direct gated app launch (returns pid + window handle).         |
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
  the one input stream is a shared resource, so they serialize on a single gate
  (`AgentRuntime.InputGate`): `drag` actuates directly under the gate on its worker, while
  `send_command` enqueues into the command engine, whose single dispatcher thread (#195) holds the same
  gate around each queued command's `Execute` (concurrent requests can't interleave keystrokes/mouse).
- **`invoke` is UIA-pattern actuation**, dispatched on a worker with a short *modal grace*: because
  invoking a control can open a modal dialog that blocks synchronously, `invoke` never holds the input
  gate for the dialog's lifetime — otherwise the agent couldn't `query`/`capture`/`invoke` to dismiss the
  very dialog it opened (see the `invoke` notes above).
- **The legacy TCP/serial command pipeline shares the same queue and dispatcher (#195).** Home-automation
  commands and `send_command` are both *producers* into the one `CommandInvoker` queue; its dedicated
  dispatcher thread is the only consumer and executes every command in order under the input gate, so
  legacy traffic and agent actuation can never interleave either. `send_command` returns only after its
  command actually executed (a per-enqueue completion the dispatcher signals), with a 30s wait bound —
  a longer-running command keeps executing, but the call reports `send-command-timeout`.

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
(default `5151`). This is a deliberately minimal floor for local scripts and agents; it is
not a general-purpose web API. A **loopback** bind (`localhost` or a literal loopback IP —
`127.x.y.z`, `::1`, `[::1]`) needs no authentication and is canonicalized to a plain
loopback literal before binding ([#152]). A **non-loopback** bind is a deliberate off-box
exposure and starts only when `McpAuthToken` is set ([#143]); otherwise the listener
refuses to start and MCEC logs a loud error explaining what to change.

Over this HTTP transport, `send_command` requires `AgentCommandsEnabled=true` (see *Which gate applies
where*, above): enabling the floor alone does **not** expose the raw-command pass-through. To drive
`send_command` without opting into the agent surface, use the local stdio transport (`mcec.exe --mcp`).

### Front-door request validation (defeats CSRF and DNS rebinding)

A localhost HTTP service is still reachable by a browser: any web page the operator visits
can issue a cross-origin `POST` to `127.0.0.1:5151` (CSRF), and a DNS-rebinding attacker can
make the browser treat the endpoint as same-origin to read responses. To close both, every
HTTP request is validated **before** its body is read or any tool runs:

- **Method + path** — only `POST /mcp` is served; anything else is rejected (`405`/`404`).
- **`Host` header** — must be a loopback authority (`127.0.0.1`, `localhost`, or `[::1]`, and,
  if a port is present, the configured `McpHttpPort`). A request with `Host: evil.com` — the
  hallmark of DNS rebinding — is refused (`403`).
- **`Origin` header** — must be absent (a normal non-browser MCP client sends none) or a
  loopback origin. A cross-site `Origin` (`http://evil.com`) or an opaque `null` origin is
  refused (`403`), which stops the drive-by CSRF case.
- **`Authorization` (optional, defense in depth)** — set `McpAuthToken` to a non-empty secret
  and every request must carry `Authorization: Bearer <token>` (constant-time compared), which
  additionally protects against a hostile process on the same machine. Empty (default) relies on
  the `Host`/`Origin` checks above.

Every rejected request is logged with an `AGENT-AUDIT:` line (decision, method, path, host,
origin, remote endpoint) so drive-by and rebinding attempts are visible to the operator.

> **Binding off-box requires a token.** The `Host` check is a browser/rebinding defense, not a
> network control — a remote client can send `Host: 127.0.0.1`. So if `McpBindAddress` is set to a
> non-loopback address (e.g. `0.0.0.0`) **and** `McpAuthToken` is empty, MCEC **refuses to start** the
> HTTP listener and logs an error. To expose the door off-box, set a bearer token (and prefer a
> network-level control too).

The floor is hardened against resource exhaustion: a request body larger than
**1 MB** is refused with `413` (the cap is enforced by a bounded read, so chunked bodies
without a `Content-Length` can't bypass it), and at most **16** requests are served
concurrently — past that the server answers `503` rather than queueing.

[#151]: https://github.com/tig/mcec/issues/151
[#152]: https://github.com/tig/mcec/issues/152
[#143]: https://github.com/tig/mcec/issues/143

---

## Summary

- New, opt-in agent surface: `capture`, `query`, `displays`, `find`, `wait-for`, `invoke`, `launch`, `drag`, `click`, `record` (plus `send_command`, and `provision-session`/`end-session`).
- Structured `{ ok, result, error, … }` JSON result envelope; the commands are exposed as MCP/HTTP tools.
- **Three independent off-by-default gates:** `AgentCommandsEnabled`, per-command
  `Enabled`, and `McpServerEnabled` (localhost-bound).
- **HTTP front-door validation:** `POST /mcp` only, loopback `Host` and absent-or-loopback
  `Origin` required, optional `McpAuthToken` bearer token — defeats browser CSRF and DNS rebinding.
- Loud `AGENT-AUDIT:` logging for every agent action.
- Fully additive — nothing about the existing HTPC behavior changes.

## Agent safety features

Two operator-safety features build on the gates above — see
[`safety-emergency-stop-and-provisioning.md`](safety-emergency-stop-and-provisioning.md):

- **Emergency stop:** a global panic hotkey (default `Ctrl+Alt+Shift+S`, set via
  `EmergencyStopHotkey`) that instantly halts a session from any window — latching the actuation gate
  (`emergency-stopped` refusals until re-armed), aborting in-flight actuation, and releasing held input. It
  reacts to physical input only, so the agent can never trip or defeat it.
- **Isolated session provisioning:** `provision-session` (gated by `AllowSessionProvisioning`) hands
  an agent a disposable, isolated MCEC directory instead of it mutating the installed config; `end-session`
  (or launch-time reaping) tears it down.
