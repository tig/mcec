<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Agent Tool Result & Error Contract (MCEC 3.0)

**Status:** Design artifact — owned by [#101](https://github.com/tig/mcec/issues/101).
**Schema:** [`agent-tool-result.schema.json`](./agent-tool-result.schema.json) (JSON Schema draft 2020-12).

This document defines **one** result envelope and **one** error vocabulary for every MCEC 3.0
agent tool. It exists so that the rest of the agent stack hardens around a single shape instead
of around per-tool ad-hoc envelopes, and so an agent can reason about success and failure
uniformly across `capture`, `query`, `find`, `wait-for`, `invoke`, `send_command`, and the
session-lifecycle tools.

The epics that demand structured results/warnings/errors — sessions (#86), trace &
failure-summary (#87), selectors (#88), waits (#89), observation hardening (#90), and
actions (#91) — reference **this** contract rather than inventing their own. The walking
skeleton (#98) emits results that validate against the schema.

> Scope note: this is a design artifact. It defines the shape and vocabulary; the runtime
> implementation lands with #98 and the individual tool epics. There is intentionally no
> code-runtime overlap here.

## The envelope

Every agent tool returns a single JSON object of this shape:

```json
{
  "sessionId": "s-4f2a…",
  "ok": true,
  "result": { "...tool-specific..." },
  "warnings": [
    { "code": "minimized-window", "detail": "Target was minimized; restored before capture." }
  ],
  "error": {
    "code": "selector-matched-3",
    "category": "ambiguous-selector",
    "detail": "Selector title='Save' matched 3 windows; refine with processName or className.",
    "lastObservation": { "...last good state..." }
  }
}
```

A real result is **either** a success **or** a failure — never both:

- **Success:** `ok: true`, `result` present, `error` omitted. `warnings` optional.
- **Failure:** `ok: false`, `error` present, `result` omitted (or null). `warnings` optional.

The schema enforces this: `ok: false` requires `error`; `ok: true` forbids an `error` object.

## Fields

| Field        | Type                | Required | Meaning |
|--------------|---------------------|----------|---------|
| `sessionId`  | string \| null      | no       | Owning session id (#86). Present when the call ran inside a mounted session; null/absent for stateless one-shot calls. |
| `ok`         | boolean             | **yes**  | The single field an agent branches on first. `true` = goal achieved. |
| `result`     | object \| null      | on success | Tool-specific success payload. Its shape is owned by each tool's epic, not by this contract. |
| `warnings`   | array of warning    | no       | Non-fatal conditions surfaced alongside the result. Present on success or failure. |
| `error`      | error object        | on failure | The failure descriptor. Omitted on success. |

`sessionId` is at the **envelope** level (not inside `result`/`error`) so it is always reachable
regardless of outcome — a failed call still tells you which session it belonged to.

## Error taxonomy

`error.category` is a **closed** set. Agents may branch exhaustively on it; new failure modes are
mapped onto an existing category (or, rarely, the set is extended by revising this contract — see
[Stability](#stability--versioning)). `error.code` is a finer-grained, open-ended string that
narrows the category; agents may branch on specific codes but **must** tolerate unknown codes by
falling back to `category`.

| `category`           | When it applies | Typical recovery for an agent |
|----------------------|-----------------|-------------------------------|
| `timeout`            | A wait/poll (e.g. `wait-for`) expired before its condition held. | Re-observe; extend the timeout; or abandon the step. |
| `ambiguous-selector` | A selector matched more than one candidate and the tool refused to guess. | Add disambiguators (`processName`, `className`, `automationId`) and retry. |
| `stale-element`      | A previously resolved element/handle is no longer valid (window closed, tree re-rendered). | Re-`query`/`find` to get a fresh reference, then retry. |
| `no-target`          | A selector matched nothing (no window/element). | Broaden the selector, `query` to discover targets, or wait for the target to appear. |
| `invalid-argument`   | The request itself is malformed or inapplicable (#191): a client-supplied argument is invalid (unknown action, oversized region, ill-formed endpoint) or cannot apply to the target (an element that lacks the pattern an action needs). | Fix the arguments and re-issue. Do **not** retry the same call, broaden a selector, or re-find the target — the request will keep failing until it changes. |
| `capture-blank`      | A screenshot was produced but detected as black/blank (composited/occluded/locked-session). | Try foreground/region capture; restore/foreground the window; surface the limitation. The suspect image still rides in `error.partialResult`. |
| `focus`              | An action required input focus that could not be set. | `invoke` `setfocus` first, or foreground the window, then retry. |
| `elevation`          | The target runs at a higher integrity level (UAC) than MCEC and cannot be driven. | Surface to the operator; the action cannot proceed without elevation. |
| `foreground`         | An action required the target to be foreground and it could not be brought forward. | Foreground the window (subject to OS foreground-lock rules), then retry. |
| `internal`           | An unexpected MCEC-side fault (bug, unhandled exception). | Not agent-recoverable; report with `lastObservation` for a bug bundle (#87). |

`focus`, `elevation`, and `foreground` are kept distinct (rather than one "input" bucket) because
the recoveries differ: focus is retryable, foreground is OS-policy-constrained, and elevation is
not recoverable by the agent at all.

### Example error codes per category

`code` values are stable strings but the list is open; these are illustrative, not exhaustive:

- `timeout` → `wait-condition-timeout`
- `ambiguous-selector` → `selector-matched-N`
- `stale-element` → `element-stale`, `window-closed`
- `no-target` → `window-not-found`, `element-not-found`
- `invalid-argument` → `region-too-large`, `action-unknown`, `pattern-unsupported`, `bad-arguments`,
  `launch-path-missing`, `recording-in-progress`, `no-recording`
- `capture-blank` → `frame-all-black`, `frame-mostly-blank`
- `focus` → `set-focus-failed`
- `elevation` → `target-elevated`
- `foreground` → `set-foreground-denied`
- `internal` → `unhandled-exception`

## Warning model

`warnings` carries **non-fatal** conditions: the call still succeeded (`ok: true`), but the agent
should know something was adjusted, degraded, or assumed. Each warning is `{ code, detail }` with
the same stability rules as error codes (kebab-case, branchable, tolerate unknowns).

Examples: `minimized-window` (target restored before capture), `tree-truncated` (UIA tree clipped
to a depth/size limit — see #90), `region-clamped` (requested region clipped to the screen).

Warnings may also accompany a **failure** — e.g. a capture that both warns about a restored window
and then fails `capture-blank`.

## `lastObservation`

`error.lastObservation` carries the **last good state** observed before the failure so a failed
call is debuggable without rerunning it. It typically holds the most recent `query`/`find`
result, a `capture` **summary** (see below), or the resolved target `WindowInfo`. It is the
primary input to #87's `failure-summary.md` ("last good observation + failing tool call") and to
bug bundles.

When no prior observation exists (e.g. the very first call in a session failed at selector
resolution), `lastObservation` is omitted.

**Image-bearing observations are summarized, never inlined (#215).** A `capture` result carries
the full base64 PNG, and replaying that into every later failure would attach megabytes of stale
screenshot to *errors*. So when the last good observation was a capture, `lastObservation` is a
compact summary instead:

```json
{
  "kind": "capture-summary",
  "window": { "...WindowInfo..." },
  "width": 800, "height": 600, "encoding": "png", "bytes": 48213,
  "blankCheck": { "blank": false, "dominantFraction": 0.1043, "dominantIsDark": false },
  "artifact": "<sessions>/<started>-<sessionId>/capture-20260701-101502123-1.png"
}
```

The PNG bytes are written to `artifact` — a file under the per-session artifact directory (the
same directory teardown/evidence bundles collect) — and `lastObservation` never contains
`base64`. If the artifact could not be written, `artifactError` explains why and only the summary
is retained. This does **not** change the capture tool's own `result` (the agent still receives
the image inline and as an MCP image block) or `error.partialResult` (a blank capture's suspect
PNG from the *failing* call, #206).

## `partialResult`

`error.partialResult` carries the failing call's **own** partial payload when the tool deliberately
kept one — e.g. a `capture-blank` failure still carries the (suspect) PNG it grabbed, so the evidence
the command paid to produce is not discarded (#206). It is distinct from `lastObservation`, which is
the last *good* state from a *prior* call. Omitted when the failure produced nothing.

## Mapping onto the MCP tool-result transport

MCP tool calls return a `CallToolResult` with a `content` array and an `isError` flag. The envelope
above rides **inside** that transport; it does not replace it:

- The envelope is serialized (compact, camelCase, nulls omitted — per `AgentJson.Options`) and
  placed in a **text** content block. Agents parse that JSON to read `ok`/`result`/`error`.
- MCP `isError` mirrors the envelope: `isError = !ok`. This lets MCP-native clients that only look
  at `isError` still distinguish success from failure, while agents that parse the body get the
  full structured error.
- Binary payloads (e.g. `capture`'s PNG) are additionally emitted as an MCP **image** content block
  so MCP image-aware clients render them; the same image is referenced from `result` for
  text-only agents.
- Protocol-level JSON-RPC errors (malformed request, unknown method/tool) remain JSON-RPC `error`
  responses and are **not** wrapped in this envelope. This contract governs *tool outcomes*, not
  *transport faults*.

The HTTP façade (`POST :5151`) returns the envelope object directly as its JSON body, with HTTP
`200` for a well-formed call regardless of `ok` (read `ok` for the outcome) and non-`200` only for
transport/auth faults.

## Relationship to today's `CommandResult`

The current `src/Commands/CommandResult.cs` emits a thinner `{ success, command, error, data }`
shape. This contract is its forward target:

| Today (`CommandResult`) | This contract |
|-------------------------|---------------|
| `success`               | `ok`          |
| `data`                  | `result`      |
| `error` (string)        | `error.detail` (+ `code`, `category`, `lastObservation`) |
| `command`               | (moves into the trace/transcript, #87) |
| —                       | `sessionId`, `warnings`, `error.category` |

Migrating `CommandResult` to this envelope is implementation work for #98 / the tool epics, not
part of this artifact.

## Stability & versioning

- `error.category` and the success/failure invariants are the **stable** surface: agents may rely
  on them. Changes here are contract revisions, reviewed as such.
- `code` strings (error and warning) are stable once shipped but the **set** is open; agents must
  tolerate unknown codes by falling back to `category`.
- `result` shapes are owned by each tool's epic and version with that tool.
- Validate any candidate envelope against
  [`agent-tool-result.schema.json`](./agent-tool-result.schema.json) in tests.
