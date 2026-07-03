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

## The agent-driven flow

The hero is meant to be recreated the way any capable agent would do it: **from a natural-language brief,
using MCEC's own tools, with the isolated subject supplied by
[`provision-session`](safety-emergency-stop-and-provisioning.md)** rather than a hand-managed copy. There
is exactly one human step, and it is a click, not a config file:

1. **Operator opts in.** In the controller MCEC, open **File ▸ Settings ▸ Agent** and check
   **Allow agents to provision disposable instances**. This is the only authorization the agent cannot
   self-serve; everything below is the agent's to do. (The controller must be a **non-installed** build:
   the Program Files install refuses the MCP/HTTP front door by design, so run the freshly built
   `src/bin/<Config>/net10.0-windows/mcec.exe`, or a copy of the install, as the controller.)
2. **Give the agent the brief.** Connected to the controller over MCP, tell it:

   > Record the MCEC hero. Provision a disposable MCEC instance for the subject, launch it, and record a
   > short tour of its own window: open File ▸ Settings and visit every tab, close it, mouse-resize the
   > window about 25% smaller by dragging its bottom-right sizing border, drag the title bar in a small
   > circle, then open Help ▸ About and pause on it. Keep the overlay narrating on the left. Write the
   > result to `docs/hero.gif`, then end the session.

3. **The agent executes it** with the tool sequence below, then calls `end-session` to delete the subject.

Because `provision-session` copies the **controller's own binaries** into the subject, the subject is
stamped with the controller's build. GitVersion bakes the current branch name into that stamp, and it is
visible in the hero (the subject's log window, status bar, and About box); any branch is fine, so just
be aware of which build you are recording. Review the result, including the log window's contents
frame-by-frame (it is part of the shot), and commit `docs/hero.gif` if it looks good.

## MCP tool sequence

Connect to the controller's HTTP floor (`POST :5151/mcp`), or drive it over stdio. Every step is a
first-class tool call; there is no hand-rolled coordinate math or config-file editing.

| Step | Tool call |
|------|-----------|
| Provision the subject | `provision-session { mcpServer: false }` → returns `{ exePath, sessionId, token, directory }`; a fresh, isolated instance with an agent-ready co-located config. Replaces the old hand-copied subject dir. |
| Launch it | `launch { path: <exePath>, timeout: 8000 }` → returns the subject's `handle`; drive it by that handle thereafter (the controller also owns an "MCEC" window, so a title match is ambiguous). |
| Observe where it landed | `query { handle: <handle>, maxDepth: 1 }` → the window bounds; derive the record region and drag points from these (nothing is pinned). |
| Start recording | `record { action: "start", x, y, width, height, fps: 4, maxWidth: 560 }` (region = the subject's rect, out to its right edge, full height so the overlay's narration column stays in frame). |
| Settings | `click { handle, at: { by: "name", value: "File" } }` → `send_command key_s` (the mnemonic an open WinForms menu exposes to the keyboard) → for each of General, Agent, Client, Server, Serial Server, Activity Monitor: `click { window: "Settings", at: { by: "name", value: <tab> } }` → `send_command key_esc`. |
| Resize | `drag { handle, from: { bottom-right sizing corner }, to: { ~25% inward } }`. |
| Move | `drag { handle, from: { title bar }, path: [ ...points around a small circle ], to: { start } }`. |
| About | `click { handle, at: { by: "name", value: "Help" } }` → `send_command key_a` → `capture { window: "About" }` → pause. |
| Stop | `record { action: "stop", file: "docs/hero.gif" }`. |
| Tear down | `end-session { sessionId, token }` (after the subject's `mcec.exe` exits) → deletes the subject directory. |

## Reducing the ceremony (`scripts/Generate-HeroGif.ps1`)

The script is now a **thin helper**, not a 250-line driver: it builds the controller, prints its version
stamp (which appears in the hero) for reference, and prints the brief above for an agent to execute. The
tour itself is the agent's job, using the tools in the table; the script no longer hand-manages a subject
copy or flips the controller's gates (`provision-session` and the Agent-tab opt-in do that now).

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1        # add -Config Release to use a Release build
```

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The deeper
tour is tuned to ≈4 MB at 560 px wide, 4 fps. To shrink it, lower `fps`, lower `maxWidth`, or trim the
per-step dwell; to make it richer, raise them.

## Remaining rough edges

- **Overlay side and the subject's own overlay.** The hero wants only the controller's overlay narrating,
  docked Left. A provisioned subject enables its own overlay (auditability) docked Right by default;
  overlay side/visibility are file-only settings not yet exposed to `provision-session`. Until they are,
  a pixel-perfect hero either tolerates the subject's idle overlay or docks the controller's overlay Left
  and records only the controller's narration column. Widening `provision-session` to accept overlay
  options is the natural follow-up that removes the last reason to touch a config file.
- **Modal-on-self.** Invoking a menu item that opens a **modal** can wedge later UIA queries of that
  dialog, so the tour opens Settings/About with a keystroke (`key_s`/`key_a`) rather than `invoke`; the
  keystroke *is* a valid creative composition.
