<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Agent Server — Architecture Notes (dev)

This is a short developer-facing tour of the MCEC 3.0 agent subsystem. It lives under
`src/Agent/` and is **additive**: it reuses the existing `Command` / `CommandInvoker`
pattern and adds no breaking changes to any existing command, transport, or default.

## Layering

```
  MCP client ──stdio──┐
  HTTP POST  ──:5151──┤
                      ▼
              AgentServer  (MCP/HTTP façade, tool dispatch)
                      │  builds command lines / CommandResult
                      ▼
              CommandInvoker  (existing)
                      │  resolves + Execute()s
                      ▼
   capture / query / displays / find / wait-for / invoke /
   drag / click / record / launch   (the ToolCatalog set — new Commands)
        │            │           │
   ScreenCapture  WindowResolver  UiaService
   (PrintWindow)  (Win32 enum)    (FlaUI / UIA3)
        └──────── AgentNativeMethods (P/Invoke) ────────┘
```

## Key seams

### `AgentRuntime` (static seam)
The single ambient hook the commands and server talk to:
`Settings` (the `AppSettings`), `Invoker` (the `CommandInvoker`),
`AgentCommandsEnabled` (derived from `AppSettings.AgentCommandsEnabled`), and
`Audit(action, detail)` which emits the `AGENT-AUDIT:` log line. Keeping this static
and tiny lets both the WinForms host and the `--mcp` headless bootstrap share one
configuration/gating point without dependency plumbing.

It also holds the host-capability half of the seam (#209): `Host` is a small `IAppHost`
each host registers — `MainWindow` in GUI mode (in its settings-apply path, alongside
`Settings`), `HeadlessAppHost` in `--mcp` mode. Engine code calls the static wrappers:

- `SendLine(line)` — outbound line to all connected transports (GUI: `MainWindow.SendLine`;
  headless/no host: logged no-op).
- `RequestShutdown()` — orderly app shutdown (GUI: `MainWindow.ShutDown()`, self-marshaled;
  headless: deferred clean process exit after in-flight replies flush, so `mcec:exit` over
  MCP actually exits; no host: logged no-op so tests survive strays).
- `MessageWindowHandle` — for `RegisterPowerSettingNotification` and the like (GUI: the
  `MainWindow` handle; headless: throws — the activity monitor never runs headless).

This is what makes "no `MainWindow.Instance` below the UI layer" enforceable:
`MainWindow.Instance` is explicitly assigned by `Program`'s GUI path (no lazy
construct-on-touch), and any touch before assignment — or ever, headless — throws a
pointed exception naming the seam instead of silently constructing a Form.

### `AgentNativeMethods` (P/Invoke)
Internal static P/Invoke surface (carries the required CA1060 suppression).
Provides `PrintWindow` with `PW_RENDERFULLCONTENT` (captures occluded/composited
windows), `GetWindowRect`, `GetForegroundWindow`, `IsWindow`, and DWM
`DwmGetWindowAttribute` with `DWMWA_EXTENDED_FRAME_BOUNDS` for accurate, shadow-free
bounds. Geometry is marshalled through the `NativeRect` struct (exposes `Width`/`Height`).

### `WindowResolver`
Turns loose selectors (`handle?`, `title?`, `processName?`, `className?`, `foreground`)
into a concrete window. `Resolve(...)` returns a `WindowInfo?`; `EnumerateTopLevel()`
lists candidates for `find`; `Describe(hwnd)` builds a `WindowInfo`. `WindowInfo`
carries handle/title/class/process/PID/bounds and serializes via `ToJsonObject()`.

### `ScreenCapture`
Renders a resolved window to a bitmap via `AgentNativeMethods.PrintWindow` and encodes
it as PNG (base64) for the `capture` result. Uses `using` for all GDI/`IDisposable`
resources.

### `UiaService` (FlaUI)
UI Automation access (UIA3 via FlaUI) backing element-level `find` / `wait-for`
queries. Isolated behind a service so the rest of the subsystem has no hard FlaUI
coupling at the command layer.

### `ToolCatalog` / `ToolDescriptor` (#205)
The single registry of the gated agent tools. Each `ToolDescriptor` carries the tool's
name, its `tools/list` schema, its MCP-arguments→`Command` mapping, its overlay
tersifier, and its policy flags (`SerializesOnInput`, `IsObservation`,
`ProvisionedByDefault`). The former hand-synced switch/list sites — `AgentServer`'s
schema builder, tools/call gate whitelist, and `BuildCommand` mapping,
`SerializesOnInputLock`, `AgentSession.IsObservationTool`,
`CommandTersifier.ForAgentTool`, and `SessionProvisioner`'s
`DefaultCommands`/`CreateEnabledCommand` — are all catalog lookups, so adding a tool is
one descriptor plus its command class. The meta-tools (`send_command`,
`provision-session`, `end-session`) are deliberately NOT in the catalog: they do not map
1:1 onto a `Command` and keep their own gating, special-cased in `AgentServer` next to
the catalog dispatch.

