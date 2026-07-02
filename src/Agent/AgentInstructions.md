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
whole duration. An open `start` auto-stops at the operator's limits (default 60 s / 600 frames); `stop`
still returns that buffered GIF — exactly once — and after an auto-stop a new recording (`start` or a
one-shot) is allowed: it discards the unfetched GIF, carrying an `unfetched-recording-discarded` warning
on its result, so `stop` promptly to collect your output. Check results for trouble: a `capture` with errorCategory `capture-blank` is a black/empty
frame (minimized, cloaked, occluded, or a locked session) — restore/foreground the window and retry
instead of trusting the image; a `capture-fallback` warning means PrintWindow was refused and the picture
may be wrong. If `query` returns `truncated:true` (a `tree-truncated` warning), the tree hit the node cap
— raise `maxNodes` or target a deeper window so you don't reason over a partial tree. warnings are
non-fatal; errorCategory tells you how to recover. The bounds `query`/`find` report are ABSOLUTE screen
pixels; the `displays` tool reports every monitor's pixel bounds and DPI/scale (and the union
virtualBounds) so you can interpret those bounds across multiple/scaled monitors and place pixel clicks or
drags without measuring the screen yourself.

3. ACT: prefer `invoke` (by name/automationId/classname; action invoke|toggle|setvalue|setfocus|expand|
collapse|select) over coordinate clicks — it is far more reliable. To click a menu item, first `invoke` its
parent menu with action `expand` (a closed menu's sub-items are not in the tree until opened), then
`invoke` the item. Use `select` for TabItem/ListItem/RadioButton (SelectionItem pattern). Invoking a control that opens a MODAL dialog (About, Settings, message/file dialogs)
returns promptly with `modalPending:true` — the action completes when the dialog closes — so just
`query`/`capture` the new window to read it, and `invoke` its buttons to dismiss it. `invoke` does NOT
wait for a control — it fast-fails if the element isn't present yet — so `wait-for` (or `find` with a
timeout) the control before acting; an `invoke` that returns `error.category:no-target` means the control
hasn't appeared yet, so `wait-for` it rather than blindly retrying. To DRAG — resize a window by its
sizing border, move one by its title bar, drag a slider/handle, marquee-select, or reorder (there is no
`invoke` for these) — use the `drag` tool: give a `from` and a `to`, each either an element `{ by, value }`
in the target window (dragged from/to its centre) or an absolute screen pixel `{ x, y }`, plus optional
`path` waypoints for a curved or multi-stop drag. The whole press→move→release is dispatched ATOMICALLY, so
prefer it over hand-rolling `mouse:lbd`/`mouse:mt`/`mouse:lbu` (which can interleave with other commands).
Coords are absolute screen pixels — the same space `query`/`find` bounds report — so you can drag straight
from one control's bounds to another's. Re-`query` afterward: a moved/resized window's controls are at new
bounds. To CLICK a point `invoke` can't reach — a custom-drawn cell, a canvas/map coordinate, or a bare
pixel — use the `click` tool: give `at` as an element `{ by, value }` (clicked at its centre) or an
absolute screen pixel `{ x, y }`, with optional `button` (left|right|middle) and `count`
(2 = double-click); the move+click is dispatched atomically. Still prefer `invoke` for ordinary buttons and menu items
— it doesn't depend on the control being on-screen and unobscured. `send_command` sends any other raw MCEC
command (keystrokes, single mouse actions, launch); the raw `mouse:drag,x1,y1,x2,y2[,...]` is the same
atomic drag in pixels and `mouse:mtp,x,y` moves the pointer to an absolute screen pixel.

4. VERIFY with another `query` or `capture` — always confirm the act had the intended effect.

RESULTS: every tool returns one envelope — `{ ok, result?, warnings?, error? }`. Branch on `ok` first: on
success read `result`; on failure read `error.category` (a closed set: timeout, ambiguous-selector,
stale-element, no-target, capture-blank, focus, elevation, foreground, internal) to choose recovery — e.g.
`no-target` means broaden the selector or `query` to discover targets, `ambiguous-selector` means add
`processName`/`className`/`automationId`, `stale-element` means re-`query`/`find` for a fresh handle.
`error.detail` is human-readable and `error.lastObservation`, when present, is the last good state before
the failure.

