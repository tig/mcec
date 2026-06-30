# Mini-spec: GIF recording for agent sessions (#80)

## Goal

Let an MCEC 3.0 agent record agent-driven desktop activity as an animated GIF — either a
whole short segment (explicit start/stop) or a bounded one-shot — reusing the same target
model and security gate as `capture`. This makes dogfooding, debugging, demos, and issue
reports far easier: the loop that can `capture` a still frame can now produce a compact
moving artifact of what happened over time.

## Surface

A single `record` command / MCP tool with three modes, selected by `action`:

| `action`  | Behavior                                                                            |
| --------- | ----------------------------------------------------------------------------------- |
| `start`   | Begin capturing the target at `fps` on a background thread; returns immediately.    |
| `stop`    | Stop the in-progress recording, encode the GIF, write `file`, return metadata.      |
| `oneshot` | `start`, capture for `durationMs`, then auto-stop/encode/write in one blocking call. |

`action` defaults to `oneshot` when `durationMs` is given, else `start`.

**Target** (same resolver as `capture`): `window` (title substring), `handle`, `process`,
`className`, `foreground:true`, or an explicit region (`x`/`y`/`width`/`height`). The target
is resolved and fixed at `start`/`oneshot`; `stop` needs no target.

**Other args:** `fps` (default 5), `durationMs` (one-shot length / auto-stop cap),
`file` (output `.gif` path; a temp path is generated if omitted), `maxWidth`
(downscale longest side, default 1280 to keep GIFs bounded).

Only one recording may be active at a time; `start` while recording returns an error, and
`stop` with nothing recording returns an error.

## Result envelope

Returns the shared `CommandResult` JSON (`success` / `command` / `error` / `data`). On a
successful `stop`/`oneshot`, `data` carries:

```json
{
  "file": "C:\\...\\rec.gif",
  "frames": 73,
  "durationMs": 14600,
  "fps": 5,
  "width": 1280,
  "height": 824,
  "bytes": 1048576,
  "target": { "handle": 123456, "title": "Notepad", "...": "..." }
}
```

`start` returns `{ "recording": true, "fps": 5, "target": { ... } }`. Errors use the
existing `error` string (e.g. ambiguous/no target, already-recording, disabled gate).

## Encoding

No new dependency: each captured frame is quantized + LZW-compressed to a single-frame GIF
by GDI+ (`Bitmap.Save(..., ImageFormat.Gif)`), and `GifEncoder` stitches those frames into
one GIF89a — global header, a Netscape looping extension, and a per-frame Graphic Control
Extension carrying the inter-frame delay (`1000/fps` → centiseconds). Each frame keeps its
own color table as a local color table, so per-frame palettes survive.

## Limits (anti-footgun)

So an agent cannot accidentally create an unbounded file, the recorder clamps to operator
settings (with built-in defaults):

- `AgentRecordMaxFps` (default 30)
- `AgentRecordMaxDurationMs` (default 60000 — 60 s)
- `AgentRecordMaxFrames` (default 600)
- `AgentRecordMaxWidth` (default 1280; frames are downscaled to fit)

Requests above a limit are clamped (not failed), and the clamp is audited.

## Security & privacy

- **Disabled by default**, behind the *same* `AgentCommandsEnabled` opt-in as `capture`,
  plus the per-command `Enabled` gate in `mcec.commands` (fail-closed) — honoring #74.
- Every `start` / `stop` / write emits an `AGENT-AUDIT:` line with target, duration, fps,
  and output path.
- Docs warn that GIF recording can capture **sensitive on-screen content** for the whole
  duration — louder than a single still `capture`.

## In-product guidance

`AgentServer.Instructions` gains a line on *when* to record vs. still-`capture`: prefer
`capture` for a single state check; use `record` only to show change over time (an
animation, a repro of a transient/flicker), and keep it short.

## Acceptance criteria mapping

- ✅ Command/MCP surface to record a GIF from a window, foreground window, or region.
- ✅ Record part of a session via explicit `start`/`stop` or bounded `durationMs`.
- ✅ Structured JSON: success/error, output path, frame count, duration, dimensions, bytes.
- ✅ Docs + built-in MCP guidance on GIF-record vs still-`capture`.
- ✅ Tests for argument validation and the disabled gate; an opt-in manual desktop test for
  real capture.
</content>
</invoke>
