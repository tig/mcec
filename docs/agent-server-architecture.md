<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Agent Server: Architecture Notes (dev)

This is a short developer-facing tour of the MCEC 3.0 agent subsystem. It lives under
`src/Agent/` and is **additive**: it reuses the existing `Command` / `CommandInvoker`
pattern and adds no breaking changes to any existing command, transport, or default.

## Layering

```
  MCP client ──stdio──▶ McpStdioTransport ──┐
  HTTP POST  ──:5151──▶ McpHttpTransport ───┤   (transports)
                                            ▼
                                   JsonRpcDispatcher   (protocol layer)
                                            │  tools/call
                                            ▼
                                   AgentToolExecutor   (gate → catalog → build → dispatch → envelope)
                                            │  builds commands / consumes CommandResult
                                            ▼
                                   CommandInvoker  (existing)
                                            │  resolves + Execute()s
                                            ▼
   capture / query / displays / find / wait-for / invoke /
   drag / click / record / launch   (the ToolCatalog set; new Commands)
        │            │           │
   ScreenCapture  WindowResolver  UiaService
   (PrintWindow)  (Win32 enum)    (FlaUI / UIA3, one dedicated MTA worker)
        └──────── AgentNativeMethods (P/Invoke) ────────┘

   AgentServer = the thin static facade wiring the production instances
   (settings/invoker/session accessors from AgentRuntime) and re-exposing
   Dispatch / RunStdio / StartHttp / StopHttp / IsHttpListening / Instructions.
```

## Key seams

### `AgentRuntime` (static seam)
The single ambient hook the commands and server talk to:
`Settings` (the `AppSettings`), `Invoker` (the `CommandInvoker`),
`AgentCommandsEnabled` (derived from `AppSettings.AgentCommandsEnabled`), and
`Audit(action, detail)` which emits the `AGENT-AUDIT:` log line. Keeping this static
and tiny lets both the WinForms host and the `--mcp` headless bootstrap share one
configuration/gating point without dependency plumbing.

It also holds the host-capability half of the seam: `Host` is a small `IAppHost`
each host registers; `MainWindow` in GUI mode (in its settings-apply path, alongside
`Settings`), `HeadlessAppHost` in `--mcp` mode. Engine code calls the static wrappers:

- `SendLine(line)`: outbound line to all connected transports (GUI: `MainWindow.SendLine`;
  headless/no host: logged no-op).
- `RequestShutdown()`: orderly app shutdown (GUI: `MainWindow.ShutDown()`, self-marshaled;
  headless: deferred clean process exit after in-flight replies flush, so `mcec:exit` over
  MCP actually exits; no host: logged no-op so tests survive strays).
- `MessageWindowHandle`: for `RegisterPowerSettingNotification` and the like (GUI: the
  `MainWindow` handle; headless: throws; the activity monitor never runs headless).

This is what makes "no `MainWindow.Instance` below the UI layer" enforceable:
`MainWindow.Instance` is explicitly assigned by `Program`'s GUI path (no lazy
construct-on-touch), and any touch before assignment (or ever, headless) throws a
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

THREADING: all UIA tree access runs on **one dedicated MTA worker thread**
owned by the service, with a single cached `UIA3Automation` (created on the worker,
disposed by `UiaService.Shutdown()` at app teardown; GUI `PerformShutdown` and the
headless `--mcp` exit both call it). Timed lookups (`wait-for`, invoke's bounded
find) poll attempt-by-attempt: each attempt is one short worker item and the sleep
happens on the caller, so a long wait never monopolizes the worker. A debug
assertion enforces that UIA work never enters from a thread running a WinForms
message loop (the historical UI-thread self-deadlock). One deliberate exception:
`invoke`'s **pattern dispatch** runs on its calling thread (the per-invoke
modal-grace worker); a modal-opening Invoke blocks until the dialog closes,
and parking the shared worker there would block the very `query`/`capture` the
agent needs to dismiss that dialog.