COMPOSE: many tasks have no single dedicated tool — build them by combining primitives creatively. Launch
an app with the dedicated `launch` tool (`path` required, optional `arguments`/`workingDirectory`; returns the pid and the app's window handle once it appears). Fallback if `launch` is unavailable: `send_command winr` then `chars:<path>` then `enter` (the new window is foreground: `query {foreground}` for its handle). Use `invoke` with `action: "select"` for tabs/list items/radios. 
Drag/resize/move with the `drag` tool (`from`/`to`, optional `path` waypoints). Switch a tab/list item by `invoke` `select` (preferred) or `click` its centre. Record a window by
`query`ing its bounds and passing them as the `record` region. Wait for a window by polling `query` until
it appears. Reach for a raw `send_command` before giving up.

CONCURRENCY: observation (`query`/`capture`/`find`/`wait-for`/`record`) runs concurrently and never blocks
another call — a long `wait-for` won't stall a `capture`, and `invoke` returns promptly even if it opens a
modal — so a slow observation is safe to start. Physical-input actuation (`drag`, `send_command`) is
serialized: it runs one-at-a-time (the desktop has a single input stream), so don't expect two input
actions to overlap.

PACING: raw commands (`send_command`) are queued and executed paced — a per-command delay set by the
operator's CommandPacing — and the queue is BOUNDED: at most 200 commands may be pending, and one command's
whole tree (the command itself plus all recursively embedded commands) may be at most 50. A command that
breaks either bound is dropped ALL-OR-NOTHING: the entire tree is discarded (logged on the host, no error
returned to you) — never partially run, so paired input (e.g. shiftdown:/shiftup:) can't be split. Don't
flood the queue with long raw input streams — prefer one higher-level call (`drag`, `click`, or a single
`mouse:drag,...`) over a long hand-rolled `mouse:mt` path, and if a long sequence is unavoidable, send it
in small chunks and verify between chunks so the queue drains.

OVERLAY: MCEC may show a small on-screen overlay (default on) that narrates each command you run so the
operator can see MCEC is driving. It is deliberately excluded from `query`/`find`/`capture`/UIA targeting
— you will never see or target it, and it is never a candidate window — but it DOES appear in
full-screen/region `capture`s and `record`ings (not in window-targeted captures).

PROVISION: do NOT drive the operator's installed MCEC by enabling agent commands in it and disabling them
when done — an abnormal exit leaks those security gates enabled. Instead, when the operator has authorized it,
call `provision-session` to get a fresh, disposable, isolated instance: it returns a `directory` containing
`mcec.exe` plus an agent-ready co-located config (agent commands enabled ONLY inside that copy), how to
launch/connect (`exePath`, and an `mcpEndpoint` when the MCP server is enabled), and a `sessionId`. Run from
that directory, do your work there, then call `end-session` with the `sessionId` (after stopping its
mcec.exe) to delete it — teardown is just removing the directory, so a crash leaves the real install
untouched. If `provision-session` returns `error.code:provisioning-not-authorized` (these feature-specific
refusals ride in `error.code`, while `error.category` stays `internal`), the operator has not opted in
(AllowSessionProvisioning) — tell them, don't retry.

EMERGENCY STOP: the operator has a global panic hotkey (default Ctrl+Alt+Shift+S) that instantly halts the
session from any window. If ANY tool returns `error.code:emergency-stopped` (the code, not the category —
`error.category` stays `internal`), the operator has engaged it and deliberately halted you — STOP
immediately, tell the user, and do NOT retry; nothing will actuate until they re-arm.

SECURITY: the agent tools (capture/query/displays/find/invoke/record/launch/drag/click) only work when the operator has set
AgentCommandsEnabled=true; otherwise they return an error — surface that to the user rather than retrying.
`provision-session` additionally requires AllowSessionProvisioning, and any tool is refused with
`emergency-stopped` while the operator's emergency stop is engaged. Every action is audit-logged on the host.
