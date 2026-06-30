# Recreating the hero GIF (`docs/hero.gif`)

`docs/hero.gif` is the hero image shown at the top of [`README.md`](../README.md) and the docs
home page ([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is
MCEC dogfooding itself: a **headless controller** (`mcec.exe --mcp`) drives a **second MCEC** through
a guided tour — launch → **File ▸ Settings** (visit every tab) → **mouse-resize** the window ~25%
smaller by dragging its sizing border → **drag the title bar in small circles** → **Help ▸ About** →
pause — and records the session to an animated GIF with the agent
[`record`](agent-server.md#record--capturing-change-over-time) tool added in #80. The resize and move
exercise the mouse-drag input path (button-down → a stream of absolute moves → button-up), not just
clicks. No external screen-recorder is involved.

## One-shot regeneration

On an interactive Windows session you can leave alone for ~30 seconds (it drives the real mouse,
keyboard, and launches an app):

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1        # add -Config Release to use a Release build
```

The script builds if needed, produces `docs/hero.gif`, and cleans up after itself. Review the result;
if it looks good, commit `docs/hero.gif`.

## What the script does (and why)

It is the executable form of these decisions — replicate them if reproducing by hand:

1. **Separate subject copy in its own directory.** The controlled MCEC is a *copy* of the build
   output in `%TEMP%\mcec-hero-subject`, launched from there. `Program.ConfigPath` is the exe's own
   folder, so the subject reads a **co-located `mcec.settings`** — isolated from the controller and
   from your installed MCEC. (A shared dir is a trap: a GUI MCEC rewrites `mcec.settings` on exit.)
2. **Subject config disables the server.** `ActAsServer=false` (its default is `true`). Otherwise the
   subject binds a listening socket on `IPAddress.Any:5150`, which triggers the first-run **Windows
   Defender Firewall** prompt — that prompt steals focus and derails the menu automation. Also
   `DisableUpdatePopup=true`, and `WindowLocation`/`WindowSize` are **pinned** so the recorded region
   is deterministic.
3. **Controller config enables the agent surface.** The `--mcp` controller's co-located config sets
   `AgentCommandsEnabled=true` and enables `capture`/`query`/`record`/`mouse` and the keystroke
   commands in `mcec.commands`. (The `--mcp` controller has no `MainWindow`, so it never starts the
   socket server and never prompts.)
4. **Backdrop.** All desktop windows are minimized (`Shell.Application.MinimizeAll()`) before
   recording. The recorded region is the window's **original** pinned rect; once the tour resizes and
   moves the window, the desktop wallpaper shows through the freed area — so whatever wallpaper is set
   becomes the hero's backdrop (a calm, low-detail one keeps the file smaller; a busy photo is fine but
   costs bytes). (The Settings/About dialogs are `CenterParent`, so they stay in frame.)
5. **Record.** `record action:start` over the window region at a low fps, then drive the tour (settings
   tabs, resize-drag, title-bar circles, About), then `record action:stop file:docs/hero.gif`.

## Manual MCP equivalent (no script)

Connect to the controller (`mcec.exe --mcp`) and, after the subject is launched and its window is up:

| Step | Tool call |
|------|-----------|
| Start | `record` `{ action:"start", x, y, width, height, fps:4, maxWidth:440 }` (region = the subject window's pinned rect) |
| Settings | click **File** → send `S` → `query` the **Settings** window → click each tab header's rect (`mouse:mt,…` + `mouse:lbc`) in turn → `Esc` |
| Resize | drag the bottom-right sizing border inward: `mouse:mt` to the corner → `mouse:lbd` → a few `mouse:mt` moves → `mouse:lbu` |
| Move | drag the title bar in circles: `mouse:mt` onto the title bar → `mouse:lbd` → `mouse:mt` around a small circle → `mouse:lbu` |
| About | re-`query` the (moved) window → click the **Help** menu item's rect → send `A` |
| Stop | pause on the About box → `record` `{ action:"stop", file:"docs/hero.gif" }` |

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The
deeper tour is tuned to ~13 s → ~42 frames → ≈2 MB at 440 px wide, 4 fps. To shrink it further, lower
`fps`, lower `maxWidth`, or trim the per-step dwell `Start-Sleep`s; to make it richer, raise them.
</content>
