MCEC (Model Context Environment Controller) lets you see and drive native Windows apps.

Work the loop: observe -> target -> act -> observe. You are measured on token cost and wall-clock time:
prefer structured observation (`windows`/`query`/`find`) over `capture`; when you must capture, the
smallest region that answers the question; use `windows` wait-for and title/process filters instead of
scroll-and-recapture loops; prefer direct URL/`launch` navigation over hunting pixels; don't re-observe
what the last result already told you — large-window captures dominate token cost.

1. TARGET a window by `window` (title substring), `process` (name without .exe), `className`, or
`foreground:true`: you MUST give at least one; a call with no target fails. If you do not yet know what to
target, DISCOVER first with `windows`: it lists the visible top-level windows (handle, title, className,
processName, processId, bounds), optionally filtered by `window`/`process`/`className`; use it to enumerate
available targets instead of guessing one, and to WAIT on window state (pass `timeout` ms + a filter and a
`condition`): `appears` (default; poll until a match exists, `count:0` on timeout), `disappears` (until no
window matches, e.g. a modal you opened has closed), or `foreground` (until a match is the foreground
window, e.g. a launched app took focus). A wait that times out carries `waitedFor` + `lastObservedWindows`,
so you can triage without a second observation. `windows` with no filter lists
everything; a wait (or `disappears`/`foreground`) with no filter is refused (it will not wait for an arbitrary window). Reuse
the `handle` a `windows`/`query` returns for follow-up calls: it is stable, and a dialog you open shares the
process name, so re-resolving by process/title can match the wrong window. `window` and `at:{by:name}` match
on **substring**, so a name can silently resolve to the wrong element or window with no error: asking for the
`Serial Server` tab can select the `Server` tab, and `window:"Settings"` can hit the OS Settings app; when
names overlap, target by `handle`. Open menus and other untitled
popups are not enumerated by title/process; target them by handle or `foreground:true`.

2. OBSERVE: `query` dumps the UI Automation tree (controlType, name, automationId, bounds, state, value)
so you can pick a control instead of guessing pixels; `capture` returns a PNG (works on composited
WinUI/WPF surfaces; check `bytes` — full windows are costly and inline base64 can blow your token budget).
Browser chrome is UIA-targetable; in-page web content is not — verify with region `capture` and
screen-absolute pixel `click` (window origin from `query` bounds + offset; re-capture after layout shifts).
Use `capture` for a single state check when the tree can't answer; use `record` ONLY to show CHANGE
over time; a bounded one-shot (`durationMs`) or `action:start` then `action:stop`; keep recordings short
(fps/duration are capped and frames downscaled), and remember it captures whatever is on screen for the
whole duration. Region targets (`x`/`y`/`width`/`height`) for `capture` and `record` are size-capped: an oversized region
fails fast with `errorCode:region-too-large` (category `invalid-argument`; the detail states the limit), so
request a smaller region or a window target (windows are bounded by their own size). An open `start`
auto-stops at the operator's limits; `stop`
still returns that buffered GIF (exactly once), and after an auto-stop a new recording (`start` or a
one-shot) is allowed: it discards the unfetched GIF, carrying an `unfetched-recording-discarded` warning
on its result, so `stop` promptly to collect your output. Check results for trouble: a `capture` with errorCategory `capture-blank` is a black/empty
frame (minimized, cloaked, occluded, or a locked session); restore/foreground the window and retry
instead of trusting the image (the suspect PNG is still available in `error.partialResult` if you want to
inspect what was grabbed); a `capture-fallback` warning means PrintWindow was refused and the picture
may be wrong. If `query` returns `truncated:true` (a `tree-truncated` warning), the tree hit the node cap;
raise `maxNodes` or target a deeper window so you don't reason over a partial tree. warnings are
non-fatal; errorCategory tells you how to recover. The bounds `query`/`find` report are ABSOLUTE screen
pixels; the `displays` tool reports every monitor's pixel bounds and DPI/scale (and the union
virtualBounds) so you can interpret those bounds across multiple/scaled monitors and place pixel clicks or
drags without measuring the screen yourself.

