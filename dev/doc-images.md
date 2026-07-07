# Regenerating doc images (MCEC dogfood)

Maintainer doc ([`dev/`](README.md)); not published to GitHub Pages. Every **UI screenshot and demo GIF**
committed under `docs/` is produced by **MCEC driving itself**:
`capture` for stills, `record` for animations. No hand-cropped external screenshots, no in-process
`DrawToBitmap` test harnesses, and no designer-only renders for committed assets.

Static **brand assets** (`Guillen256x256.png`, `assets/favicon.png`) are exported from
`src/Resources/Guillen.Icon.ico` and are not runtime captures.

> **Shared bootstrap, MCP envelope, and targeting gotchas** live in [Examples](examples.md) (read
> "Targeting gotchas that bite every recipe" before any pass). Stand up the first authorized controller
> with [`scripts/Generate-HeroGif.ps1`](../scripts/Generate-HeroGif.ps1) (prints `HERO_MCP_URL=`).

## Catalog

| Asset | Where used | How |
|-------|------------|-----|
| `hero.gif` | `index.md`, `README.md` | `record` — [hero-gif.md](hero-gif.md) |
| `paint-smiley-email.gif` | `paint-smiley-email.md` | `record` — [paint-smiley-email.md](paint-smiley-email.md) |
| `settings_general.png` … `settings_activity.png` | `configuration.md`, `remote_control.md`, `agent_control.md` | `capture` — [Settings tabs](#settings-dialog-tabs) |
| `provision_handoff.png` | `agent_control.md` | `capture` — [Provision handoff](#provision-new-handoff-dialog) |
| `commands_enable.png`, `commands_test.png` | `remote_control.md`, `examples.md` | `capture` — [Commands window](#commands-window) |
| `telemetry_optin.png` | `telemetry.md` | `capture` — [Installer telemetry page](#installer-telemetry-opt-in) |

After every `capture`, **crop the Win11 `PrintWindow` frame**: 8px black bands on left/right/bottom
(FixedDialog 491×412 → 475×404). Example:

```powershell
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($rawPath)
$srcW = $img.Width - 16; $srcH = $img.Height - 8
$bmp = New-Object System.Drawing.Bitmap $srcW, $srcH
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($img, (New-Object System.Drawing.Rectangle 0,0,$srcW,$srcH),
  (New-Object System.Drawing.Rectangle 8,0,$srcW,$srcH), [System.Drawing.GraphicsUnit]::Pixel)
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
```

Teardown when finished: `Generate-HeroGif.ps1 -Stop` and confirm no stray `mcec.exe` under your subject
or session dirs.

---

## Settings dialog tabs

`docs/settings_*.png` (General, Agent, Client, Server, Serial Server, Activity Monitor).

1. **Build + bootstrap** (see above). Copy the Debug build to a throwaway **subject** dir and delete any
   co-located `mcec.settings` so the subject boots to fresh-install defaults.
2. **Launch the subject** (`launch { path: <subject exe> }`). Dismiss a Windows Firewall prompt if it
   steals foreground (`processName == "PickerHost"` → `click` **Cancel**).
3. **Open Settings** on the subject `handle`: `click { handle, at:{ by:name, value:File } }` →
   `send_command s` (Settings mnemonic). Verify `query { foreground:true }` → `title == "Settings"` and
   `processName == "mcec"` (not the Windows Settings app).
4. **Capture each tab** to the repo's absolute `docs/` path:
   - **General** — selected on open; `capture { handle, file }` immediately.
   - **Agent / Client / Server** — `click { handle, at:{ by:name, value:<tab> } }` (or coordinate-click
     the `TabItem` if by-name is ambiguous), dwell, `capture`.
   - **Serial Server / Activity Monitor** — by-name substring-hits **Server**; `TabItem` bounds are
     null, so coordinate-click the tab header from the window rect (see [Examples](examples.md)), then
     capture.
5. **Crop** each PNG (see above). Re-view every file before committing — the wrong tab looks plausible.

When **editing dialog layout**, iterate with an in-process render harness in xUnit (`new SettingsDialog`,
`DrawToBitmap` offscreen) to size tabs, then do one MCEC pass for the committed PNGs.

---

## Provision new… handoff dialog

`docs/provision_handoff.png` in [agent_control.md](agent_control.md).

The subject must have **`AllowSessionProvisioning=true`** (write it into the subject's co-located
`mcec.settings` before launch, or tick the checkbox on the Agent tab first).

1. Bootstrap + launch subject (as above).
2. Open **Settings** (File → Settings) and select the **Agent** tab (`click` the `TabItem` by coordinate
   or `automationId: _tabPageAgent` if by-name fails).
3. **`click { handle, at:{ by:automationid, value:_buttonProvision } }`** (the button's accessible name is
   `Provision new...`, not `Provision new`). Wait for the handoff dialog (`title` contains
   `Provisioned instance`).
4. **`capture { handle:<handoff>, file:<repo>/docs/provision_handoff.png }`**, then crop.

---

## Commands window

`docs/commands_enable.png` (main list) and `docs/commands_test.png` (test client pane).

1. Bootstrap + launch a fresh-default **subject**.
2. Open the Commands window on the subject `handle`: `click { handle, at:{ by:name, value:Commands } }`
   → `send_command c` (the **Enable and Test Commands…** mnemonic under **Commands**).
3. Verify foreground / `query` → title contains **Commands**.
4. **`commands_enable.png`** — `capture` the window as opened (command list visible).
5. For **`commands_test.png`**, configure test mode first (still on the subject): open Settings → **Client**
   (enable, `localhost`) and **Server** (enable), OK; re-open the Commands window if needed; `capture`
   again so the send/test panes are visible.
6. Crop both PNGs.

---

## Installer telemetry opt-in

`docs/telemetry_optin.png` in [telemetry.md](telemetry.md).

1. Bootstrap the controller (agent commands + `capture` enabled).
2. **Launch the signed installer** from a release build (`launch { path: <MCEControllerSetup.exe> }`) or
   run the NSIS output under `Installer/`.
3. Step through the wizard until the **telemetry opt-in** page is foreground; `query { foreground:true }`
   to confirm.
4. **`capture { handle, file:<repo>/docs/telemetry_optin.png }`**. Crop if the installer window shows a
   `PrintWindow` frame band.
5. Cancel the installer (do not complete install on a machine you care about).

---

## Animated examples

| Doc | Recipe |
|-----|--------|
| [hero-gif.md](hero-gif.md) | Full numbered `record` tour of a provisioned subject |
| [paint-smiley-email.md](paint-smiley-email.md) | Prompt demo; `record` one good run |

Both assume the controller bootstrap and disposable-subject pattern in [Examples](examples.md).