### `ToolCatalog` / `ToolDescriptor`
The single registry of the gated agent tools. Each `ToolDescriptor` carries the tool's
name, its `tools/list` schema, its MCP-arguments→`Command` mapping, its overlay
tersifier, and its policy flags (`SerializesOnInput`, `IsObservation`,
`ProvisionedByDefault`). The former hand-synced switch/list sites; `AgentServer`'s
schema builder, tools/call gate whitelist, and `BuildCommand` mapping,
`SerializesOnInputLock`, `AgentSession.IsObservationTool`,
`CommandTersifier.ForAgentTool`, and `SessionProvisioner`'s
`DefaultCommands`/`CreateEnabledCommand`; are all catalog lookups, so adding a tool is
one descriptor plus its command class. The meta-tools (`send_command`,
`provision-session`, `end-session`) are deliberately NOT in the catalog: they do not map
1:1 onto a `Command` and keep their own gating, special-cased in `AgentServer` next to
the catalog dispatch.

## Structured replies (objects, not re-parsed strings)

Agent commands do not write free text. `ExecuteCore()` **returns** a `CommandResult` (in
`System.Text.Json.Nodes`): `Success`, `Command`, `Error?`, `Data?`, plus the mandatory failure
taxonomy `ErrorCode`/`ErrorCategory` and `Warnings`, with factory helpers `Ok(command, data?)` /
`Fail(command, error, code, category, data?)` and `ToJson()` / `ToJsonObject()`. Serialization
options live in `AgentJson` (`Serialize<T>`).

`AgentCommand`'s sealed template emits the result once per transport: a legacy TCP/serial `Reply`
receives the single `ToJson()` line (unchanged wire format), while a `CapturingReply : Reply`;
the in-process tool dispatch; receives the **object** in its typed `Result` slot. The server
consumes that object directly (`AgentToolResult.FromCommandResult`) to build the result-contract envelope:
no `ToJson → JsonNode.Parse` round-trip of its own output (a capture's base64 PNG used to be
materialized 3–4×), no prose-sniffing categorization (deleted with `FromLegacy`/`Categorize`),
and no "non-JSON output is success" fallback. The template also normalizes any failure missing a
code/category to `unhandled`/`internal`, so every agent failure is categorical by construction.
`CapturingReply.Captured` lazily serializes the typed result when legacy text is wanted
(`send_command` output, tests).

## How the new commands plug into the existing pattern

Each agent command (`capture`, `query`, `find`/`wait-for`, `invoke`, `click`, `drag`,
`record`, `displays`, `launch`) derives from `AgentCommand : Command` and follows
the house pattern:

- No per-command `Clone` code: the MemberwiseClone-based `Command.Clone(Reply)`
  copies every field by construction (all serializable command state is value/string-typed),
  installs the fresh `Reply`, and deep-clones `EmbeddedCommands`. A reflection hygiene test
  (`CommandClonePropertyRoundTripTests`) round-trips every public settable property of every
  command through Clone.
- `BuiltInCommands` returning the command with `Enabled=false` by default, referenced
  explicitly by the type's one-line `CommandRegistry.Entries` entry; the single
  registration point that drives serialization, the invoker's built-ins table, and the
  registry-completeness hygiene test (`CommandRegistryTests`).
- `[XmlAttribute]` on serializable props (all-lowercase names; see `XmlNameCasingTests`).

`AgentCommand` owns a **sealed** `Execute()` template method: the base
`Command.Execute()` (per-command `Enabled` gate + telemetry), then the
`AgentRuntime.AgentCommandsEnabled` gating block (Warn log + structured
`CommandResult.Fail` reply, fail-closed), then the `AGENT-AUDIT:` line, then dispatch to
the command's `ExecuteCore()`. The gate is **structural**: a subclass cannot override
`Execute()`, so a new agent command cannot forget the opt-in check. This matters because
agent commands are ordinary `Command`s reachable over the legacy TCP/serial pipeline,
which has **no** server-side agent gate; in-command enforcement is the only gate on
those transports (`AgentServer` re-checks it only on the MCP/HTTP path).
`AgentCommandStructuralGateTests` asserts every agent tool maps to an `AgentCommand`.

