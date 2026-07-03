# Recreating the hero GIF (`docs/hero.gif`)

`docs/hero.gif` is the hero image shown at the top of [`README.md`](../README.md) and the docs home page
([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is MCEC dogfooding
itself: one MCEC drives a **second MCEC** through a guided tour (launch → **File ▸ Settings** (visit
every tab) → **mouse-resize** the window ~25% smaller by dragging its sizing border → **drag the title
bar in small circles** → **Help ▸ About** → pause) while the **on-screen command overlay**
narrates every command in burnt orange, all recorded by the agent
[`record`](agent-server.md#record--capturing-change-over-time) tool. No external screen-recorder.

The two oranges line up on purpose: the overlay's item background is the **About box's brand orange**,
so the About dialog and the narration match.

## How it is made

An **agent drives MCEC over MCP** and does everything itself; there is no driver script to run or write.
One MCEC is the **controller** the agent is connected to; the agent uses the controller's tools to
[`provision-session`](safety-emergency-stop-and-provisioning.md) a disposable **subject** MCEC, `launch`
it, tour it, and `record` the region while the controller's overlay narrates. The only scripted step is
standing up that first controller, because an agent cannot bootstrap it over MCP (there is nothing to
connect to yet).

This page is written so a **fresh agent session can reproduce the hero by reading it and calling MCEC's
MCP tools**, no scripting required beyond the one bootstrap command below.

## Setup (the only script)

[`scripts/Generate-HeroGif.ps1`](../scripts/Generate-HeroGif.ps1) builds MCEC and stands up an authorized,
MCP-serving controller from a **disposable copy** of the build (so `src/bin` is never mutated), then
prints its endpoint:

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1
# ...
#   MCP endpoint : http://127.0.0.1:<free-port>/mcp   (a free port is chosen; -Port pins one)
#   Register it  : claude mcp add --transport http mcec http://127.0.0.1:<free-port>/mcp
```

Register the printed endpoint so your agent has MCEC's tools (`provision-session`, `launch`, `query`,
`click`, `drag`, `record`, `capture`, `displays`, `send_command`, `end-session`), then ask it to recreate
the hero. The controller's config enables everything the tour needs: the agent surface, per-command
`Enabled` for the tools it calls (`query`/`click`/`drag`/`record`/`capture`/`launch`/`displays`; agent
tools are gated by BOTH `AgentCommandsEnabled` and their own table entry), `AllowSessionProvisioning`, the
overlay on/docked Left, and the built-in keyboard primitives (`chars:` for single characters,
`shiftdown:`/`shiftup:` for modifier chords). Drive the whole tour promptly in one pass; the controller
stays up while idle, but do not dawdle. When finished, tear it down:

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1 -Stop
```

`-Stop` only kills the hero controller(s) (mcec running from a `mcec-hero-controller-*` temp dir) and
removes those copies; other MCEC instances (yours, or another agent's) are left running.

The controller is a **non-installed** copy on purpose: the Program Files install refuses the MCP/HTTP
front door by design (`Program.IsProgramFilesInstall`).

## The playbook (the agent drives, all via MCP tools)

Coordinates are absolute screen pixels (the space `query`/`displays` report). Compute them from the two
observations below; do not hard-code them. Keystrokes go through MCEC's native keyboard primitives (no
custom commands): `send_command { "command": "<char>" }` types any single character (the `chars:`
built-in), and `shiftdown:<mod>` / `shiftup:<mod>` hold and release a modifier (`lwin`, `alt`, `ctrl`,
`shift`) around it. Dialog buttons and menu items are reached with `click`/`invoke` by name.

1. **Screen size.** `displays` → take the primary monitor's width `SW` and height `SH`.
2. **Provision the subject.** `provision-session { "mcpServer": false }` → keep `exePath`, `sessionId`,
   `token`. This is the isolated copy being toured; it replaces any hand-managed subject directory.
3. **Clear the backdrop, then launch.** Send **Win+D** to minimize other windows so they are not in frame
   (the controller is hidden and its overlay is a tool window, so both survive): `shiftdown:lwin`, then
   `d`, then `shiftup:lwin`. Do this before the subject exists so it is not minimized too. Then
   `launch { "path": <exePath>, "timeout": 8000 }` → keep the returned window `handle`; drive the subject
   by that handle from here on (the controller also owns an "MCEC" window, so a title match is ambiguous).
   Wait ~1.5–2 s for the window and menu bar to build.
4. **Read its bounds.** `query { "handle": <handle>, "maxDepth": 1 }` → the window rect `WX, WY, WW, WH`.
   Derive everything below from these (nothing is pinned).
5. **Clear any stray desktop popup BEFORE recording.** A leftover right-click context menu on the desktop
   will otherwise sit in frame. Dismiss it with a click well OUTSIDE its bounds: the subject's lower-right
   client area is safely clear of a top-left menu. `click { "at": { "x": WX+WW-60, "y": WY+WH-60 } }` (a
   click outside a menu dismisses it; Escape does not reliably reach a desktop menu).
6. **Start recording.** Record the left band, full height (so the overlay's narration column stays in
   frame wherever the window sits) out to the window's right edge:
   `record { "action": "start", "x": 0, "y": 0, "width": min(SW, WX+WW+12), "height": SH, "fps": 4, "maxWidth": 560 }`.
   Then `capture { "handle": <handle> }` and dwell ~0.6 s so the opening frame settles.
7. **Tour Settings, every tab (Agent is second).** `click { "handle": <handle>, "at": { "by": "name", "value": "File" } }`
   → `send_command { "command": "s" }` (the Settings mnemonic an open WinForms menu exposes only to the
   keyboard; a single char via `chars:`). Then for each of **General, Agent, Client, Server, Serial Server,
   Activity Monitor**: `click { "window": "Settings", "at": { "by": "name", "value": <tab> } }`, dwelling
   ~0.65 s. Close the dialog by clicking its button:
   `click { "window": "Settings", "at": { "by": "name", "value": "Cancel" } }`.
8. **Resize ~25% smaller.** Drag the bottom-right sizing border inward:
   `drag { "handle": <handle>, "from": { "x": WX+WW-2, "y": WY+WH-2 }, "to": { "x": WX+0.75*WW, "y": WY+0.75*WH } }`.
9. **Move in a small circle.** Drag the title bar around a small circular `path` and back:
   grab at `{ x: WX+0.375*WW, y: WY+12 }`, with `path` waypoints stepping around a ~55 px circle just
   below the grab point, and `to` the last point.
10. **Help ▸ About.** `click { "handle": <handle>, "at": { "by": "name", "value": "Help" } }` →
    `send_command { "command": "a" }` (the About mnemonic) → `capture { "window": "About" }`, then dwell
    ~1 s on it.
11. **Stop and write the GIF.** `record { "action": "stop", "file": "docs/hero.gif" }`.
12. **Close the subject, then tear down.** Close the About box:
    `click { "window": "About", "at": { "by": "name", "value": "OK" } }`. Close the subject via its menu:
    `click { "handle": <handle>, "at": { "by": "name", "value": "File" } }` → `send_command { "command": "x" }`
    (the Exit mnemonic) so its exe exits and releases the directory. Then
    `end-session { "sessionId": <sessionId>, "token": <token> }` removes it (the age-reaper is the fallback
    if anything is still locked). Finally run `-Stop` to remove the controller copy.

Because `provision-session` copies the **controller's own binaries** into the subject, the subject is
stamped with the controller's build. GitVersion bakes the current branch name into that stamp, and it is
visible in the hero (the subject's log window, status bar, About box); any branch is fine, so just be
aware of which build you are recording.

## Gotchas (learned recording it)

- **Emergency stop.** If any tool returns `error.code:emergency-stopped`, the operator hit the panic
  hotkey (default `Ctrl+Alt+Shift+S`) and deliberately halted the session; a refused `record stop` will
  not overwrite the committed GIF. Stop and tell the operator; do **not** retry until they re-arm.
- **Clean backdrop.** Step 3's Win+D (`shiftdown:lwin` + `d` + `shiftup:lwin`) minimizes other windows so
  only the subject and overlay are in frame; the agent does it with the keyboard primitives, no dedicated
  tool or operator step.
- **Stray desktop menu.** A leftover right-click context menu on the desktop will still sit in frame (Win+D
  shows the desktop but does not close an open menu). Step 5 dismisses it with a click outside its bounds;
  if a take still catches one, re-record.
- **Modal-on-self.** Invoking a menu item that opens a modal can wedge later UIA queries of that dialog,
  so open Settings/About with the mnemonic keystroke (`s`/`a`) rather than `invoke`; the keystroke is a
  valid creative composition.
- **Overlay side.** The controller's overlay is docked Left so it lands in the recorded band; the subject
  runs no commands of its own, so its overlay stays quiet.

## Verify, then commit

The subject's log window is in frame (marketing surface) and the recording doubles as a bug-finding
channel. Before committing, extract a few keyframes (System.Drawing `SelectActiveFrame`; read the old GIF
via bash `git show HEAD:docs/hero.gif > f`, since PowerShell pipes mangle binary) and confirm the shot is
clean (no stray menus, expected tabs, readable log). Commit `docs/hero.gif` on the operator's say-so.

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The tour is
tuned to ≈4 MB at 560 px wide, 4 fps. To shrink it, lower `fps`, lower `maxWidth`, or trim the per-step
dwell; to make it richer, raise them.
