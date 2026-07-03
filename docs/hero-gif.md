# Recreating the hero GIF (`docs/hero.gif`)

> **Flavor:** Scripted recipe. One of the worked [Examples](examples.md); the shared bootstrap, MCP
> envelope, and targeting gotchas live there and aren't repeated here.

`docs/hero.gif` is the hero image shown at the top of [`README.md`](../README.md) and the docs home page
([`docs/index.md`](index.md), served at `https://tig.github.io/mcec/hero.gif`). It is MCEC dogfooding
itself: one MCEC drives a **second MCEC** through a guided tour (launch → **File ▸ Settings** (visit
every tab) → **mouse-resize** the window ~25% smaller by dragging its sizing border → **drag the title
bar in small circles** → **Help ▸ About** → pause) while the **on-screen command overlay**
narrates every command in burnt orange, all recorded by the agent
[`record`](environment-controller.md#record--capturing-change-over-time) tool. No external screen-recorder.

The two oranges line up on purpose: the overlay's item background is the **About box's brand orange**,
so the About dialog and the narration match.

## How it is made

An **agent drives MCEC over MCP** and does the whole tour itself; **no tour-logic script ships in the
repo**. One MCEC is the **controller** the agent is connected to; the agent uses the controller's tools to
[`provision-session`](safety-emergency-stop-and-provisioning.md) a disposable **subject** MCEC, `launch`
it, tour it, and `record` the region while the controller's overlay narrates. The only scripted step is
standing up that first controller, because an agent cannot bootstrap it over MCP (there is nothing to
connect to yet).

This page is written so a **fresh agent session can reproduce the hero by reading it**. An agent with MCEC
mounted as native MCP tools calls them directly; an agent without one (most IDE agents) writes a thin
JSON-RPC wrapper to POST to the controller (see the reference call in the playbook). Either way the
choreography is the agent's, not a shipped driver's; only the one bootstrap command below is in-repo.

## Setup (the only script)

[`scripts/Generate-HeroGif.ps1`](../scripts/Generate-HeroGif.ps1) builds MCEC and stands up an authorized,
MCP-serving controller from a **disposable copy** of the build (so `src/bin` is never mutated), then
prints its endpoint:

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1
# ...
#   MCP endpoint : http://127.0.0.1:<free-port>/mcp   (a free port is chosen; -Port pins one)
#   Register it  : claude mcp add --transport http mcec http://127.0.0.1:<free-port>/mcp
#
# HERO_MCP_URL=http://127.0.0.1:<free-port>/mcp        <- machine-readable; grep these, don't parse prose
# HERO_MCP_PORT=<free-port>
# HERO_MCP_PID=<pid>
```

A driver reads the endpoint from the `HERO_MCP_URL=` line (e.g. `... | Select-String '^HERO_MCP_URL=(.+)$'`).

Register the printed endpoint (or POST JSON-RPC to it directly, see the playbook note below) so your agent
has MCEC's tools (`provision-session`, `launch`, `query`, `click`, `drag`, `record`, `capture`,
`displays`, `send_command`, `end-session`), then ask it to recreate the hero. The controller's config
enables everything the tour needs: the agent surface, per-command `Enabled` for the tools it calls
(`query`/`click`/`drag`/`record`/`capture`/`launch`/`displays`; agent tools are gated by BOTH
`AgentCommandsEnabled` and their own table entry), `AllowSessionProvisioning`, the overlay on/docked Left,
and the built-in keyboard primitives. (`send_command` itself is a meta-tool and needs no `Enabled` row;
only the raw commands it runs do, so the config enables `chars:` for single characters and
`shiftdown:`/`shiftup:` for modifier holds.) The controller is launched **detached** (via WMI, outside the
launcher's job) so it survives this script exiting; still, drive the tour in one prompt pass. When
finished, tear it down:

```powershell
pwsh -NoProfile -File scripts/Generate-HeroGif.ps1 -Stop
```

`-Stop` only kills the hero controller(s) (mcec running from a `mcec-hero-controller-*` temp dir) and
removes those copies; other MCEC instances (yours, or another agent's) are left running.

The controller is a **non-installed** copy on purpose: the Program Files install refuses the MCP/HTTP
front door by design (`Program.IsProgramFilesInstall`).

## The playbook (the agent drives, all via MCP tools)

Coordinates are absolute screen pixels (the space `query`/`displays` report); compute them from the two
observations below, do not hard-code them, and **send integers** (a tool expecting a pixel rejects
`767.0`). Keystrokes go through the `send_command` meta-tool and MCEC's native keyboard primitives:
`send_command { "command": "<char>" }` types any single character (the `chars:` built-in), and
`send_command { "command": "shiftdown:<mod>" }` / `"shiftup:<mod>"` hold and release a modifier (`lwin`,
`alt`, `ctrl`, `shift`) around it. Dialog buttons and menu items are reached with `click` by name.

**Before you start, the things that trip agents up:**
- **Responses are double-wrapped.** A tool's envelope `{ ok, result, warnings?, error }` arrives as a JSON
  **string** inside the JSON-RPC `result.content[]` text block, NOT at the top level. Unwrap it, then
  branch on `ok` (not `success`); read `result` on success, `error.code` on failure. Canonical parser:
  `PayloadData` in [`AgentDesktopE2ETests.cs`](../tests/MCEControl.xUnit/Integration/AgentDesktopE2ETests.cs);
  full contract: [`agent-tool-result-contract.md`](design/agent-tool-result-contract.md).
- **Absolute paths.** The controller's working dir is its temp copy, so any repo path you pass a tool
  (notably `record stop`'s `file`) must be the repo's **absolute** path, or output lands in the temp copy
  and is lost.
- **Registration is optional.** `claude mcp add` is only for an interactive MCP client. An agent without
  MCEC mounted POSTs JSON-RPC straight to the printed URL (copy it whole, including `http://` and the
  port); that is how this playbook is driven.