Window-targeting commands additionally derive from
`WindowTargetingAgentCommand : AgentCommand`, which hosts the five shared selector
properties (`window`/`handle`/`process`/`classname`/`foreground`) once, performs the
single `WindowResolver.Resolve` call, and hands `ExecuteCore(WindowInfo? target)` the
resolved window (commands with window-less modes; pixel `click`/`drag`, region
`capture`, `record`; override `RequiresWindowTarget`).

Because they are normal commands, they are dispatched by the existing
`CommandInvoker` and are reachable through every existing transport as well as the new
MCP/HTTP façade; no special-casing in the command pipeline.

## The MCP server: transports, dispatcher, executor, facade

The old 1,200-line static `AgentServer` is split along its seams into four types, with
`AgentServer` remaining as the thin static facade that wires the production instances:

- **`McpStdioTransport`**: the newline-delimited JSON-RPC loop the `--mcp` headless
  bootstrap runs. Each request line dispatches on a worker; the pending-task
  list is pruned per iteration and in-flight dispatches are capped (16, matching the
  HTTP bound) by *backpressure*; the reader stops consuming stdin until a slot frees.
- **`McpHttpTransport`**: the HTTP floor: one JSON-RPC request per `POST /mcp`, bound
  to `McpBindAddress` (default `127.0.0.1`) on `McpHttpPort` (default `5151`). Owns the
  `HttpListener` lifecycle (Stop **joins the accept thread and drains in-flight workers**,
  both bounded, so a Settings-dialog Stop/Start can't overlap old workers with a new
  listener), the pure `GateHttpRequest` (Host/Origin/bearer), the loopback-bind
  canonicalization, the 1 MB body cap, and the 16-worker 503 bound.
- **`JsonRpcDispatcher`**: the protocol layer (`initialize`/`ping`/`tools/list`/
  `tools/call` routing, response shapes, the meta-tool schemas), shared by both transports.
- **`AgentToolExecutor`**: the tool-execution layer: emergency-stop /
  `AgentCommandsEnabled` / per-command gates, argument validation, `ToolCatalog` command
  building, the concurrency dispatch rules, the meta-tools (`send_command`,
  `provision-session`, `end-session` + its token check), and envelope/overlay
  publication. An instance type taking settings/invoker/session **accessors** via
  constructor; tests exercise it (or construct their own transports with injected
  dispatch) instead of the old `HttpDispatchOverride`-style static seams.

The facade exposes the gated agent tools registered in `ToolCatalog` (`capture`, `query`,
`displays`, `find`, `wait-for`, `invoke`, `drag`, `click`, `record`, `launch`) plus the
meta-tools `send_command` (a generic raw command-line passthrough), `provision-session`,
and `end-session`. Tool calls are translated into command invocations executed against a
`CapturingReply`; the command's typed `CommandResult` (its `Result` slot) is wrapped
in the result-contract envelope and returned to the caller.

## `--mcp` headless bootstrap

The `--mcp` command-line switch starts MCEC without the MainWindow host: it initializes
`AgentRuntime` (settings + invoker + the `HeadlessAppHost`), starts `HeadlessOperatorUi`
(a dedicated STA pump thread hosting the operator safety surface: the emergency-stop
hotkey, the command overlay, and the modal re-arm prompt; both features are
message-loop-bound and the protocol thread never pumps), then runs `AgentServer` as
an MCP stdio server and pumps stdin/stdout until the client disconnects. This path
shares the exact same commands and gating as the interactive host; the only difference
is the absence of MainWindow (no transports, no settings UI, no tray icon) and the
stdio transport. The process exits when the client closes stdin (EOF) or when a
`mcec:exit` command runs (`HeadlessAppHost.RequestShutdown` performs the same teardown
as the EOF path after letting the in-flight reply flush); both stop the operator
surface, the dispatcher, and the UIA worker.

## Gating model recap

Three independent, off-by-default gates, enforced wherever the surface is reachable:

1. `AppSettings.AgentCommandsEnabled` (via `AgentRuntime.AgentCommandsEnabled`):
   the agent-command master switch, separate from actuation.
2. Per-command `Enabled`: each command still ships `Enabled=false`.
3. `AppSettings.McpServerEnabled`: the network façade, localhost-bound by default.

And `AgentRuntime.Audit` emits an `AGENT-AUDIT:` line for every agent action regardless
of transport.
