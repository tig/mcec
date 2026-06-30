# Recreating the hero GIF (`docs/hero.gif`)

`docs/hero.gif` is the hero image at the top of [`README.md`](../README.md) and the docs home page
([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is MCEC dogfooding
itself: one MCEC drives a **real GUI** — its own window — through **Help ▸ About** and **File ▸
Settings** using the agent tools, while the **on-screen command overlay** (#119) narrates every command
it runs in burnt orange. The whole thing is recorded by the agent
[`record`](agent-server.md#record--capturing-change-over-time) tool (#80) — no external screen recorder.

The two oranges line up on purpose: the overlay's item background is the **About box's brand orange**,
so the About dialog and the narration match.

## One-shot regeneration

On an interactive Windows session you can leave alone for ~15 seconds (it drives the real keyboard and
launches the app):

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1        # add -Config Release to use a Release build
```

The script builds if needed, produces `docs/hero.gif`, and restores config afterward. Review the result;
if it looks good, commit `docs/hero.gif`.

## What the script does (and why)

It is the executable form of these decisions — replicate them if reproducing by hand:

1. **One GUI MCEC, driven over HTTP.** Unlike a headless `--mcp` controller, a **GUI** MCEC has a
   message loop and so renders the overlay. The script sets `McpServerEnabled=true` (the localhost HTTP
   floor), `AgentCommandsEnabled=true`, and `CommandOverlayEnabled=true` in a **co-located**
   `mcec.settings` (`Program.ConfigPath` == the exe's folder), then drives that same instance by POSTing
   JSON-RPC tool calls to `http://127.0.0.1:5151/mcp`. The agent tools target the app's own window, so
   the overlay narrates MCEC operating MCEC.
2. **Overlay docked Left for a compact capture.** `CommandOverlayPosition=Left` puts the overlay over
   the left of a wide, pinned window, so the recorded region is **just the window** — compact and with no
   wallpaper — yet still contains the narration. (Right is the product default; Left is chosen here only
   to keep the hero tight.)
3. **Pinned window + clean backdrop.** `WindowLocation`/`WindowSize` are pinned so the recorded region is
   deterministic; `DisableUpdatePopup=true` and `ActAsServer=false` avoid the update prompt and the
   Windows Firewall prompt (both steal focus and would derail the automation). All other windows are
   minimized (`Shell.Application.MinimizeAll()`) and the MCEC window is then restored — the overlay is an
   independent top-most window and stays put.
4. **Record.** `record action:start` over the window region at a low fps, drive **Help ▸ About** (the
   orange About box) and **File ▸ Settings** with `query`/`capture`/`invoke` plus a couple of keystrokes,
   then `record action:stop file:docs/hero.gif`.

## Manual MCP equivalent (no script)

Connect to the GUI MCEC's HTTP floor (`POST :5151/mcp`) once its window is up:

| Step | Tool call |
|------|-----------|
| Start | `record` `{ action:"start", x, y, width, height, fps:4, maxWidth:680 }` (region = the window) |
| Observe | `query` / `capture` `{ window:"MCEC" }` |
| About | `invoke` `{ window:"MCEC", by:"name", value:"Help", action:"expand" }` → send `A` → `capture` `{ window:"About" }` → `Esc` |
| Settings | `invoke` `{ window:"MCEC", by:"name", value:"File", action:"expand" }` → send `S` → `capture` `{ window:"MCEC" }` → `Esc` |
| Stop | `record` `{ action:"stop", file:"docs/hero.gif" }` |

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. To shrink
the hero, lower `fps`, lower `maxWidth`, or trim the per-step dwell `Start-Sleep`s in the script. The
committed asset targets ≈4 MB at 680 px wide, 4 fps.