**Reference call** (issue one tool and unwrap its envelope; every step below is this shape):

```powershell
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"displays","arguments":{}}}'
$rpc  = Invoke-RestMethod -Uri $McpUrl -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 30
$env  = ($rpc.result.content | Where-Object type -eq 'text').text | ConvertFrom-Json  # { ok, result, error }
if (-not $env.ok) { throw $env.error.code }
$env.result    # tool-specific payload; the field paths are named in each step
```

1. **Screen size.** `displays` → `result.displays[]`; take the entry with `primary:true`. Read
   `bounds.x/y/width/height` as `SX, SY, SW, SH` (on a secondary-as-primary setup `SX/SY` can be non-zero;
   the record region below uses them).
2. **Provision the subject.** `provision-session { "mcpServer": false }` → top-level in `result`:
   `exePath`, `sessionId`, `token`. This isolated copy is what you tour.
3. **Clear the backdrop, then launch.** Send **Win+D** to minimize other windows (the controller is hidden
   and its overlay is a tool window, so both survive), as three separate `send_command` calls:
   `{ "command": "shiftdown:lwin" }`, `{ "command": "d" }`, `{ "command": "shiftup:lwin" }`. Do this before
   the subject exists so it is not minimized too. Then `launch { "path": <exePath>, "timeout": 8000 }` →
   `result.handle`; drive the subject by that `handle` from here on (the controller also owns an "MCEC"
   window, so a title match is ambiguous). Wait ~1.5–2 s for the window and menu bar to build.
4. **Read its bounds.** `query { "handle": <handle>, "maxDepth": 1 }` → `result.window` gives `x, y, width,
   height` (the window rect, not the UIA tree) as `WX, WY, WW, WH`. Derive everything below from these.
5. **Clear any stray desktop popup BEFORE recording.** A leftover right-click context menu on the desktop
   would sit in frame. Use `click { "at": { "x": WX+WW-60, "y": WY+WH-60 } }`, a **coordinate-only** click
   with no `handle`/`window`, so it lands on whatever is at that desktop pixel (the subject's lower-right,
   clear of a top-left menu) and dismisses the menu. (Escape does not reliably reach a desktop menu.)
