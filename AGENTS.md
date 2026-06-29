<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# AGENTS.md — driving (and testing) MCEC with an agent

MCEC 3.0 (the **Model Context Environment Controller**) gives an AI agent eyes, hands, and a safe
front door on Windows. This file is the **self-reinforcing guidance loop**: the canonical guidance an
agent needs, plus the recipe to *dogfood* it — drive MCEC through its own MCP server — so the guidance
stays honest. Each time MCEC changes, re-run the dogfood and refine the guidance below and in
`AgentServer.Instructions`.

## The built-in guidance (single source of truth)

The guidance an MCP client surfaces to the model lives in code, in
[`src/Services/AgentServer.cs`](src/Services/AgentServer.cs) as `AgentServer.Instructions`, and is
returned in the MCP `initialize` response (`result.instructions`). Keep that string and this section
in sync. In short:

> Work the loop **observe → target → act → observe**.
> 1. **Target** a window by `window` (title substring), `process` (name without `.exe`), `className`,
>    or `foreground:true`. At least one is required — a call with no target fails by design.
> 2. **Observe** — `query` dumps the UI Automation tree (controlType, name, automationId, bounds,
>    state, value); `capture` returns a PNG (renders composited WinUI/WPF surfaces correctly).
> 3. **Act** — prefer `invoke` (`by` name/automationId/classname; `action` invoke|toggle|setvalue|
>    setfocus) over coordinate clicks. Use `find`/`wait-for` to wait for a control. `send_command`
>    sends any raw MCEC command (keystrokes, mouse, launch).
> 4. **Verify** with another `query`/`capture`.

## Security (do not regress)

Three independent, **off-by-default** gates — see [`docs/agent-server.md`](docs/agent-server.md):
`AgentCommandsEnabled` (the observation opt-in, separate from actuation), per-command `Enabled`, and
`McpServerEnabled` (HTTP floor, localhost-bound). Every agent action is logged with an `AGENT-AUDIT:`
line. An agent that hits "agent commands are disabled" should tell the user, not retry.

## Dogfood recipe — test MCEC using MCEC

This is the proposal's success metric ("an MCP client mounts MCEC and completes a multi-step GUI
task"). It runs headless and needs a desktop session.

1. **Build:** `dotnet build src/MCEControl.csproj -c Debug`
2. **Enable the gate** in the build output's `MCEControl.settings`:
   `<AgentCommandsEnabled>true</AgentCommandsEnabled>`
3. **Open a target** (e.g. Notepad) so there is a real window to drive.
4. **Drive the MCP stdio server.** MCEC is a *WinExe*, so a console-pipeline (`Get-Content | exe`)
   will not reliably capture its redirected stdout — launch it with explicit inherited pipe handles
   (e.g. Python `subprocess.Popen([exe, "--mcp"], stdin=PIPE, stdout=PIPE)`), then send newline-
   delimited JSON-RPC: `initialize`, `tools/list`, `tools/call {name:"query", arguments:{process:"notepad"}}`,
   `tools/call {name:"capture", arguments:{process:"notepad"}}`.
5. **Verify:** `initialize.result.instructions` is non-empty; `tools/list` lists
   `capture/query/find/invoke/send_command`; `query` returns a multi-node UIA tree; `capture` returns
   an `image` content block whose base64 decodes to bytes starting with the PNG signature
   `89 50 4E 47` and renders the window (not black).

### Last validated

Driving Windows 11's modern **Notepad** (a WinUI 3 / DirectComposition app — the case plain screen
grabs return black for): `query` returned a 42-node UIA tree; `capture` returned a valid 1094×691 PNG
that rendered the toolbar, tab, syntax-highlighted text, and status bar correctly. This confirms the
`PrintWindow(PW_RENDERFULLCONTENT)` path and the gating both work end-to-end.

## Working in this repo

- Build is strict: `Nullable=enable`, `TreatWarningsAsErrors=true`, and house analyzers **MCEC0001
  (one top-level type per file)** / **MCEC0002 (no nested types)**. New code must be warning-clean.
- Agent subsystem lives in `src/Agent/` + `src/Services/AgentServer.cs`; commands plug into the
  existing `Command`/`CommandInvoker` pattern. Dev notes:
  [`docs/agent-server-architecture.md`](docs/agent-server-architecture.md).
- Tests: `dotnet test tests/MCEControl.xUnit/MCEControl.xUnit.csproj`.
