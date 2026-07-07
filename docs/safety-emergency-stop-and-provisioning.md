<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Agent safety: Emergency Stop, Isolated Session Provisioning, and Command-Access Consent

Three related safety features for MCEC's Agent Mode. When MCEC is agent-driving the desktop, the **target
app has focus, not MCEC**; so the operator needs (a) a way to instantly intervene when a run goes wrong,
(b) assurance that a session can't leave the machine in an unsafe state, and (c) a way to grant an agent
more capability mid-session without ever letting it grant itself. All three exist so the operator stays
in control.

## How to use this in practice

If you are using MCEC from a desktop agent app, the recommended flow is:

1. Turn on **Allow agents to provision disposable instances** on **File ▸ Settings ▸ Agent**. This keeps
   the agent on a throwaway copy of MCEC rather than your installed instance.
2. Provision a fresh session (either by clicking **Provision new…** or by letting the agent call
   `provision-session` over the bootstrap surface). MCEC gives the agent the launch command, the session
   token, and the teardown instructions.
3. Point your agent app at the provisioned instance's `mcec.exe mcp` (or its HTTP endpoint if you enabled
   one). The agent can then use the normal agent control tools, while the safety gates remain in
   effect.
4. If the agent needs a command that is disabled, it asks for it with `request-command-access`; you can
   approve it for that instance, approve it plus later requests, or deny it.
5. If the run goes wrong, press the emergency-stop hotkey. When you are done, end the session or delete
   the provisioned instance.

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
4. **Loud feedback.** A persistent red `⛔ STOPPED by operator` banner on the overlay (replacing the
   routine centered `MCEC is controlling your PC` banner it shows while running), an
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

### Limitations

- **Elevated targets / secure desktop (UIPI).** A non-elevated MCEC's LL hook won't receive keys while an
  **elevated** window is foreground, and never on the secure desktop (UAC/lock). If MCEC drives elevated
  apps, run MCEC elevated too. Documented, not solved here.
- **Non-interactive headless launches.** If `--mcp` is started without an interactive desktop (a
  service, a disconnected session), the hook and the windows are refused; `HeadlessOperatorUi` logs
  each piece and skips it, and the protocol serves normally. That is also the configuration where no
  actuation could reach a desktop anyway.

---

## 2. Isolated session provisioning

Isolated session provisioning gives an authorized agent a fresh, disposable copy of MCEC to drive instead
of the operator's installed instance. `provision-session` hands the agent a throwaway directory containing
`mcec.exe` + dependencies and a **co-located, agent-ready config** (agent commands enabled **only** inside
that copy). The agent runs from there and deletes it when done, so any enabled state lives only in the
throwaway copy, "cleanup" is `rm -rf <dir>`, and a crashed or killed session leaves the real install
untouched. Concurrent sessions each get their own directory and never contend.

The installed copy **enforces** its side of this: `mcec.exe` running from Program Files never serves the
full agent surface. It refuses to start the MCP/HTTP endpoint (`AgentServer.StartHttp` →
`Program.IsProgramFilesInstall`), and over `--mcp` it serves **only the provisioning bootstrap** (see
below). The operator's install cannot be turned into an observation/actuation agent server even by
editing its settings.

### Bootstrapping: how a fresh install gets its first instance (#296)

There is a chicken-and-egg to close. `provision-session` is itself an MCP tool, so an agent must reach
*some* MCEC MCP server to call it; but a fresh install's own copy refuses to serve agents, and a new
user has no other copy yet. Two first hops resolve it, neither requiring the operator to hand-edit a
config:

- **Operator-initiated (GUI).** The **Agent** tab's **Provision new…** button calls
  `SessionProvisioner.Provision()` directly (no MCP round-trip) and shows a two-step handoff
  ([`ProvisionedInstanceDialog`](../src/Dialogs/ProvisionedInstanceDialog.cs)): step 1, the MCP client
  setup (the `mcec.exe --mcp` launch line and, when enabled, the HTTP endpoint + bearer token); step 2,
  a ready-made **briefing prompt to paste to the agent** carrying the session id, directory, token
  custody, the rules of engagement (ask for disabled commands via `request-command-access`, never edit
  config files, stop on `emergency-stopped`), and the teardown duty. Each step has its own copy
  button. Enabled only when the opt-in below is ticked.