6. **Start recording.** Record the left band, full height, out to the window's right edge so the overlay's
   narration column stays in frame:
   `record { "action": "start", "x": SX, "y": SY, "width": min(SW, WX+WW+12-SX), "height": SH, "fps": 4, "maxWidth": 560 }`.
   On a primary at the origin `SX=SY=0`, so this is just `x:0, y:0`. Worked example: primary `1113×768` at
   `0,0`, subject at `(52,52) 1024×640` → `x:0, y:0, width: min(1113, 52+1024+12) = 1088, height:768`.
   Then `capture { "handle": <handle> }` and dwell ~0.6 s so the opening frame settles.
7. **Tour Settings, every tab (Agent is second).** `click { "handle": <handle>, "at": { "by": "name", "value": "File" } }`,
   wait ~0.3 s for the menu → `send_command { "command": "s" }` (the Settings mnemonic; a single char via
   `chars:`), wait ~0.6 s for the dialog. Then for each of **General, Agent, Client, Server, Serial Server,
   Activity Monitor**: `click { "window": "Settings", "at": { "by": "name", "value": <tab> } }`, dwelling
   ~0.65 s. Close with `click { "window": "Settings", "at": { "by": "name", "value": "Cancel" } }`.
8. **Resize ~25% smaller.** Drag the bottom-right sizing border inward (integer pixels):
   `drag { "handle": <handle>, "from": { "x": WX+WW-2, "y": WY+WH-2 }, "to": { "x": round(WX+0.75*WW), "y": round(WY+0.75*WH) } }`.
9. **Move in a small circle.** Re-`query { "handle": <handle> }` first (the resize changed the window; its
   top-left stayed put, but read fresh bounds to be safe). Grab the title bar at
   `G = { x: round(WX+WW*0.375), y: WY+12 }`; build a circular `path` of **integer** points
   `{ x: round(cx+r*cos θ), y: round(cy+r*sin θ) }` with `cx=G.x`, `cy=G.y+55`, `r=55`, for
   `θ = 0°,50°,…,720°` (two loops, ~15 points). Pass all but the last as `path`, the last as `to`:
   `drag { "handle": <handle>, "from": G, "path": [ {"x":123,"y":456}, {"x":140,"y":470}, … ], "to": <lastPoint> }`.
10. **Help ▸ About.** `click { "handle": <handle>, "at": { "by": "name", "value": "Help" } }`, wait ~0.3 s
    → `send_command { "command": "a" }` (the About mnemonic), wait ~0.8 s for the dialog →
    `capture { "window": "About" }`, then dwell ~1 s on it.
11. **Stop and write the GIF.** `record { "action": "stop", "file": "<repo>/docs/hero.gif" }` with the
    repo's **absolute** path (relative lands in the temp copy and is lost). It encodes and returns within a
    few seconds (not a long-poll), so a normal request timeout is fine. The `result` looks like:
    ```json
    { "ok": true, "result": { "frames": 41, "durationMs": 12960, "fps": 4,
      "width": 560, "height": 405, "bytes": 3725861, "file": "C:\\...\\docs\\hero.gif" } }
    ```
    Assert `frames` (≈35–50), `bytes` (≈3–4 MB), and that a file now exists at your absolute `file` path.
    Diagnostic: if `bytes > 0` but nothing is at the repo path, you passed a relative path and it wrote
    into the controller's temp copy.
12. **Close the subject.** Close the About box:
    `click { "window": "About", "at": { "by": "name", "value": "OK" } }`. Close the subject via its menu:
    `click { "handle": <handle>, "at": { "by": "name", "value": "File" } }` → `send_command { "command": "x" }`
    (the Exit mnemonic) so its exe exits and releases the directory. Then
    `end-session { "sessionId": <sessionId>, "token": <token> }` removes it (the age-reaper is the fallback
    if anything is still locked).
