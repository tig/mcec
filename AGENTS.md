<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# AGENTS.md: driving (and testing) MCEC with an agent

MCEC 3.0 (the **Model Context Environment Controller**) gives an AI agent eyes, hands, and a safe
front door on Windows. This file is the **self-reinforcing guidance loop**: the canonical guidance an
agent needs, plus the recipe to *dogfood* it (drive MCEC through its own MCP server) so the guidance
stays honest. Each time MCEC changes, re-run the dogfood and refine the guidance below and in
`AgentServer.Instructions`.

## The built-in guidance (single source of truth)

The connect-time guidance an MCP client shows the model is authored in
[`src/Agent/AgentInstructions.md`](src/Agent/AgentInstructions.md). It is **embedded into the exe at build
time** and returned in the MCP `initialize` response (`result.instructions`) via `AgentServer.Instructions`,
which loads the embedded file and collapses each blank-line-separated paragraph to one line.

> Work the loop **observe → target → act → observe**.
> 1. **Target** a window by `window` (title substring), `process` (name without `.exe`), `className`,
>    or `foreground:true`. At least one is required; a call with no target fails by design.
> 2. **Observe**: `query` dumps the UI Automation tree (controlType, name, automationId, bounds,
>    state, value; bounded by `maxDepth` and `maxNodes`); `capture` returns a PNG (renders composited
>    WinUI/WPF surfaces correctly). **Check results before trusting them:** a `capture` with
>    `errorCategory: capture-blank` is a black/empty frame (minimized/cloaked/occluded/locked session);
>    restore or foreground the window and retry, don't trust the image; a `capture-fallback` warning
>    means PrintWindow was refused and the picture may be wrong; a `query` with `truncated:true` (a
>    `tree-truncated` warning) hit the node cap; raise `maxNodes` or target a deeper window. `warnings`
>    are non-fatal; `errorCategory` tells you how to recover. (Shape: `docs/design/agent-tool-result-contract.md`.)
> 3. **Act**: prefer `invoke` (`by` name/automationId/classname; `action` invoke|toggle|setvalue|
>    setfocus|expand|collapse|select) over coordinate clicks. `invoke` **fast-fails** if the control isn't present (it does not
>    wait), so `find`/`wait-for` the control first; an `invoke` that returns `no-target` means it hasn't
>    appeared yet; `wait-for` it rather than retrying blindly. Use `select` for TabItem/ListItem/RadioButton.
>    To **drag** (resize a window by its sizing border, move one by its title bar, or drag a slider/handle;
>    no `invoke` for these), use the `drag` tool: give a `from` and `to`, each an element `{ by, value }` or
>    an absolute screen pixel `{ x, y }`, plus optional `path` waypoints; the whole press-move-release is
>    dispatched **atomically** (prefer it over hand-rolling `mouse:lbd`/`mouse:mt`/`mouse:lbu`, which can
>    interleave with other input). To **click** a point `invoke` can't reach (a custom-drawn cell, a
>    canvas/map coordinate, a bare pixel), use the `click` tool. Before firing an app's own keyboard
>    shortcut at a specific surface (e.g. a MAUI GraphicsView), `focus` it first; keystrokes only reach the
>    foreground window's focused control. `send_command` is the raw escape hatch for any other MCEC command
>    (keystrokes, a single mouse action, launch). Re-`query` after acting; bounds have moved.
> 4. **Verify** with another `query`/`capture`.
>
> **Compose creatively.** Many tasks have no single dedicated tool; build them from primitives. Launch an
> app with the `launch` tool (preferred). Use `invoke` action:select for tabs/list/radios.
> Drag/resize/move with the `drag` tool; switch a tab by `invoke` select or `click`;
> record a window by passing its `query`'d bounds as the `record` region;
> wait on window state with the `windows` tool + a `timeout` and a `condition` (appears/disappears/foreground).
> A capable agent uses the *full* command set; reach for a raw
> `send_command` before concluding something can't be done.
>
**There is exactly one copy: edit that file.** It is the observe → target → act → observe playbook
(targeting; observation with `query`/`capture`/`record`/`displays`; `invoke`, `drag`, `click`, `focus` and
`send_command`; the result
envelope; creative composition of primitives; the on-screen overlay; and the security gates). Nothing here
to keep in sync.

## Security (do not regress)