3. ACT: prefer `invoke` (by name/automationId/classname; action invoke|toggle|setvalue|setfocus|expand|
collapse|select) over coordinate clicks; it is far more reliable. To put text in a native field, prefer
`invoke`+`setvalue` (exact, no keystroke race) or `clipboard`+paste over `chars:`; for passwords rely on
the OS password manager / masked fields — never invent or log secrets. To click a menu item, first `invoke` its
parent menu with action `expand` (a closed menu's sub-items are not in the tree until opened), then
`invoke` the item. Use `select` for TabItem/ListItem/RadioButton (SelectionItem pattern). Invoking a control that opens a MODAL dialog (About, Settings, message/file dialogs)
returns promptly with `modalPending:true` (the action completes when the dialog closes), so just
`query`/`capture` the new window to read it, and `invoke` its buttons to dismiss it. `invoke` does NOT
wait for a control (it fast-fails if the element isn't present yet), so `wait-for` (or `find` with a
timeout) the control before acting; an `invoke` that returns `error.category:no-target` means the control
hasn't appeared yet, so `wait-for` it rather than blindly retrying. An `invoke` failing with
`error.code:pattern-unsupported` (category `invalid-argument`) means the element EXISTS but cannot perform
that action; re-finding it will never help; pick a different action or `click` its centre instead.
`action-unknown` means the `action` string itself is wrong; fix it (invoke|toggle|setvalue|setfocus|
expand|collapse|select). To DRAG (resize a window by its
sizing border, move one by its title bar, drag a slider/handle, marquee-select, or reorder; there is no
`invoke` for these), use the `drag` tool: give a `from` and a `to`, each either an element `{ by, value }`
in the target window (dragged from/to its centre) or an absolute screen pixel `{ x, y }`, plus optional
`path` waypoints for a curved or multi-stop drag. The whole press→move→release is dispatched ATOMICALLY, so
prefer it over hand-rolling `mouse:lbd`/`mouse:mt`/`mouse:lbu` (which can interleave with other commands).
Coords are absolute screen pixels (the same space `query`/`find` bounds report), so you can drag straight
from one control's bounds to another's. Re-`query` afterward: a moved/resized window's controls are at new
bounds. For window-level move/resize, use the `window` tool: `action:"move"`/`"resize"` with target
coordinates and optional `animate:true` to make the window appear to be dragged rather than instantly
teleported. `window` also handles `minimize`, `maximize`, `restore`, and `foreground`.
To CLICK a point `invoke` can't reach (a custom-drawn cell, a canvas/map coordinate, or a barepixel), use the `click` tool: give `at` as an element `{ by, value }` (clicked at its centre) or an
absolute screen pixel `{ x, y }`, with optional `button` (left|right|middle) and `count`
(2 = double-click); the move+click is dispatched atomically. Still prefer `invoke` for ordinary buttons and menu items;
it doesn't depend on the control being on-screen and unobscured. System file dialogs (Open, Save Print
Output As) are separate windows; `wait-for`/`query` by title, reuse the returned `handle`, and act on that
target (don't assume the dialog shares the app's process). They often have no UIA-settable filename field;
`clipboard { action:set, text:… }` then Ctrl+V and Enter, or `send_command chars:<path>` (chars: types the
path LITERALLY; a single backslash is a backslash, `C:\folder\file.ext`, no doubling). Click the filename
field first when paste fails. `send_command`
sends any other raw MCEC command (keystrokes, single mouse actions, launch); the raw
`mouse:drag,x1,y1,x2,y2[,...]` is the same atomic drag in pixels and `mouse:mtp,x,y` moves the pointer to
an absolute screen pixel. If `invoke`/`click` by name returns `no-target`, `query` the tree: WinUI/MAUI
labels often include emoji and ellipsis; click a control's bounds centre from `query` instead of guessing a
plain name. Before you send an app's own keyboard SHORTCUT to a specific control (e.g. a MAUI GraphicsView
that zooms on `+`/`-`, a canvas, a game surface), `focus` it first: keystrokes only reach the foreground
window's focused control, and a bare `click` or `invoke setfocus` does not reliably focus a custom-drawn
surface. The `focus` tool foregrounds the window, clicks the control (a real click focuses what SetFocus
misses), and verifies; give `at` an element `{ by, value }` or pixel `{ x, y }`, or omit `at` to just
foreground a window and confirm focus. It fails `foreground` if the window won't activate, `focus` if no
control took focus. `invoke setfocus` is also verified now; it fails `focus` (code `focus-not-set`) when
the element does not end up focused, which is your cue to `focus` (it clicks) or `click` the control.

4. VERIFY with another `query` or `capture`; always confirm the act had the intended effect.

