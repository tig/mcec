MCEC (Model Context Environment Controller) lets you see and drive native Windows apps.

Work the loop: observe -> target -> act -> observe.

1. TARGET a window by `window` (title substring), `process` (name without .exe), `className`, or
`foreground:true` — you MUST give at least one; a call with no target fails. Reuse the `handle` a `query`
returns for follow-up calls: it is stable, and a dialog you open shares the process name, so re-resolving
by process/title can match the wrong window. Open menus and other untitled popups are not enumerated by
title/process — target them by handle or `foreground:true`.

2. OBSERVE: `query` dumps the UI Automation tree (controlType, name, automationId, bounds, state, value)
so you can pick a control instead of guessing pixels; `capture` returns a PNG of the window (works on
composited WinUI/WPF surfaces). Use `capture` for a single state check; use `record` ONLY to show CHANGE
over time — a bounded one-shot (`durationMs`) or `action:start` then `action:stop`; keep recordings short
(fps/duration are capped and frames downscaled), and remember it captures whatever is on screen for the
whole duration. Check results for trouble: a `capture` with errorCategory `capture-blank` is a black/empty
frame (minimized, cloaked, occluded, or a locked session) — restore/foreground the window and retry
instead of trusting the image; a `capture-fallback` warning means PrintWindow was refused and the picture
may be wrong. If `query` returns `truncated:true` (a `tree-truncated` warning), the tree hit the node cap
— raise `maxNodes` or target a deeper window so you don't reason over a partial tree. warnings are
non-fatal; errorCategory tells you how to recover.

3. ACT: prefer `invoke` (by name/automationId/classname; action invoke|toggle|setvalue|setfocus|expand|
collapse) over coordinate clicks — it is far more reliable. To click a menu item, first `invoke` its
parent menu with action `expand` (a closed menu's sub-items are not in the tree until opened), then
`invoke` the item. Invoking a control that opens a MODAL dialog (About, Settings, message/file dialogs)
returns promptly with `modalPending:true` — the action completes when the dialog closes — so just
`query`/`capture` the new window to read it, and `invoke` its buttons to dismiss it. `invoke` does NOT
wait for a control — it fast-fails if the element isn't present yet — so `wait-for` (or `find` with a
timeout) the control before acting; an `invoke` that returns `error.category:no-target` means the control
hasn't appeared yet, so `wait-for` it rather than blindly retrying. `send_command` sends any raw MCEC
command (keystrokes, mouse, launch). To DRAG — resize a window by its sizing border, move one by its
title bar, or drag a slider/handle (there is no `invoke` for these) — `send_command` a press-move-release
sequence: `mouse:mt,x,y` to the start point, then `mouse:lbd` (button down), then a STREAM of `mouse:mt,x,y`
along the path, then `mouse:lbu` (button up); coords are absolute screen pixels and a short pause between
moves keeps the target tracking. Re-`query` afterward — a moved/resized window's controls are at new
bounds.

4. VERIFY with another `query` or `capture` — always confirm the act had the intended effect.

RESULTS: every tool returns one envelope — `{ ok, result?, warnings?, error? }`. Branch on `ok` first: on
success read `result`; on failure read `error.category` (a closed set: timeout, ambiguous-selector,
stale-element, no-target, capture-blank, focus, elevation, foreground, internal) to choose recovery — e.g.
`no-target` means broaden the selector or `query` to discover targets, `ambiguous-selector` means add
`processName`/`className`/`automationId`, `stale-element` means re-`query`/`find` for a fresh handle.
`error.detail` is human-readable and `error.lastObservation`, when present, is the last good state before
the failure.

COMPOSE: many tasks have no single dedicated tool — build them by combining primitives creatively. Launch
an app with `send_command winr` then `chars:<path>` then `enter` (the new window is foreground: `query
{foreground}` for its handle). Drag/resize/move by `send_command mouse:lbd` → a path of `mouse:mt` →
`mouse:lbu`. Switch a tab/list item by `query`ing its bounds and clicking its centre. Record a window by
`query`ing its bounds and passing them as the `record` region. Wait for a window by polling `query` until
it appears. Reach for a raw `send_command` before giving up.

OVERLAY: MCEC may show a small on-screen overlay (default on) that narrates each command you run so the
operator can see MCEC is driving. It is deliberately excluded from `query`/`find`/`capture`/UIA targeting
— you will never see or target it, and it is never a candidate window — but it DOES appear in
full-screen/region `capture`s and `record`ings (not in window-targeted captures).

SECURITY: observation tools (capture/query/find/invoke/record) only work when the operator has set
AgentCommandsEnabled=true; otherwise they return an error — surface that to the user rather than retrying.
Every action is audit-logged on the host.
