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
  MCP client ──stdio──┐
  HTTP POST  ──:5151──┤
                      ▼
              AgentServer  (MCP/HTTP façade, tool dispatch)
                      │  builds command lines / CommandResult
                      ▼
              CommandInvoker  (existing)
                      │  resolves + Execute()s
                      ▼
   capture / query / find / wait-for / invoke   (new Commands)
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

## Structured replies

Agent commands do not write free text. They write a `CommandResult` (in
`System.Text.Json.Nodes`): `Success`, `Command`, `Error?`, `Data?` with factory helpers
`Ok(command, data?)` / `Fail(command, error)` and `ToJson()` / `ToJsonObject()`.
Serialization options live in `AgentJson` (`Serialize<T>`). For in-process tool dispatch
the server runs a command against a `CapturingReply : Reply` and reads back
`Captured` rather than going through a socket.

## How the new commands plug into the existing pattern

Each new command (`capture`, `query`, `find`, `wait-for`, `invoke`) is an ordinary
`Command` subclass and follows the house pattern exactly:

- `Clone(Reply)` via `base.Clone(reply, new XxxCommand { ... })`.
- `Execute()` whose **first** line is `if (!base.Execute()) { return false; }`
  (preserving the per-command `Enabled` gate + telemetry).
- `BuiltInCommands` returning the command with `Enabled=false` by default.
- `[XmlAttribute]` on serializable props.

Immediately after `base.Execute()`, every agent command runs the gating block:

```csharp
if (!AgentRuntime.AgentCommandsEnabled) {
    Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED; agent commands are disabled. " +
        "Set AgentCommandsEnabled=true to opt in.");
    Reply?.WriteLine(CommandResult.Fail(Cmd,
        "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
    return false;
}
AgentRuntime.Audit(Cmd, "<target/action>");
```

Because they are normal commands, they are dispatched by the existing
`CommandInvoker` and are reachable through every existing transport as well as the new
MCP/HTTP façade; no special-casing in the command pipeline.

## `AgentServer` (MCP / HTTP)

`AgentServer` is the network façade, gated by `AppSettings.McpServerEnabled`. It exposes
the tools `capture`, `query`, `find`, `invoke`, and `send_command` (a generic raw
command-line passthrough). Two transports share one dispatch path:

- **MCP stdio**: used by the `--mcp` headless bootstrap.
- **HTTP floor**: one JSON-RPC request per `POST` to `/mcp`, bound to
  `McpBindAddress` (default `127.0.0.1`) on `McpHttpPort` (default `5151`).

Tool calls are translated into command invocations executed through
`CommandInvoker` + `CapturingReply`, and the captured `CommandResult` JSON is returned
to the caller.

## `--mcp` headless bootstrap

The `--mcp` command-line switch starts MCEC without the WinForms UI: it initializes
`AgentRuntime` (settings + invoker), then runs `AgentServer` as an MCP stdio server and
pumps stdin/stdout until the client disconnects. This path shares the exact same
commands and gating as the interactive host; the only difference is the absence of UI
and the stdio transport.

## Gating model recap

Three independent, off-by-default gates, enforced wherever the surface is reachable:

1. `AppSettings.AgentCommandsEnabled` (via `AgentRuntime.AgentCommandsEnabled`):
   the agent-command master switch, separate from actuation.
2. Per-command `Enabled`: each command still ships `Enabled=false`.
3. `AppSettings.McpServerEnabled`: the network façade, localhost-bound by default.

And `AgentRuntime.Audit` emits an `AGENT-AUDIT:` line for every agent action regardless
of transport.