- **Bootstrap MCP mode (headless).** An agent may connect to the **installed** `mcec.exe --mcp`; but
  from the install it serves a restricted surface: `tools/list` and dispatch expose **only**
  `provision-session` and `end-session`, and every other tool is refused with `error.code:bootstrap-only`
  (the connect-time `instructions` say so). That is enough to mint a disposable instance and no more; the
  installed config is never touched because neither meta-tool observes or actuates. Gated, as always, by
  `AllowSessionProvisioning`. See `Program.ProvisioningBootstrapOnly`, `JsonRpcDispatcher` (restricted
  `tools/list` + bootstrap `instructions`), and `AgentToolExecutor.CallTool` (the `bootstrap-only` gate).

Either way the agent gets back a **non-installed** disposable copy that serves the full tool surface, and
drives *that*.

### Flow

1. Operator opts in once: the **Allow agents to provision disposable instances** checkbox on the
   Settings dialog's **Agent** tab (`AppSettings.AllowSessionProvisioning`); the one thing that can't be
   self-served, or the isolation is theater. This is the sole opt-in a non-technical operator performs
   to let an agent drive MCEC; it grants access only to a disposable copy, never this installed instance.
   The tab refuses to toggle it from inside a provisioned copy, so a driving agent can never widen its
   own permissions.
2. A session is created, either by the operator's **Provision new…** button or by an agent calling
   `provision-session` (optional `mcpServer`, `commands`) over the bootstrap MCP surface. MCEC
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
session directory itself, so it reads its own co-located config with no launch flags. This is isolated by
construction, and supersedes the earlier hand-rolled "copy the build and write a config" practice.

### Teardown & reaping

- **Explicit:** `end-session` (or just deleting the directory).
- **Operator:** the Settings dialog's **Agent** tab lists every provisioned instance (id, age, size,
  running/stale), offers per-row **Delete** and **Delete all**, and (with the opt-in ticked)
  **Provision new…** to create one, so the operator can create and clean up copies without hunting
  through `%LOCALAPPDATA%`. Deleting is the same directory removal `end-session` and the reaper
  perform; a running instance is locked and skipped (stop it first).
- **Belt-and-suspenders:** on every launch (`Program.Main`) and before each provision, MCEC reaps session
  directories older than `AgentServer.SessionReapAgeHours` (12h). A **running** session's files are locked,
  so a live session is never reaped; it's collected on a later launch after it exits.

### Design / where it lives