RESULTS: every tool returns one envelope: `{ ok, result?, warnings?, error? }`. Branch on `ok` first: on
success read `result`; on failure read `error.category` (a closed set: timeout, ambiguous-selector,
stale-element, no-target, invalid-argument, capture-blank, focus, elevation, foreground, internal) to
choose recovery; e.g. `no-target` means broaden the selector, `query` to discover targets, or `wait-for`
the element; `invalid-argument` means the REQUEST itself is wrong (unknown action, oversized region,
ill-formed endpoint, an action the element can't perform); fix the arguments, do NOT retry the same call
or broaden a selector; `ambiguous-selector` means the element selector matched more than one element and
the tool refused to guess (the match count rides in the code, `selector-matched-N`); NARROW the selector
(prefer `automationId`, else `className` or a more specific name, or click the control's centre from its
`query` bounds); a tab's header and its page can carry the same name, so drive tabs with `invoke`+`select`
or an automationId rather than the shared name; retrying it unchanged cannot help;
`stale-element` means the window/element went away mid-call (closed or re-rendered); re-`query`/`find`
for a fresh handle, then retry; `elevation` means the target runs elevated (UAC) at a higher integrity
level than MCEC and cannot be observed or driven; report it to the user, do not retry; `internal` is not
recoverable by you; report it. `foreground` means Windows refused to bring the target to the foreground
(a foreground lock, a modal on another app, or a full-screen exclusive window is holding it), so keystrokes
would not reach it; retry the `focus` tool after whatever holds the foreground is gone, or ask the operator
to click the target. `focus` means the window is foreground but no control took keyboard focus (it went
nowhere, or to a sibling); `click` the exact control, or drive it with `invoke` instead of keystrokes.
Branch on codes and categories, never on
the wording of `error.detail` (it is human-readable and may change). `error.lastObservation`, when present, is the last good state before the failure, and
`error.partialResult` is the failing call's OWN partial payload (e.g. a blank capture's suspect PNG).
For a `capture`, `lastObservation` is a compact summary plus an `artifact` path where the PNG was saved; it
never carries the image bytes inline, so re-`capture` if you need to SEE the prior state rather than reason
about its metadata.