## Structured replies

Agent commands do not write free text. They write a `CommandResult` (in
`System.Text.Json.Nodes`): `Success`, `Command`, `Error?`, `Data?` with factory helpers
`Ok(command, data?)` / `Fail(command, error)` and `ToJson()` / `ToJsonObject()`.
Serialization options live in `AgentJson` (`Serialize<T>`). For in-process tool dispatch
the server runs a command against a `CapturingReply : Reply` and reads back
`Captured` rather than going through a socket.

## How the new commands plug into the existing pattern

Each agent command (`capture`, `query`, `find`/`wait-for`, `invoke`, `click`, `drag`,
`record`, `displays`, `launch`) derives from `AgentCommand : Command` (#208) and follows
the house pattern:

- No per-command `Clone` code (#207): the MemberwiseClone-based `Command.Clone(Reply)`
  copies every field by construction (all serializable command state is value/string-typed),
  installs the fresh `Reply`, and deep-clones `EmbeddedCommands`. A reflection hygiene test
  (`CommandClonePropertyRoundTripTests`) round-trips every public settable property of every
  command through Clone.
- `BuiltInCommands` returning the command with `Enabled=false` by default, referenced
  explicitly by the type's one-line `CommandRegistry.Entries` entry (#204) — the single
  registration point that drives serialization, the invoker's built-ins table, and the
  registry-completeness hygiene test (`CommandRegistryTests`).
- `[XmlAttribute]` on serializable props (all-lowercase names — see `XmlNameCasingTests`, #200).

`AgentCommand` owns a **sealed** `Execute()` template method: the base
`Command.Execute()` (per-command `Enabled` gate + telemetry), then the
`AgentRuntime.AgentCommandsEnabled` gating block (Warn log + structured
`CommandResult.Fail` reply, fail-closed), then the `AGENT-AUDIT:` line, then dispatch to
the command's `ExecuteCore()`. The gate is **structural**: a subclass cannot override
`Execute()`, so a new agent command cannot forget the opt-in check. This matters because
agent commands are ordinary `Command`s reachable over the legacy TCP/serial pipeline,
which has **no** server-side agent gate — in-command enforcement is the only gate on
those transports (`AgentServer` re-checks it only on the MCP/HTTP path).
`AgentCommandStructuralGateTests` asserts every agent tool maps to an `AgentCommand`.

Window-targeting commands additionally derive from
`WindowTargetingAgentCommand : AgentCommand`, which hosts the five shared selector
properties (`window`/`handle`/`process`/`classname`/`foreground`) once, performs the
single `WindowResolver.Resolve` call, and hands `ExecuteCore(WindowInfo? target)` the
resolved window (commands with window-less modes — pixel `click`/`drag`, region
`capture`, `record` — override `RequiresWindowTarget`).

Because they are normal commands, they are dispatched by the existing
`CommandInvoker` and are reachable through every existing transport as well as the new
MCP/HTTP façade — no special-casing in the command pipeline.

## `AgentServer` (MCP / HTTP)

`AgentServer` is the network façade, gated by `AppSettings.McpServerEnabled`. It exposes
the gated agent tools registered in `ToolCatalog` (`capture`, `query`, `displays`, `find`,
`wait-for`, `invoke`, `drag`, `click`, `record`, `launch`) plus the meta-tools
`send_command` (a generic raw command-line passthrough), `provision-session`, and
`end-session`. Two transports share one dispatch path:

- **MCP stdio** — used by the `--mcp` headless bootstrap.
- **HTTP floor** — one JSON-RPC request per `POST` to `/mcp`, bound to
  `McpBindAddress` (default `127.0.0.1`) on `McpHttpPort` (default `5151`).

Tool calls are translated into command invocations executed through
`CommandInvoker` + `CapturingReply`, and the captured `CommandResult` JSON is returned
to the caller.

## `--mcp` headless bootstrap

The `--mcp` command-line switch starts MCEC without the WinForms UI: it initializes
`AgentRuntime` (settings + invoker + the `HeadlessAppHost`), then runs `AgentServer` as
an MCP stdio server and pumps stdin/stdout until the client disconnects. This path
shares the exact same commands and gating as the interactive host — the only difference
is the absence of UI and the stdio transport. The process exits when the client closes
stdin (EOF) or when a `mcec:exit` command runs (`HeadlessAppHost.RequestShutdown`
performs the same teardown as the EOF path after letting the in-flight reply flush).

## Gating model recap

Three independent, off-by-default gates, enforced wherever the surface is reachable:

1. `AppSettings.AgentCommandsEnabled` (via `AgentRuntime.AgentCommandsEnabled`) —
   the agent-command master switch, separate from actuation.
2. Per-command `Enabled` — each command still ships `Enabled=false`.
3. `AppSettings.McpServerEnabled` — the network façade, localhost-bound by default.

And `AgentRuntime.Audit` emits an `AGENT-AUDIT:` line for every agent action regardless
of transport.