13. **Tear down the controller.** `pwsh -NoProfile -File scripts/Generate-HeroGif.ps1 -Stop` removes the
    throwaway controller copy so no orphan lingers.

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

The tour drives in ~20 s (the GIF is written by then; longer runs are just idle time, so don't pad with
dwells beyond those noted). The result is ≈3–4 MB. The subject's log window is in frame (marketing
surface) and the recording doubles as a bug-finding channel, so spot-check a few keyframes before
committing. Confirm: the Settings strip shows **Agent as the second tab** (the feature this hero exists to
show), no stray desktop menu is in frame, and the log window reads cleanly. The tab STRIP (Agent second)
is visible in any Settings frame, but the Agent tab is only SELECTED for a beat right after step 7's Agent
click, so to see its content (the provisioning checkbox + session list) extract the frame just after that
click, roughly frames 8–11 of a ~40-frame take, not just an every-12th sample (which tends to land on
General). Extract frames with:

```powershell
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("<repo>\docs\hero.gif")
$fd  = New-Object System.Drawing.Imaging.FrameDimension $img.FrameDimensionsList[0]  # NOT FrameDimension.Time
for ($f = 0; $f -lt $img.GetFrameCount($fd); $f += 12) {
  $img.SelectActiveFrame($fd, $f) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap $img.Width, $img.Height
  ([System.Drawing.Graphics]::FromImage($bmp)).DrawImage($img, 0, 0, $img.Width, $img.Height)
  $bmp.Save("$env:TEMP\hero_$f.png")
}
```

(To diff against the committed GIF, extract it via bash `git show HEAD:docs/hero.gif > f` first; PowerShell
pipes mangle binary.) Commit `docs/hero.gif` on the operator's say-so.

## Tuning size

The GIF encoder writes full (non-diffed) frames, so **file size ≈ frame count × frame area**. The tour is
tuned to ≈4 MB at 560 px wide, 4 fps. To shrink it, lower `fps`, lower `maxWidth`, or trim the per-step
dwell; to make it richer, raise them.

## Appendix: minimal JSON-RPC driver (local, not committed)

An agent without MCEC mounted as native tools writes a throwaway wrapper to POST to the controller. This
is the whole boilerplate; do **not** add it to the repo (the tour logic is the agent's, per above). Paste
it into a scratch file, set `$McpUrl` from the bootstrap's `HERO_MCP_URL=` line, and drive the numbered
steps through `Invoke-McecRpc`:

```powershell
$McpUrl = 'http://127.0.0.1:<port>/mcp'          # from the HERO_MCP_URL= line
$script:rpcId = 0
function Invoke-McecRpc([string]$name, $arguments = @{}) {
  $script:rpcId++
  $body = @{ jsonrpc = '2.0'; id = $script:rpcId; method = 'tools/call'
             params = @{ name = $name; arguments = $arguments } } | ConvertTo-Json -Depth 12 -Compress
  $rpc  = Invoke-RestMethod -Uri $McpUrl -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 30
  $env  = ($rpc.result.content | Where-Object type -eq 'text').text | ConvertFrom-Json  # unwrap the envelope
  if (-not $env.ok) { throw "$name failed: $($env.error.code)" }
  return $env.result                              # tool-specific payload
}
function Send-Cmd([string]$c) { $null = Invoke-McecRpc 'send_command' @{ command = $c } }
function Dwell([int]$ms)      { Start-Sleep -Milliseconds $ms }

# Steps 1-2:
$prim = (Invoke-McecRpc 'displays').displays | Where-Object primary
$ps   = Invoke-McecRpc 'provision-session' @{ mcpServer = $false }   # -> $ps.exePath / .sessionId / .token
# Step 3: Send-Cmd 'shiftdown:lwin'; Send-Cmd 'd'; Send-Cmd 'shiftup:lwin'
$h    = (Invoke-McecRpc 'launch' @{ path = $ps.exePath; timeout = 8000 }).handle
# ...continue with query/record/click/drag/capture per the numbered steps, then end-session...
```