SESSIONS: every result carries a `sessionId`; the session it ran in. A session is the runtime's memory of
one task: its active target window, last observation, last action, last error, and a per-session artifact
directory (where a `capture`'s bytes are spilled). You do NOT need to manage sessions for a single linear
task: omit `sessionId` and every call shares one implicit default session, so state just accumulates. To
run INDEPENDENT tasks that must not share state, call `session-start` to get a fresh `sessionId`, then pass
that `sessionId` on each call to route it into that session; two sessions keep separate targets and
histories. `session-status` (optional `sessionId`, else the default) returns a session's remembered state
for debugging or replay; `session-end` (required `sessionId`) frees a session's state. After you end a
session, a call that still echoes its id is refused with `error.code:unknown-session` (category
`invalid-argument`); start a new one or omit `sessionId` to fall back to the default. An id you never
started is refused the same way. (Note the hyphen: the tools are `session-start`/`session-status`/
`session-end`.) The provision handoff's `Session id` is for `end-session`/teardown only — never pass it as `sessionId` on
tool calls; omit `sessionId` (default) or use an id from `session-start`. This is separate from
`provision-session`, which hands you a whole disposable MCEC INSTALL (see PROVISION), not an in-process
session.

COMPOSE: many tasks have no single dedicated tool; build them by combining primitives creatively. When
injected keystrokes must reach Start/search or the bare desktop, first show the desktop (Win+D) or `click` an
open desktop pixel; IDE/terminal shells otherwise swallow them. Launch
an app with the dedicated `launch` tool (`path` required, optional `arguments`/`workingDirectory`; returns the pid and the app's window handle once it appears). If `launch`'s `processId` differs from the returned
window's `processId`, you likely foregrounded an existing single-instance app (Notepad, browsers) — verify a
blank document/tab before typing. Fallback if `launch` is unavailable: `send_command winr` then `chars:<path>` then `enter`, or Start Menu: `send_command desktop` (Win+D) then Win+S, type the app name, Enter, then `wait-for`/`query` for its process (the new window is foreground: `query {foreground}` for its handle). If the process never appears, Win+D and retry Win+S once before concluding the app is missing; an IDE/terminal in the foreground can swallow the first attempt even after Win+D.
KEYSTROKES split two ways, and confusing them silently does nothing. To fire an app SHORTCUT or press a
navigation/editing key; a Ctrl/Alt/Win chord (Ctrl+C/V/A/S), a lone shortcut key (zoom `+`/`-`/`=`), or an
arrow/function/Enter/Esc/Tab; send a real KEYDOWN via `send_command`: a `VK_` name (`VK_OEM_PLUS` is `=`/`+`),
a named key (`enter`, `escape`, `left`/`right`/`up`/`down`, `tab`), or a chord builtin (`ctrl-x`, `ctrl-a`, `ctrl-c`, `ctrl-v`, `ctrl-z`, `ctrl-s`); bracket
with `shiftdown:<mods>`/`shiftup:<mods>` for extra modifiers. `chars:` is for LITERAL TEXT ONLY (it is WM_CHAR text entry, not SendKeys): `chars:=` types `=` and never
zooms; `chars:^a` types those characters; `shiftdown:ctrl`+`chars:c` does NOT fire Ctrl+C; right after
focus it can drop characters — prefer `invoke`+`setvalue` for fields. Type paths with `chars:`; fire
shortcuts with keydown commands. Prefer `clipboard` for bulk text; use Ctrl+C/Ctrl+V only to move data
through an app that owns the selection (copy a canvas, paste an image). Use `invoke` with `action: "select"` for tabs/list items/radios. 
Drag/resize/move with the `drag` tool (`from`/`to`, optional `path` waypoints). Switch a tab/list item by `invoke` `select` (preferred) or `click` its centre. Record a window by
`query`ing its bounds and passing them as the `record` region; use a **desktop region** when Start/search
and system dialogs must stay visible. For a repeatable demo that writes a known output file, delete the
prior file before the run and dismiss the viewer with Alt+F4 afterward so the next run's delete succeeds
(viewers keep the file locked). Wait for a top-level window with `windows` (a `process`/`window` filter plus
a `timeout`) rather than sleeping; poll `query` only for a control INSIDE a window you already have. Reach
for a raw `send_command` before giving up.

CONCURRENCY: observation (`query`/`capture`/`find`/`wait-for`/`record`) runs concurrently and never blocks
another call; a long `wait-for` won't stall a `capture`, and `invoke` returns promptly even if it opens a
modal; so a slow observation is safe to start. Physical-input actuation (`drag`, `send_command`) is
serialized: it runs one-at-a-time (the desktop has a single input stream), so don't expect two input
actions to overlap.

PACING: raw commands (`send_command`) are queued and executed paced (a per-command delay set by the
operator's CommandPacing), and a `send_command` call returns only AFTER its command has actually executed
(its `result.output` is the command's real output). It fails fast when nothing will run:
`error.code:unknown-command` means the name isn't in the loaded command table (fix the name; nothing
executed), `command-disabled` means the command exists but the operator has not enabled it (nothing
executed; recover via `request-command-access`, see COMMAND ACCESS), and `command-dropped` means the
queue refused it whole (bounds/shutdown; nothing executed).
The execution wait is bounded at 30s: a command still running past it (a long macro, `pause`, or a deep
queue backlog ahead of it) returns `error.code:send-command-timeout` while continuing to execute on the
host; wait and verify with `query`/`capture` rather than resending. The queue is BOUNDED (both total pending
commands and one command's whole embedded-command tree). A command that breaks a bound is dropped
ALL-OR-NOTHING: the entire tree is discarded, never partially run, so paired input (e.g. shiftdown:/shiftup:)
can't be split. Don't
flood the queue with long raw input streams; prefer one higher-level call (`drag`, `click`, or a single
`mouse:drag,...`) over a long hand-rolled `mouse:mt` path, and if a long sequence is unavoidable, send it
in small chunks and verify between chunks so the queue drains. Avoid sending a raw `invoke`-style command
through `send_command` if it may open a modal dialog; the queue path has no modal grace and the command
queue stalls until the dialog is dismissed; use the `invoke` tool, which handles modals.

LIFECYCLE: `send_command mcec:exit` (when that command is enabled) shuts MCEC itself down; on the stdio
transport it ends this MCP server (your call's reply flushes first, then the process exits); so send it
only to deliberately end the session, e.g. stopping a provisioned instance before `end-session`.

OVERLAY: MCEC may show a small on-screen overlay (default on) that narrates each command you run so the
operator can see MCEC is driving. It is deliberately excluded from `query`/`find`/`capture`/UIA targeting;
you will never see or target it, and it is never a candidate window; but it DOES appear in
full-screen/region `capture`s and `record`ings (not in window-targeted captures).

PROVISION: do NOT drive the operator's installed MCEC by enabling agent commands in it; an abnormal exit
leaks those security gates enabled, and the installed copy will not serve the full agent surface anyway.
Recommended path: the operator enables "Allow agents to provision disposable instances" on File > Settings
> Agent, clicks "Provision new…", and hands you a disposable instance's directory, launch line, and
token; connect to THAT copy's `mcec.exe --mcp` (or its HTTP endpoint when enabled) and do all work
there. A provisioned session enables the standard observation/actuation tool set; `launch` and most
`send_command` built-ins (e.g. `chars:`) start disabled — batch `request-command-access` for what you'll
need up front (see COMMAND ACCESS), never by editing the session's files. The `token` is the
session credential; keep it: every HTTP request to the session's `mcpEndpoint` must send the header
`Authorization: Bearer <token>` (stdio needs no header), and `end-session` requires it when you tear down
via the bootstrap server. When your task is done, tell the operator (they delete the instance from the Agent tab; you cannot remove
your own MCP client connection). stdio instances stop when the client disconnects. If you are also on
the bootstrap server, call `end-session` with the provision id and `token` after this instance has stopped;
teardown is just removing the directory. An
`end-session` with a wrong/missing token fails with `error.code:session-token-invalid`; a session you did
not provision is not yours to tear down; orphaned sessions are reaped automatically. The Agent tab also
lists provisioned instances and lets the operator delete any you leave behind.
Bootstrap path (when you are connected to the installed `mcec.exe --mcp` instead): it serves ONLY
provisioning tools — `provision-session` and `end-session` — and every observation/actuation tool is
refused with `error.code:bootstrap-only`. That is by design: when the operator has authorized it, call
`provision-session` to mint a fresh isolated instance (it returns `directory`, `exePath`, optional
`mcpEndpoint`, `sessionId`, and `token`), reconnect to that copy, and work there. If `provision-session`
returns `error.code:provisioning-not-authorized` (these feature-specific refusals ride in `error.code`,
while `error.category` stays `internal`), the operator has not opted in; tell them to enable provisioning
on the Agent tab, then retry; do not retry blindly before they do. You own teardown: `end-session` every
instance you provision.

COMMAND ACCESS: any tool or raw command refused with `error.code:command-disabled` can be requested from
the OPERATOR with the `request-command-access` tool: pass the command name(s) the refusal reported (e.g.
`launch`, `chars:`) and a one-line, honest `reason`; MCEC shows the operator a consent dialog on their
screen and your call BLOCKS (up to ~2 minutes) for their answer. On a grant the commands are immediately
usable; enabled in-memory in THIS instance only, never written to a config file; NEVER edit mcec.commands,
mcec.settings, or anything in the session directory to grant yourself access. The operator can also choose
"allow any later requests", after which further requests auto-approve (still ask per command; every grant
is audited and narrated on the overlay). A deny is FINAL for this instance: re-requesting a denied command
returns `error.code:consent-denied` with no prompt; do not nag; tell the user what you needed and why.
`consent-timeout` means the operator never answered (they may be away; you may ask again later, ideally
after checking in with the user). `consent-pending` means a consent prompt is already open: while it is up,
only observation (`capture`/`query`/`displays`/`windows`/`find`/`wait-for`/`record`) is served and every
other call is refused with that code; wait for your pending request to return. `consent-unavailable` means
no operator prompt can be shown (fail closed); ask the user to enable the command themselves. You will
never see or target the consent dialog; it is excluded from targeting like the overlay, and actuation is
frozen while it is open; only the operator's physical input can answer it.

EMERGENCY STOP: the operator has a global panic hotkey (default Ctrl+Alt+Shift+S) that instantly halts the
session from any window. If ANY tool returns `error.code:emergency-stopped` (the code, not the category;
`error.category` stays `internal`), the operator has engaged it and deliberately halted you; STOP
immediately, tell the user, and do NOT retry; nothing will actuate until they re-arm.

SECURITY: the agent tools (capture/query/displays/windows/find/wait-for/invoke/record/launch/drag/click/clipboard, and the
session-start/session-status/session-end lifecycle) only work when the operator has set
AgentCommandsEnabled=true; otherwise they return an error; surface that to the user rather than retrying.
`send_command` is also gated by AgentCommandsEnabled when you are connected over the HTTP transport (it is
refused with `error.code:agent-commands-disabled` if the agent surface is not opted in); over the local
stdio transport (`mcec.exe --mcp`) `send_command` remains available without that opt-in; and
`request-command-access` follows the same transport rule. Either way the raw
command it runs still needs its own command Enabled in mcec.commands (recover from `command-disabled` via
`request-command-access`, never by editing files; see COMMAND ACCESS).
`provision-session` additionally requires AllowSessionProvisioning, and any tool is refused with
`emergency-stopped` while the operator's emergency stop is engaged. Every action is audit-logged on the host.
