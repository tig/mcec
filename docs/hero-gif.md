# Recreating the hero GIF (`docs/hero.gif`)

`docs/hero.gif` is the hero image shown at the top of [`README.md`](../README.md) and the docs
home page ([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is
MCEC dogfooding itself: a **headless controller** (`mcec.exe --mcp`) drives a **second MCEC** through
its whole life — launch → **Help ▸ About** → **File ▸ Settings** → **File ▸ Exit** — and records the
session to an animated GIF with the agent [`record`](agent-server.md#record--capturing-change-over-time)
tool added in #80. No external screen-recorder is involved.

## One-shot regeneration

On an interactive Windows session you can leave alone for ~20 seconds (it drives the real mouse,
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
4. **Clean backdrop.** All desktop windows are minimized (`Shell.Application.MinimizeAll()`) before
   recording, and the region is **cropped to the pinned window** (the About/Settings dialogs are
   `CenterParent`, so they fall inside it) — so the hero is just the app, no wallpaper, and compact.
5. **Record.** `record action:start` over the window region at a low fps, then drive the menus, then
   `record action:stop file:docs/hero.gif`.

## Manual MCP equivalent (no script)

Connect to the controller (`mcec.exe --mcp`) and, after the subject is launched and its window is up:

| Step | Tool call |
|------|-----------|
| Start | `record` `{ action:"start", x, y, width, height, fps:4, maxWidth:680 }` (region = the subject window) |
| About | `query` the window → click the **Help** menu item's rect (`send_command` `mouse:mt,…` + `mouse:lbc`) → send `A` |
| Settings | dismiss About (`Esc`) → click **File** → send `S` |
| Exit | dismiss Settings (`Esc`) → click **File** → send `X` |
| Stop | `record` `{ action:"stop", file:"docs/hero.gif" }` |

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. To
shrink the hero, lower `fps`, lower `maxWidth`, or trim the per-dialog dwell `Start-Sleep`s in the
script. The committed asset targets ≈4 MB at 680 px wide, 4 fps.
</content>
