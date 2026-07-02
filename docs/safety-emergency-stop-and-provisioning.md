<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Agent safety: Emergency Stop and Isolated Session Provisioning

Two related safety features for MCEC's Agent Mode. When MCEC is agent-driving the desktop, the **target
app has focus, not MCEC**; so the operator needs (a) a way to instantly intervene when a run goes wrong,
and (b) assurance that a session can't leave the machine in an unsafe state. Both features exist so the
operator stays in control.

---

## 1. Emergency Stop: a global dead-man's-switch hotkey

### Goal

A single keystroke, hittable from **any** focused window, that instantly halts the active agent session;
the human override. The agent must not be able to trip it accidentally or defeat it deliberately.

### The hotkey

- **Default: `Ctrl+Alt+Shift+S`** (mnemonic **S**top); a four-key chord no app uses and the agent never
  synthesizes, so accidental triggering is near-zero.
- **Configurable** via `AppSettings.EmergencyStopHotkey` (a `+`-separated spec, e.g. `Pause`, `Ctrl+Alt+Q`).
  Parsed by [`EmergencyStopHotkey`](../src/Agent/EmergencyStopHotkey.cs); left/right modifier variants
  collapse to `ctrl`/`alt`/`shift`/`win`.

### What "stop" does

Engaging the stop (`EmergencyStop.Trigger`):

1. **Latches the actuation gate.** `AgentRuntime.EmergencyStopped` is set; `AgentServer.CallTool` refuses
   **every** tool call (actuation, observation, and raw `send_command`) with the distinct
   `emergency-stopped` error until re-armed.
2. **Aborts in-flight actuation.** Any recording is stopped; an in-flight `drag` observes the latch between
   waypoints and bails out (releasing the button). The `invoke` modal-grace worker runs a synchronous UIA
   call that can't be safely aborted mid-flight, but the latch refuses every follow-up and the input release
   neutralizes anything left held.
3. **Releases held input.** All mouse buttons up; shift/ctrl/alt/win reset (the same reset `MainWindow`
   runs on exit); no stuck drag or chord.
4. **Loud feedback.** A persistent red `⛔ STOPPED by operator` banner on the overlay, an
   `AGENT-AUDIT:` log line, a status-bar message, and a stamp into the `AgentSession` record.
5. **Latches until re-armed.** Re-arming is always a deliberate operator action; the latch is never
   cleared automatically, and it deliberately has no MCP surface (an agent that could re-arm itself
   would defeat the human override). GUI host: the **⛔ Re-arm (Emergency Stop)** menu item (visible
   only while stopped). Headless `--mcp`: a modal re-arm prompt (`EmergencyStopRearmDialog`) opens the
   moment the stop engages; **Re-arm** resumes, **Leave stopped** (also Enter/Esc/close; the fail-safe
   default) keeps the latch, and pressing the chord again reopens the prompt
   (`EmergencyStop.Retriggered`). The prompt is safe as a re-arm surface because the latch refuses
   every tool call while it is up, so only physical input can reach its buttons.

Step 1 (the latch) is the only work performed inside the low-level hook callback; steps 2–4 run
immediately after on a background thread. A slow log or serial write can therefore never stall the
`WH_KEYBOARD_LL` callback past `LowLevelHooksTimeout`, which would make Windows silently evict the hook;
and the panic hotkey with it. Nothing actuates in the gap: the latch is already set, so every tool call is
refused before the background steps even run.

### Why it can't be tripped or defeated by the agent

MCEC's own agent actuation **injects** keystrokes and mouse input. Windows flags injected low-level events
with `LLKHF_INJECTED`. The emergency stop reuses MCEC's existing global `WH_KEYBOARD_LL` hook
([`HookManager`](../src/Hooks/HookManager.cs)) and reacts to **physical input only**;
injected key events never arm a modifier and never trigger. This is what makes the hotkey a true human
override rather than something the agent could press or hold to defeat.

### Design / where it lives

| Concern | Location |
| --- | --- |
| Injected-flag surfaced on a physical-key event | `HookManager.Callbacks.cs` → `GlobalKeyEventArgs` (`KeyDownExt`/`KeyUpExt`) |
| Chord state machine (pure, unit-tested) | [`EmergencyStopDetector`](../src/Agent/EmergencyStopDetector.cs) |
| Hotkey parsing | [`EmergencyStopHotkey`](../src/Agent/EmergencyStopHotkey.cs) |
| Hook wiring + trigger/re-arm orchestration | [`EmergencyStop`](../src/Agent/EmergencyStop.cs) |
| The latch | `AgentRuntime.EmergencyStopped` |
| Actuation-gate refusal | `AgentServer.CallTool` (`emergency-stopped`) |
| Cooperative drag abort | `MouseCommand.PerformDrag` |
| Overlay banner | `CommandOverlayWindow` |
| Re-arm affordance + host lifecycle (GUI) | `MainWindow` (`Start`/`Stop`, `SetUpEmergencyStopUi`) |
| Re-arm affordance + host lifecycle (headless) | [`HeadlessOperatorUi`](../src/Agent/HeadlessOperatorUi.cs) + [`EmergencyStopRearmDialog`](../src/Agent/EmergencyStopRearmDialog.cs) |

The LL hook needs a message loop on its installing thread, so each host arms it where one pumps: the
**GUI host** arms on the UI thread whenever `EmergencyStopEnabled` and the agent front door could be
driving (`McpServerEnabled || AgentCommandsEnabled`); **headless `--mcp`** arms on
`HeadlessOperatorUi`'s dedicated STA pump thread whenever `EmergencyStopEnabled` (the stdio transport
is always a live front door, so there is no `McpServerEnabled` qualifier). That same headless pump
thread hosts the command overlay, so a headless session narrates and shows the `⛔ STOPPED` banner
exactly like the GUI host.