| Concern | Location |
| --- | --- |
| Provision / teardown / reap | [`SessionProvisioner`](../src/Agent/SessionProvisioner.cs) |
| Handoff descriptor | [`ProvisionedSession`](../src/Agent/ProvisionedSession.cs) |
| MCP tools + authorization gate | `AgentServer` (`provision-session`, `end-session`) |
| Bootstrap-only MCP surface (#296) | `Program.ProvisioningBootstrapOnly`, `JsonRpcDispatcher` (restricted `tools/list` + `BootstrapInstructions`), `AgentToolExecutor.CallTool` (`bootstrap-only`) |
| Operator opt-in + management UI | Settings dialog **Agent** tab ([`AgentSettingsTab`](../src/Dialogs/SettingsTabs/AgentSettingsTab.cs)) → `AppSettings.AllowSessionProvisioning` |
| Operator "Provision new…" handoff (#296) | [`ProvisionedInstanceDialog`](../src/Dialogs/ProvisionedInstanceDialog.cs) |
| Session enumeration for the UI | `SessionProvisioner.ListSessions` |
| Reap on launch | `Program.Main` |

### Limitations

- **Authorization depth.** `AllowSessionProvisioning` is a single operator opt-in; there is no per-session
  authorization or token-scoping.
- **Auto-launch.** `provision-session` returns a directory and how to launch it; it does not spawn the
  process (the caller/host does).
- **Artifacts.** Evidence bundles live under the session directory, so teardown collects them.

---

## 3. Command access by operator consent (`request-command-access`)

A provisioned session enables the standard agent tool set; everything else (`launch`, raw built-ins like
`chars:` or `shutdown`, user-defined commands) starts disabled, and any tool or `send_command` that hits
one is refused with `error.code:command-disabled`. The legitimate mid-session acquisition path is the
`request-command-access` meta-tool: the agent names the command(s) and a one-line reason, and MCEC shows
the **operator** a consent dialog ([`CommandAccessConsentDialog`](../src/Dialogs/CommandAccessConsentDialog.cs))
on their desktop; GUI host on the main window's thread, headless `--mcp` on `HeadlessOperatorUi`'s pump
thread. The agent's call blocks for the answer. Three choices:

1. **Allow these commands**; enables exactly the requested names.
2. **Allow these + any later requests**; also auto-approves this instance's future requests (the agent
   still asks per command, and every grant is audit-logged and narrated on the overlay).
3. **Deny** (the default button; Esc, the close box, and the ~2-minute timeout all deny). A deny is
   **sticky** for the instance: re-requesting a denied command returns `consent-denied` without a
   prompt, so an agent cannot nag the operator into approval. An **emergency stop** engaging while
   the prompt is open dismisses it too, but that is the operator halting the session, not answering
   the question: it is reported as `emergency-stopped`, records no deny, and the command stays
   grantable after re-arm.

### Why the agent can't answer its own prompt

The agent being asked can synthesize input and drive UIA on the same desktop, so the dialog is protected
by three layers:

1. The call holds the **input gate** (`AgentRuntime.InputGate`) for the prompt's whole lifetime, so no
   queued or synthesized keyboard/mouse input can land while it is up.
2. Dispatch freezes everything except the flagged observation tools (`ToolDescriptor.ServedDuringConsent`;
   a new tool defaults to frozen) with `consent-pending` while a prompt is open. The freeze runs before
   the meta-tool branches, so it covers `invoke` (UIA-pattern actuation that deliberately does not take
   the input gate) **and** `provision-session` (which could otherwise mint a second, unfrozen instance
   to drive the dialog with).
3. The dialog registers itself with `WindowResolver` as never-a-target (the overlay's mechanism), so
   in-process targeting can never resolve it.

Residual risk: a *second* MCEC instance driven by the same agent shares none of these; the audit log and
overlay narration are the mitigations there.

### Why grants are in-memory only

A grant flips the command's `Enabled` flag on the **loaded table** of the running instance; nothing is
written to `mcec.commands` or `mcec.settings`, and `CommandInvoker.Save` serializes consent-granted
commands as **disabled**, so even a later Commands-window "Save changes" cannot quietly persist an
agent's grant. A leaked session directory therefore never carries
permissions beyond its provisioned defaults, a respawned instance resets and must ask again, and the
co-located files remain a faithful record of what *provisioning* granted (the audit log holds what
*consent* granted). Consent is deliberately MCEC's own out-of-band dialog, never MCP elicitation, which
would route the question through the agent's own client; the party being constrained.

### Design / where it lives

| Concern | Location |
| --- | --- |
| Consent state (single-flight, standing grant, sticky denies) | [`AgentConsent`](../src/Agent/AgentConsent.cs) |
| The consent dialog | [`CommandAccessConsentDialog`](../src/Dialogs/CommandAccessConsentDialog.cs) |
| Tool dispatch, gates, and the consent-pending freeze | `AgentToolExecutor` (`request-command-access`, `ConsentPendingRefusal`) |
| GUI prompt channel | `MainWindow.PromptCommandAccess` |
| Headless prompt channel | `HeadlessOperatorUi.PromptCommandAccess` |

