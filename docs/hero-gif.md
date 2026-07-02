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

## One-shot regeneration

On an interactive Windows session you can leave alone for ~30 seconds (it drives the real mouse,
keyboard, and launches an app):

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1        # add -Config Release to use a Release build
```

The script rebuilds (so the version stamp matches the checkout), produces `docs/hero.gif`, and restores
config afterward. Because the stamped version string appears in the recorded log window, status bar, and
About box, the script refuses to record from any branch but `develop` (override with
`-AllowNonDevelopBuild` when a branch build is deliberate). Review the result (including the log
window's contents frame-by-frame; it is part of the shot) and if it looks good, commit `docs/hero.gif`.

## What the script does (and why)

It is the executable form of these decisions; replicate them if reproducing by hand:

1. **GUI controller that renders the overlay, driven over HTTP.** Unlike a headless `--mcp` controller, a
   **GUI** MCEC has a message loop and so paints the overlay. The controller's co-located `mcec.settings`
   sets `McpServerEnabled=true` (the localhost HTTP floor), `AgentCommandsEnabled=true`,
   `CommandOverlayEnabled=true`, and `CommandOverlayPosition=Left`; the driver POSTs JSON-RPC tool calls
   to `http://127.0.0.1:5151/mcp`.
2. **Separate, isolated subject copy.** The controlled MCEC is a *copy* of the build in
   `%TEMP%\mcec-hero-subject` so it reads its own co-located `mcec.settings` (`Program.ConfigPath` == the
   exe's folder); isolated from the controller and your installed MCEC. Its config sets `ActAsServer=false`
   (else it binds `IPAddress.Any:5150` and triggers the first-run Windows Firewall prompt that steals focus
   and derails the tour), `DisableUpdatePopup=true`, turns its **own overlay off**
   (`CommandOverlayEnabled=false`; only the controller narrates), and **pins** `WindowLocation`/`WindowSize`.
3. **Launch it the way an agent would: using the direct `launch` tool (not `Start-Process` or Win+R).** The controller dogfoods the first-class gated launch: it calls `launch { "path": "<exe>", "timeout": 10000 }` which starts the process and returns the pid + primary window handle (when it appears). This is more reliable than the Run dialog. (The script falls back to the Win+R composition only if needed for other demos.) The freshly-launched window handle is used to target the subject thereafter (its "MCEC" title is ambiguous with the controller) and the modal dialogs by their unambiguous titles (`Settings`, `About`).
4. **Overlay docked Left over a wide window → compact capture.** With the overlay on the left of the wide,
   pinned, left-docked subject window, the recorded region is **just the window** (compact, no wallpaper)
   yet still contains the narration. The Settings/About dialogs are `CenterParent`, so they sit to the
   right of the left-hugging overlay. (Right is the product default; Left is chosen here only for the hero.)
5. **Clean backdrop.** All windows are minimized (`Shell.Application.MinimizeAll()`); the overlay is an
   independent top-most window and survives, so the controller's window stays minimized (out of frame)
   while its overlay keeps narrating.
6. **Record.** `record action:start` over the window region at a low fps, then drive the tour (Settings
   tabs → resize-drag → title-bar circles → About), then `record action:stop file:docs/hero.gif`.

## Manual MCP equivalent (no script)

Connect to the GUI controller's HTTP floor (`POST :5151/mcp`) once the subject's window is up (drive it
by its `handle`):

| Step | Tool call |
|------|-----------|
| Launch | `launch { "path": "<exe path>", "timeout": 10000 }` (returns `handle` + `window` in result; preferred). Fallback composition: `send_command winr` → `send_command "chars:<path>"` ... → `query { foreground:true }`. |
| Start | `record` `{ action:"start", x, y, width, height, fps:4, maxWidth:560 }` (region = the subject window's pinned rect) |
| Settings | click **File** → send `S` → `query` the **Settings** window → click each tab header's rect (`mouse:mt,…` + `mouse:lbc`) in turn → `Esc` |
| Resize | drag the bottom-right sizing border inward with one atomic `mouse:drag,x1,y1,…,xN,yN` (corner → a few waypoints inward) |
| Move | drag the title bar in a circle with one atomic `mouse:drag,x1,y1,…` (title bar → points around a small circle → back) |
| About | re-`query` the (moved) window by `handle` → click the **Help** menu item's rect → send `A` → `capture` `{ window:"About" }` |
| Stop | pause on the About box → `record` `{ action:"stop", file:"docs/hero.gif" }` |

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The deeper
tour is tuned to ≈4 MB at 560 px wide, 4 fps. To shrink it, lower `fps`, lower `maxWidth`, or trim the
per-step dwell `Start-Sleep`s; to make it richer, raise them.

## Toward script-free recreation

A key aspect of MCEC is that a capable **agent composes the full command set creatively**; so the right
question isn't "can an agent recreate the hero?" but "how *robustly*?" The honest answer: **an agent can
already recreate almost all of this today with nothing but `mcec.exe`**, by combining existing primitives.
The `.ps1` reaches outside MCEC mainly for **determinism and convenience**, not because MCEC can't express
the step.

Most of what the tour needs is now **first-class**: `launch` starts the subject and returns its pid +
window handle; `displays` reports per-monitor pixel bounds and DPI so `query`'d bounds convert cleanly to
clicks and drags; the `drag` tool (and raw `mouse:drag`) does atomic press → move-path → release in screen
pixels; and `invoke` with `action: "select"` switches tabs/list items. What remains composition: wait for a
window by polling `query`, and record a window by passing its `query`'d bounds as the `record` region.

One caveat: invoking a menu item that opens a **modal** can wedge later UIA queries of that dialog, so the
tour opens Settings with a keystroke (`key_s`) rather than `invoke`; the keystroke *is* a valid creative
composition.

Deliberately **out of scope** for the agent: provisioning the isolated subject (copy + config) and
enabling the actuation gates (`AgentCommandsEnabled`, per-command `Enabled`, and the overlay settings). An
agent enabling its own input would defeat the security model, so an operator does that first; the agent's
recipe begins after.