### Limitations / follow-ups

- **Elevated targets / secure desktop (UIPI).** A non-elevated MCEC's LL hook won't receive keys while an
  **elevated** window is foreground, and never on the secure desktop (UAC/lock). If MCEC drives elevated
  apps, run MCEC elevated too. Documented, not solved here.
- **Non-interactive headless launches.** If `--mcp` is started without an interactive desktop (a
  service, a disconnected session), the hook and the windows are refused; `HeadlessOperatorUi` logs
  each piece and skips it, and the protocol serves normally. That is also the configuration where no
  actuation could reach a desktop anyway.

---

## 2. Isolated session provisioning

### Problem

Historically an agent would `Start-Process` the **installed** MCEC, flip `AgentCommandsEnabled=true` and
enable the commands it needs in the installed `mcec.commands`, then disable them at the end. That mutates
the operator's config and (because "disable at the end" only runs on the happy path) **leaks enabled
security gates** on any crash/timeout/kill. It also can't compose across concurrent sessions.

### Solution

MCEC owns the isolation. `provision-session` hands an authorized agent a fresh, disposable directory
containing `mcec.exe` + dependencies and a **co-located, agent-ready config** (agent commands enabled
**only** inside the copy). The agent runs from there and deletes it when done; so enabled state lives only
in the throwaway copy, "cleanup" is `rm -rf <dir>`, and a crashed session leaves the real install untouched.

The installed copy **enforces** its side of this: `mcec.exe` running from Program Files refuses
`mcp`/`--mcp` and refuses to start the MCP/HTTP endpoint (`Program.IsProgramFilesInstall`), with an
error pointing at provisioning or a manual writable copy. The operator's install cannot be turned into
an agent server even by editing its settings.

### Flow

1. Operator opts in once: `AppSettings.AllowSessionProvisioning = true` (the one thing that can't be
   self-served, or the isolation is theater).
2. Agent calls `provision-session` (optional `mcpServer`, `commands`). MCEC
   ([`SessionProvisioner`](../src/Agent/SessionProvisioner.cs)):
   - creates `%LOCALAPPDATA%\MCEC\sessions\<id>`,
   - **copies the binaries** from the running exe's dir (excluding the installed `mcec.settings` /
     `mcec.commands` / `*.log`),
   - writes a co-located `mcec.settings` (`AgentCommandsEnabled=true`, `ActAsServer=false` to avoid the
     firewall prompt, MCP bound to a free loopback port, `AllowSessionProvisioning=false` so it can't
     re-provision, and `McpAuthToken=<token>`; see below) and a `mcec.commands` enabling the requested
     agent commands,
   - returns `{ sessionId, directory, exePath, mcpEndpoint?, token, launch, teardown }`.
3. Agent runs `mcec.exe` from `directory` and drives it. Over the session's HTTP endpoint every
   request must carry `Authorization: Bearer <token>` (the instance's own bearer-token gate enforces its
   configured `McpAuthToken`); the stdio transport is process-ownership-authenticated and needs no header.
4. Agent calls `end-session { sessionId, token }` (after stopping the session's exe) → the directory is
   deleted.

### The session token

`token` is the **session credential**, doing real work on both surfaces:

- **Connect:** it is the provisioned instance's `McpAuthToken`, so the session's localhost MCP/HTTP
  endpoint refuses requests without the matching `Authorization: Bearer` header; another local
  process can't hijack a session it didn't provision.
- **Teardown:** `end-session` validates the presented token against the session's **co-located**
  config (`SessionProvisioner.ValidateTeardownToken`) before deleting anything. `end-session` is
  deliberately *not* behind the `AllowSessionProvisioning` gate (teardown must always be possible),
  so without the token any MCP caller could delete any session it could name. A wrong/missing token
  is refused with `error.code:session-token-invalid`; an already-gone session stays idempotent
  success. Validation is fail-closed: if the session's config can't be read, the token can't be
  verified and teardown is refused; the age-based reaper still collects the directory later.

The credential lives *in the session directory itself* (never in the installed config), so it
survives installed-instance restarts and is retired the moment the directory is deleted.

### Isolation approach: copy binaries

We copy the binaries into the session directory (rather than a `--config-dir` redirect of the installed
exe). Because the copy runs from a non-`Program Files` location, `Program.ConfigPath` resolves to the
session directory itself, so it reads its own co-located config with no launch flags. This matches the
existing manual practice (`scripts/Run-Customer0Skeleton.ps1`) and is isolated by construction.

### Teardown & reaping

- **Explicit:** `end-session` (or just deleting the directory).
- **Belt-and-suspenders:** on every launch (`Program.Main`) and before each provision, MCEC reaps session
  directories older than `AgentServer.SessionReapAgeHours` (12h). A **running** session's files are locked,
  so a live session is never reaped; it's collected on a later launch after it exits.

### Design / where it lives

| Concern | Location |
| --- | --- |
| Provision / teardown / reap | [`SessionProvisioner`](../src/Agent/SessionProvisioner.cs) |
| Handoff descriptor | [`ProvisionedSession`](../src/Agent/ProvisionedSession.cs) |
| MCP tools + authorization gate | `AgentServer` (`provision-session`, `end-session`) |
| Operator opt-in | `AppSettings.AllowSessionProvisioning` |
| Reap on launch | `Program.Main` |

### Limitations / follow-ups

- **Authorization depth.** `AllowSessionProvisioning` is a single operator opt-in. A richer per-session
  authorization / token-scoping model is future work.
- **Auto-launch.** `provision-session` returns a directory + how to launch; it does not spawn the process
  (the caller/host does). An optional MCEC-side launch is a candidate follow-up.
- **Artifacts** (evidence bundles) live under the session directory so teardown collects them.

