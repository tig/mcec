# Regenerating the Settings dialog screenshots

> **Flavor:** Scripted recipe. The shared bootstrap, MCP envelope, and **targeting gotchas** live in
> [Examples](examples.md) and are not repeated here; read that "Targeting gotchas that bite every recipe"
> list first, it is what makes this work first-try.

`docs/settings_*.png` (General, Agent, Client, Server, Serial Server, Activity Monitor) are the Settings
dialog tab screenshots embedded in [configuration.md](configuration.md) and
[home-automation.md](home-automation.md). They are produced by **MCEC dogfooding itself**: a controller
drives a disposable subject, opens its Settings dialog, and `capture`s each tab. No committed producer
script; an agent authors a throwaway driver from this page.

## Recipe

1. **Build + bootstrap.** `dotnet build src/MCEControl.csproj -c Debug`, then stand up an authorized,
   MCP-serving controller with [`scripts/Generate-HeroGif.ps1`](../scripts/Generate-HeroGif.ps1) (it prints
   `HERO_MCP_URL=`). Copy the Debug build to a throwaway subject dir and delete any co-located
   `mcec.settings` so the subject boots to fresh-install defaults (a clean-install look for the shots).
2. **Launch the subject** (`launch { path: <subject exe> }`). The default config has **Server enabled**, so
   Windows Firewall may pop a "Windows Security" prompt that steals the foreground. It is a normal-desktop
   dialog (not secure-desktop UAC), so MCEC can dismiss it: `query { foreground:true }`, and if
   `processName == "PickerHost"` / title matches `Security`, `click { at:{ x, y } }` its **Cancel** button.
3. **Open Settings by keyboard** (a by-name `File` click is ambiguous): `send_command shiftdown:alt` +
   `f` + `shiftup:alt`, then `s`.
4. **Get the dialog handle** from `query { foreground:true }` -> `result.window.handle` (verify
   `title == "Settings"` and `processName == "mcec"` - `window:"Settings"` alone would match the Windows
   Settings app). Use this `handle` for every `capture`/`click` below.
5. **Capture each tab** to the repo's **absolute** `docs/` path:
   - **General** is selected on open - `capture { handle, file }` straight away.
   - **Agent / Client / Server** - `click { handle, at:{ by:name, value:<tab> } }`, dwell, capture.
   - **Serial Server / Activity Monitor** - by-name is unreliable here (`"Serial Server"` substring-hits
     the **Server** tab), and `TabItem` bounds are null, so **coordinate-click** the tab header computed
     from the window rect (see the tabs gotcha in [Examples](examples.md)), then capture.
6. **Crop the 8px black border** PrintWindow adds (left/right/bottom): 491x412 -> 475x404, e.g. with
   `System.Drawing` `DrawImage` from `srcRect(8, 0, W-16, H-8)`.
7. **Re-view every PNG** before committing - a wrong tab looks plausible - then teardown:
   `Generate-HeroGif.ps1 -Stop` plus kill any `mcec.exe` under the subject dir; confirm zero `mcec.exe`.

## Changing the dialog layout (not just recapturing)

If you are also **editing** the dialog (size, control positions), do not reason from the Designer
coordinates alone: WinForms `AutoScaleMode.Font` renders the tab content ~1.16x larger than the design
`6x13` font (runtime Segoe UI 9), so controls overflow a tab sized in design units. Iterate with an
**in-process render harness** instead of driving the desktop each time: an xUnit test in
`tests/MCEControl.xUnit` can `new SettingsDialog(new AppSettings())`, `Show()` it offscreen, select a tab,
and `DrawToBitmap` + measure control bounds - in the app's own runtime, so the scaling matches a real
`capture`. (A PowerShell reflection harness cannot: it hits a `System.IO.Ports` version mismatch loading
`mcec.dll`.) Size the dialog to the **tallest** tab's measured content, then do one MCEC pass for the
committed PNGs.