Three independent, **off-by-default** gates; see [`docs/environment-controller.md`](docs/environment-controller.md):
`AgentCommandsEnabled` (the observation opt-in, separate from actuation), per-command `Enabled`, and
`McpServerEnabled` (HTTP floor, localhost-bound). Every agent action is logged with an `AGENT-AUDIT:`
line. An agent that hits "agent commands are disabled" should tell the user, not retry.

Three safety features layer on top (see [`docs/safety-emergency-stop-and-provisioning.md`](docs/safety-emergency-stop-and-provisioning.md)):

- **Emergency stop**: a global "dead man's switch" hotkey (default `Ctrl+Alt+Shift+S`) the operator
  can hit from **any** window to instantly halt a session: it latches the actuation gate (every tool call is
  refused with `emergency-stopped` until the operator re-arms), aborts in-flight actuation, and releases held
  input. It reacts to **physical input only** (injected keys are ignored via `LLKHF_INJECTED`), so the agent
  can never trip or defeat it. Do not weaken the injected-key filter or the latch.
- **Isolated session provisioning**: agents must **not** enable/disable commands in the installed
  instance (a crash leaks enabled gates). Instead, `provision-session` (gated by the operator's
  `AllowSessionProvisioning` opt-in, set from **File ▸ Settings ▸ Agent**) hands the agent a disposable
  directory with its own agent-ready config; teardown is deleting the directory, and MCEC reaps orphaned
  session dirs on launch. The installed config is never touched. That same Agent tab lists the provisioned
  instances, lets the operator delete any an agent leaves behind, and (with the opt-in ticked) has a
  **Provision new…** button that mints one and shows the handoff. So provisioning can bootstrap itself
  (#296): the installed `mcec.exe --mcp` serves the **bootstrap only** — just `provision-session` /
  `end-session`, every other tool refused with `bootstrap-only` — so a fresh install's agent has a first
  hop, while the full observe/act surface is still never served from Program Files. Keep the bootstrap
  restriction (`Program.ProvisioningBootstrapOnly`) and the meta-tools' own gates intact. The operator's
  **Provision new…** handoff dialog shows a two-step handoff: MCP client setup plus a ready-made agent
  briefing prompt (`ProvisionedInstanceDialog.BuildAgentPrompt`); the prompt points at the connect-time
  instructions rather than duplicating the playbook, keep it that way.
- **Command-access consent** (#307): a `command-disabled` refusal is recovered by the
  `request-command-access` meta-tool, never by editing config files. It shows the OPERATOR a consent
  dialog (`CommandAccessConsentDialog`) with three choices: allow these commands, allow these plus any
  later requests, or deny (sticky per instance). Grants are **in-memory only** (the loaded table's
  `Enabled` flags; never persisted), and the dialog is protected from the agent by three layers: the
  call holds `AgentRuntime.InputGate` while the prompt is up, dispatch refuses actuation-capable tools
  with `consent-pending` (`AgentToolExecutor.ServedWhileConsentPending` is an allow-list; keep it one),
  and the dialog registers itself with `WindowResolver` as never-a-target. Do not weaken any of the
  three, do not persist grants, and do not replace the dialog with MCP elicitation (that routes consent
  through the agent's own client; the party being constrained).

## Dogfood: test MCEC using MCEC (mcec drives mcec)

This is the proposal's success metric ("an MCP client mounts MCEC and completes a multi-step GUI
task"), encoded as a real, opt-in test: **MCEC drives MCEC** end-to-end with genuine keyboard + mouse
input; no `StartProcess` launch crutch.

It lives in [`tests/MCEControl.xUnit/Integration/AgentDesktopE2ETests.cs`](tests/MCEControl.xUnit/Integration/AgentDesktopE2ETests.cs).
Because it drives the **real desktop** (global keystrokes, mouse, launching apps), it is **skipped
unless `MCEC_DESKTOP_E2E=1`**; `dotnet test`/CI never touch your desktop. Run it deliberately on an
interactive session:

```
dotnet build src/MCEControl.csproj -c Debug
$env:MCEC_DESKTOP_E2E=1 ; dotnet test --filter Category=DesktopE2E
```

What it does, all via the `mcec.exe --mcp` server's `send_command`/`query`/`capture` tools:

1. **Launch via the Win+R Run dialog (keyboard):** `send_command winr` (Win+R) → `chars:<path to
   mcec.exe>` → `enter`. This starts a *second* MCEC purely through keyboard input.
2. **Open Help ▸ About (mouse + keyboard):** `query` the new window's UIA tree to get the **Help**
   menu's bounding rect → `mouse:mt,<x>,<y>` + `mouse:lbc` to **mouse-click** it → `key_a` to press
   **A** and open About. (Pixel coords from UIA are normalized to InputSimulator's 0..65535 space
   using the physical primary-display size.)
3. **Verify:** `capture {window:"About"}` returns a PNG image block; `query {window:"About"}` returns
   the dialog's UIA tree (`Window:About`, the version text, `License Agreement`, `Button:OK`).
4. **Graceful shutdown:** `key_esc` dismisses About (its OK button is the CancelButton) → `alt_f`
   opens the File menu → `key_x` chooses **E&xit**, so the controlled instance runs its normal
   `ShutDown()` (saves settings, closes) rather than being killed. The test asserts the window is gone.

The actuation commands it relies on are enabled in a temporary `mcec.commands` it writes next to the
exe (`winr`, `chars:`, `enter`, `mouse:`, `key_a`, `key_esc`, `alt_f`, `key_x`) and removed afterward.

### Operational tips (learned the hard way)

- Give the controlled copy `ActAsServer=false`; otherwise its socket-server bind raises a **Windows
  Firewall** prompt that steals foreground and eats the keystrokes.
- A freshly launched copy must be the **foreground** window for menu keystrokes to land; injected
  Win+R is reliable, but let the Run dialog focus before typing the path.
- MCEC is a *WinExe*, so a console pipeline (`Get-Content | exe`) won't reliably capture its stdout;
  spawn it with explicit inherited pipe handles (the test uses `Process` with `RedirectStandardInput/Output`).

### Bugs this dogfood surfaced (now fixed)

- `SetStatus` set `NotifyIcon.Text` (a 127-char cap) to the full informational version → crashed GUI
  startup on long prerelease version strings. Now capped.
- `.commands` loading called `new Version(informationalVersion)` (throws on `x.y.z-branch+sha`) and
  popped `MessageBox` prompts that would hang the headless `--mcp` process. Now uses
  `System.Version.TryParse` and suppresses load-path dialogs when `AgentRuntime.Headless`.

## Dogfood: WinPrint hero GIF

> The worked examples (the mcec hero GIF, this WinPrint one, and prompt demos like Paint → smiley → email)
> are cataloged for humans and agents in [`docs/examples.md`](docs/examples.md), with a template for adding more.

MCEC driving installed **WinPrint** is a marquee dogfooding example, but the WinPrint hero GIF
(`hero-gui-win.gif`) is **owned and produced in the [winprint](https://github.com/tig/winprint) repo**,
agent-driven via the generic [`scripts/Generate-HeroGif.ps1`](scripts/Generate-HeroGif.ps1) controller
(no producer script lives here). Follow winprint's authoritative recipe
[`docs/hero-gif-win.md`](https://github.com/tig/winprint/blob/develop/docs/hero-gif-win.md); it records a
clean, **window-only, overlay-off** hero. mcec-side conveniences that recipe still hand-configures (a
bootstrap `-NoOverlay` switch, default-enabling `windows`/`right`/`down`/`enter`/`run`) are tracked as
controller-bootstrap follow-ups.

## Working in this repo

- **Agent-facing guidance is part of "Done": not optional, not "later."** Any change to how an agent
  observes/targets/acts (a new tool, arg, failure mode, warning/error category, or driving technique)
  MUST update the connect-time playbook in
  [`src/Agent/AgentInstructions.md`](src/Agent/AgentInstructions.md) (the single source of truth, embedded
  into the exe and served as `AgentServer.Instructions`) in the same change. Updating a per-tool
  `Tool(...)` description is **not** a substitute (those describe *args*); the instructions teach *recovery
  and strategy*. Read the trigger by principle, not keyword: it fires on feature work, not just
  dogfooding. Treat it like updating tests.
- Build is strict: `Nullable=enable`, `TreatWarningsAsErrors=true`, and house analyzers **MCEC0001
  (one top-level type per file)** / **MCEC0002 (no nested types)**. New code must be warning-clean.
- Agent subsystem lives in `src/Agent/` + the MCP server in `src/Services/` (`AgentServer.cs` is a
  thin static facade over `McpStdioTransport`/`McpHttpTransport`/`JsonRpcDispatcher`/
  `AgentToolExecutor`); commands plug into the existing `Command`/`CommandInvoker` pattern.
  Dev notes: [`docs/agent-server-architecture.md`](docs/agent-server-architecture.md).
- Tests: `dotnet test tests/MCEControl.xUnit/MCEControl.xUnit.csproj`.
