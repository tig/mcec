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
— it doesn't depend on the control being on-screen and unobscured. System file dialogs (Open, Save Print
Output As) are separate windows — `wait-for`/`query` by title, reuse the returned `handle`, and act on that
target (don't assume the dialog shares the app's process). They often have no UIA-settable filename field —
`clipboard { action:set, text:… }` then Ctrl+V and Enter, or `send_command chars:<path>` with every Windows
backslash doubled (`C:\\folder\\file.ext`). Click the filename field first when paste fails. `send_command`
sends any other raw MCEC command (keystrokes, single mouse actions, launch); the raw
`mouse:drag,x1,y1,x2,y2[,...]` is the same atomic drag in pixels and `mouse:mtp,x,y` moves the pointer to
an absolute screen pixel. If `invoke`/`click` by name returns `no-target`, `query` the tree: WinUI/MAUI
labels often include emoji and ellipsis — click a control's bounds centre from `query` instead of guessing a
plain name.

4. VERIFY with another `query` or `capture` — always confirm the act had the intended effect.

RESULTS: every tool returns one envelope — `{ ok, result?, warnings?, error? }`. Branch on `ok` first: on
success read `result`; on failure read `error.category` (a closed set: timeout, ambiguous-selector,
stale-element, no-target, capture-blank, focus, elevation, foreground, internal) to choose recovery — e.g.
`no-target` means broaden the selector or `query` to discover targets, `ambiguous-selector` means add
`processName`/`className`/`automationId`, `stale-element` means re-`query`/`find` for a fresh handle.
`error.detail` is human-readable and `error.lastObservation`, when present, is the last good state before
the failure.

COMPOSE: many tasks have no single dedicated tool — build them by combining primitives creatively. When
injected keystrokes must reach Start/search or the bare desktop, first show the desktop (Win+D) or `click` an
open desktop pixel — IDE/terminal shells otherwise swallow them. Launch an app with `send_command winr`
then `chars:<path>` then `enter`, or Start Menu: Win+S then type the app name then Enter (the new window is
foreground: `query {foreground}` for its handle). Use `invoke` with `action: "select"` for tabs/list
items/radios. Drag/resize/move with the `drag` tool (`from`/`to`, optional `path` waypoints). Switch a
tab/list item by `invoke` `select` (preferred) or `click` its centre. Record a **desktop region** with
`record` so Start/search and system dialogs are visible. Repeatable demos that write a known output file:
harness/operator prep deletes the prior file before the run; after opening it in a viewer, dismiss with
Alt+F4 so the next run's delete succeeds (PDF viewers often keep the file locked). Customer 1 (WinPrint hero,
issue #84): harness removes prior `winprintdemo.pdf` → disposable MCEC session (#138) → record region →
Start Menu WinPrint → file tour → Print to PDF → open PDF → close viewer — see `docs/winprint-hero-gif.md`.
Run from winprint repo; installed MCEC (`winget install Kindel.mcec`); operator ensures WinPrint is
installed. Wait for a window by polling `query` until it appears. Reach for a raw `send_command` before
giving up.

CONCURRENCY: observation (`query`/`capture`/`find`/`wait-for`/`record`) runs concurrently and never blocks
another call — a long `wait-for` won't stall a `capture`, and `invoke` returns promptly even if it opens a
modal — so a slow observation is safe to start. Physical-input actuation (`drag`, `send_command`) is
serialized: it runs one-at-a-time (the desktop has a single input stream), so don't expect two input
actions to overlap.

OVERLAY: MCEC may show a small on-screen overlay (default on) that narrates each command you run so the
operator can see MCEC is driving. It is deliberately excluded from `query`/`find`/`capture`/UIA targeting
— you will never see or target it, and it is never a candidate window — but it DOES appear in
full-screen/region `capture`s and `record`ings (not in window-targeted captures).

SECURITY: the agent tools (capture/query/displays/find/invoke/record/drag/click/clipboard) only work when the
operator has set AgentCommandsEnabled=true; otherwise they return an error — surface that to the user rather
than retrying.
Every action is audit-logged on the host.
