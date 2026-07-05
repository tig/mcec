# WinPrint Windows hero GIF

> **Flavor:** Scripted recipe. One of the worked [Examples](examples.md); the shared bootstrap, MCP
> envelope, and targeting gotchas live there and aren't repeated here.

`docs/hero-gui-win.gif` in [tig/winprint](https://github.com/tig/winprint) is produced by **MCEC driving a real
Windows desktop** — Start Menu launch, settings/zoom tour, Print to Microsoft Print to PDF, save
`winprintdemo.pdf`, open the PDF — recorded as a **desktop region** GIF with the MCEC command overlay
narrating each step.

The producer script is intended to live in the **winprint** repo and be run from that repo root
(paths are repo-relative). Until that PR lands, a copy lives at `scripts/Generate-WinPrint-HeroGif.ps1`
here and accepts `-WinPrintRoot` (default: current directory).

## Prerequisites (operator)

| Requirement | Notes |
|-------------|--------|
| **Unlocked interactive session** | Real injected mouse/keyboard. |
| **MCEC installed** | The harness **checks** for an existing install (default dir, PATH, winget registration) and **does not** download or install MCEC. If missing: `winget install Kindel.mcec` when published, or run the signed setup.exe until then. **Do not build from source** for hero runs. |
| **WinPrint installed** | Start Menu search must find **WinPrint** (Velopack/winget install — operator responsibility). |
| **Demo PDF path** | `%USERPROFILE%\Documents\winprintdemo.pdf` |

### Harness-only prep (not MCP choreography)

- **Delete prior `winprintdemo.pdf`** — the harness runs `Remove-Item` before `record start`. Disposable
  `provision-session` will fold this artifact cleanup into session provisioning.
- **Disposable MCEC session** — the harness copies the installed MCEC into
  `%LOCALAPPDATA%\MCEC\sessions\winprint-hero`, writes agent config **only there**, and deletes the
  dir afterward, so the core install's `mcec.settings` / `mcec.commands` are never touched. (The
  `provision-session` tool now does this properly; the mcec hero uses it and this harness should move
  to it too.)

## One-shot regeneration

From the **winprint repo root**:

```powershell
# Until the script moves to winprint:
pwsh -NoProfile -File C:\path\to\mcec\scripts\Generate-WinPrint-HeroGif.ps1

# After the winprint PR (same repo root as cwd):
pwsh -NoProfile -File scripts/Generate-WinPrint-HeroGif.ps1
```

**Pre-release dev builds:** MCEC is not yet published to winget, so until it is installed on the box the
harness has nothing to discover. For local dogfood, build MCEC and point `-McecInstallDir` at the build
output (it needs the `clipboard` tool, which is on `develop`):

```powershell
dotnet build src\MCEControl.csproj -c Debug
pwsh -NoProfile -File scripts\Generate-WinPrint-HeroGif.ps1 `
  -McecInstallDir C:\path\to\mcec\src\bin\Debug\net10.0-windows
```

**Tuning size:** the tour runs ~40 s, so file size tracks frame count (`~40 x Fps`) times frame area.
Defaults (`-Fps 2 -MaxWidth 560`) produce ~4 MB; raise them for a smoother/larger GIF, lower to shrink.

Evidence bundles land under `artifacts/customer1/` in the mcec repo (see [evidence-bundles.md](evidence-bundles.md)).

## Troubleshooting

| Symptom | Recovery |
|---------|----------|
| `WinPrint did not appear` after Start search | Kill stray `winprint`/`mcec` processes, pause a few seconds, rerun. The harness already sends Win+D before Win+S; a focused IDE can still steal the first attempt. |
| `Unknown tool: clipboard` | MCEC predates the `clipboard` tool; use a current `develop` build (pass `-McecInstallDir` to it). |
| `Remove-Item` fails on `winprintdemo.pdf` | A PDF viewer still has the file open; the harness sends Alt+F4 after record stop, but kill the viewer manually if a prior run aborted early. |

## Manual MCP recipe (agent-playbook)

Connect to the **disposable session** MCEC HTTP floor (`POST http://127.0.0.1:5151/mcp`) after the
operator/harness has provisioned it (agent commands enabled in the session copy only).

| Step | Tool call |
|------|-----------|
| Start record | `record { action:"start", x, y, width, height, fps:4, maxWidth:880 }` (desktop region) |
| Launch | `send_command desktop` (Win+D) → `send_command winsearch` (Win+S) → `chars:WinPrint` → Enter → `query { process:"winprint" }` |
| Open sample | `query` tree → `click` File… button by bounds → `clipboard { action:"set", text:"…SheetViewModel.cs" }` → Ctrl+V → Enter |
| Settings | `click` **Line Numbers** (twice), **Landscape** (twice) |
| Zoom | `click` preview → `key_equals` ×4 → arrows → `key_0` |
| Second file | File → clipboard `README.md` → Ctrl+V → Enter |
| Print | `query` tree → `click` toolbar **Print…** button by bounds (printer already **Microsoft Print to PDF**) |
| Save PDF | `query` **Save Print Output As** by `handle` → `click` filename field → `chars:C:\\…\\winprintdemo.pdf` (backslashes doubled) → Enter |
| Open PDF | `send_command winr` → `send_command chars:<pdf path>` (backslashes doubled) → Enter |
| Stop | `record { action:"stop", file:"docs/hero-gui-win.gif" }` |
| Close PDF | `send_command alt_f4` — release the file lock so the next run's harness `Remove-Item` succeeds |

**Not in the agent recipe:** deleting the old PDF (harness `Remove-Item` before connect), installing
MCEC/WinPrint, copying the session dir, evidence zip — the harness/operator handles these around the
MCP choreography.

Agent connect-time guidance: `AgentServer.Instructions` in `src/Services/AgentServer.cs` and
[Agents.md](../Agents.md).