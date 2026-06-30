# Recreating the hero GIF (`docs/hero.gif`)

`docs/hero.gif` is the hero image shown at the top of [`README.md`](../README.md) and the docs home page
([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is MCEC dogfooding
itself: one MCEC drives a **second MCEC** through a guided tour — launch → **File ▸ Settings** (visit
every tab) → **mouse-resize** the window ~25% smaller by dragging its sizing border → **drag the title
bar in small circles** → **Help ▸ About** → pause — while the **on-screen command overlay** (#119)
narrates every command in burnt orange, all recorded by the agent
[`record`](agent-server.md#record--capturing-change-over-time) tool (#80). No external screen-recorder.

The two oranges line up on purpose: the overlay's item background is the **About box's brand orange**,
so the About dialog and the narration match.

## One-shot regeneration

On an interactive Windows session you can leave alone for ~30 seconds (it drives the real mouse,
keyboard, and launches an app):

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1        # add -Config Release to use a Release build
```

The script builds if needed, produces `docs/hero.gif`, and restores config afterward. Review the result;
if it looks good, commit `docs/hero.gif`.

## What the script does (and why)

It is the executable form of these decisions — replicate them if reproducing by hand:

1. **GUI controller that renders the overlay, driven over HTTP.** Unlike a headless `--mcp` controller, a
   **GUI** MCEC has a message loop and so paints the overlay. The controller's co-located `mcec.settings`
   sets `McpServerEnabled=true` (the localhost HTTP floor), `AgentCommandsEnabled=true`,
   `CommandOverlayEnabled=true`, and `CommandOverlayPosition=Left`; the driver POSTs JSON-RPC tool calls
   to `http://127.0.0.1:5151/mcp`.
2. **Separate, isolated subject copy.** The controlled MCEC is a *copy* of the build in
   `%TEMP%\mcec-hero-subject`, launched from there so it reads its own co-located `mcec.settings`
   (`Program.ConfigPath` == the exe's folder) — isolated from the controller and your installed MCEC. Its
   config sets `ActAsServer=false` (else it binds `IPAddress.Any:5150` and triggers the first-run Windows
   Firewall prompt that steals focus and derails the tour), `DisableUpdatePopup=true`, turns its **own
   overlay off** (`CommandOverlayEnabled=false` — only the controller narrates), and **pins**
   `WindowLocation`/`WindowSize`.
3. **Target the subject by handle.** The controller now also has an "MCEC"-titled window, so a title match
   is ambiguous; the script drives the subject's main window by **handle** (`query`/`capture` `{ handle }`)
   and the modal dialogs by their unambiguous titles (`Settings`, `About`).
4. **Overlay docked Left over a wide window → compact capture.** With the overlay on the left of the wide,
   pinned, left-docked subject window, the recorded region is **just the window** — compact, no wallpaper —
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
| Start | `record` `{ action:"start", x, y, width, height, fps:4, maxWidth:560 }` (region = the subject window's pinned rect) |
| Settings | click **File** → send `S` → `query` the **Settings** window → click each tab header's rect (`mouse:mt,…` + `mouse:lbc`) in turn → `Esc` |
| Resize | drag the bottom-right sizing border inward: `mouse:mt` to the corner → `mouse:lbd` → a few `mouse:mt` moves → `mouse:lbu` |
| Move | drag the title bar in circles: `mouse:mt` onto the title bar → `mouse:lbd` → `mouse:mt` around a small circle → `mouse:lbu` |
| About | re-`query` the (moved) window by `handle` → click the **Help** menu item's rect → send `A` → `capture` `{ window:"About" }` |
| Stop | pause on the About box → `record` `{ action:"stop", file:"docs/hero.gif" }` |

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The deeper
tour is tuned to ≈4 MB at 560 px wide, 4 fps. To shrink it, lower `fps`, lower `maxWidth`, or trim the
per-step dwell `Start-Sleep`s; to make it richer, raise them.

## Toward script-free recreation

The point of MCEC is that an **agent** drives complex GUI tasks easily — so the goal is that an agent
reads *this file* and recreates the hero with **nothing but `mcec.exe`**, no `.ps1`. The script is a
stopgap for the steps MCEC can't yet express as agent tool calls. What it does outside the agent
surface, and the capability that would remove each (tracking issues):

| Script does (not via MCEC) | Needed capability | Issue |
|---|---|---|
| `GetSystemMetrics`/`SetProcessDPIAware`, then normalizes every pixel to `mouse:mt`'s 0–65535 space | pixel-/element-relative mouse targeting + a `displays` query (the keystone — `query` gives pixels, `mouse` wants normalized) | #122 |
| `mouse:lbd` + a stream of `mouse:mt` + `mouse:lbu` for the resize and the circular move | a first-class `drag` action | #123 |
| Resize 25% / move by dragging the chrome; pins `WindowLocation`/`WindowSize`; `Shell.MinimizeAll()` | window-management actions (move/resize/min/max/foreground; clean-desktop) | #124 |
| Clicks each Settings tab header by computed pixel center | a `select` action (SelectionItem) — `invoke` is a no-op on tabs | #125 |
| `Start-Process` to launch the subject | a gated launch action | #126 |
| Computes a fixed `{x,y,w,h}` record region | `record { handle }`, optionally following the window | #127 |
| Polls `query` for the subject window / Settings dialog to appear | wait-for-**window** predicate | #112 |

Menus **are** already script-free in principle (`invoke … action:expand` then `invoke` the item), but
the tour still opens Settings with a keystroke because invoking a menu item that opens a **modal**
dialog currently wedges later UIA queries of that dialog (#128) — needed here to read the tab bounds.

Deliberately **out of scope** for the agent: provisioning the isolated subject and enabling the
actuation gates (`AgentCommandsEnabled`, per-command `Enabled`). An agent enabling its own input would
defeat the security model, so an operator does that first; the agent's recipe begins after.
