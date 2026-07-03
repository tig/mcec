#requires -version 7
<#
.SYNOPSIS
  Regenerates the MCEC hero GIF (docs/hero.gif): one MCEC drives a SECOND MCEC through a guided tour
  (launch -> File > Settings, visit every tab -> resize the window ~25% smaller -> move it in a circle
  -> Help > About) while the on-screen command overlay (#119) narrates every step, recorded with the
  agent `record` tool (#80).

.DESCRIPTION
  The demo is driven through MCEC's own high-level agent tools, not hand-rolled coordinate math or
  UIA-tree walking: `launch` starts the subject and returns its window handle; `click` targets menu
  bar items and Settings tabs BY NAME (`at = { by = 'name', value = ... }`, so no pixel coordinates and
  robust to the window moving); a keyboard mnemonic (`send_command key_*`) picks the dropdown item an
  open WinForms menu exposes only to the keyboard (Settings, About); `drag` performs the two genuine
  pixel gestures (resize by the sizing border, move along a circular path of waypoints); `capture`
  grabs the dialogs; `record` captures the region. The script is essentially the intent list; MCEC does
  the work. (This is the pattern a connected agent uses directly; the script exists only to reproduce
  the marketing asset on demand.)

  The controller is a GUI MCEC (renders the overlay) with the localhost MCP HTTP floor on
  (McpServerEnabled), agent commands enabled (AgentCommandsEnabled), and the overlay on and docked Left
  (CommandOverlayEnabled / CommandOverlayPosition). The controlled subject is a SEPARATE COPY of the
  build in its own directory so it reads a co-located config (Program.ConfigPath == the exe's own
  folder); its config sets ActAsServer=false (so it never binds IPAddress.Any:5150 and triggers the
  Windows Firewall prompt that would steal focus) and turns its own overlay OFF (only the controller
  narrates). The subject window is NOT pinned; it opens at MCEC's default size and location, and the
  script observes where it landed (one `query`) to derive the recorded region and every drag point. The
  subject is driven BY HANDLE (the controller also owns an "MCEC" window, so a title match is
  ambiguous); the modal dialogs (Settings/About) are unambiguous by title.

  This drives the REAL desktop (mouse, launching an app) for ~30s. Run it on an interactive session you
  can leave alone.

.PARAMETER Config
  Build configuration to use (Debug or Release). Default: Debug.

.PARAMETER AllowNonDevelopBuild
  Skip the develop-branch/develop-stamp guard. The GitVersion-stamped version string is baked into the
  binary and appears IN the hero (the subject's log window, status bar, and About box), so by default
  the script refuses to record from anything but a develop-stamped build. Pass this only when a
  feature-branch hero is deliberate.
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')][string]$Config = 'Debug',
  [switch]$AllowNonDevelopBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$ctrlDir = Join-Path $repoRoot "src\bin\$Config\net10.0-windows"
$exe = Join-Path $ctrlDir 'mcec.exe'
$outGif = Join-Path $repoRoot 'docs\hero.gif'
$subjectDir = Join-Path $env:TEMP 'mcec-hero-subject'
$url = 'http://127.0.0.1:5151/mcp'

# GUARD: the recording must come from a develop-stamped build. GitVersion bakes the branch name into
# the version string, which shows in the hero's log window, status bar, and About box; a
# timestamp-fresh binary from whatever branch was built last passes a naive "build if needed" check.
$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'develop' -and -not $AllowNonDevelopBuild) {
  throw "Refusing to record the hero from branch '$branch' (the GitVersion stamp lands in the GIF). " +
        'Switch to develop (git checkout develop && git pull), or pass -AllowNonDevelopBuild if a ' +
        'branch build is deliberate.'
}

# Always build: incremental is a fast no-op when nothing changed, and GitVersion re-stamps the binary
# whenever HEAD moved; the stamp always reflects the checkout being recorded, never a stale build.
Write-Host "Building ($Config)..."
dotnet build (Join-Path $repoRoot 'src\MCEControl.csproj') -c $Config | Out-Null
if (-not (Test-Path $exe)) { throw "mcec.exe not found at $exe" }

$stamp = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $AllowNonDevelopBuild -and $stamp -notlike '*Branch.develop.*') {
  throw "mcec.exe is stamped '$stamp' after rebuilding; expected a Branch.develop stamp. " +
        'Is the working tree in a detached/unexpected state?'
}
Write-Host "Recording from: $stamp"

# Physical screen size in the agent's coordinate space (absolute device pixels). The subject window is
# NOT pinned; it opens at MCEC's default size/location and we observe where it lands (below), so the
# recorded region and the drag points follow the real window rather than hard-coded numbers.
Add-Type -Namespace Native -Name U32 -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
[Native.U32]::SetProcessDPIAware() | Out-Null
$sw = [Native.U32]::GetSystemMetrics(0); $sh = [Native.U32]::GetSystemMetrics(1)

# ---- subject: a separate copy of the build with its own co-located config (overlay OFF) ----
if (Test-Path $subjectDir) { Remove-Item $subjectDir -Recurse -Force }
Copy-Item $ctrlDir $subjectDir -Recurse -Force
$subjectExe = Join-Path $subjectDir 'mcec.exe'

Set-Content -Encoding UTF8 -Path (Join-Path $subjectDir 'mcec.settings') -Value @'
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <ActAsServer>false</ActAsServer>
  <ActAsClient>false</ActAsClient>
  <ActAsSerialServer>false</ActAsSerialServer>
  <DisableUpdatePopup>true</DisableUpdatePopup>
  <CommandOverlayEnabled>false</CommandOverlayEnabled>
</AppSettings>
'@

# ---- controller: GUI MCEC with the agent surface + overlay (docked Left), driven over HTTP ----
$ctrlSettings = Join-Path $ctrlDir 'mcec.settings'
$ctrlCommands = Join-Path $ctrlDir 'mcec.commands'
$savedSettings = if (Test-Path $ctrlSettings) { Get-Content -Raw $ctrlSettings } else { $null }
$savedCommands = if (Test-Path $ctrlCommands) { Get-Content -Raw $ctrlCommands } else { $null }

Set-Content -Encoding UTF8 -Path $ctrlSettings -Value @'
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>true</McpServerEnabled>
  <CommandOverlayEnabled>true</CommandOverlayEnabled>
  <CommandOverlayPosition>Left</CommandOverlayPosition>
  <HideOnStartup>true</HideOnStartup>
  <ActAsServer>false</ActAsServer>
</AppSettings>
'@

# Only the tools this tour uses need to be enabled: the observation/actuation tools plus the three
# menu-mnemonic keys (an open WinForms dropdown exposes its items to the keyboard, not to name-based
# UIA targeting).
Set-Content -Encoding UTF8 -Path $ctrlCommands -Value @'
<?xml version="1.0" encoding="utf-8"?>
<MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="3.0.0">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <capture Cmd="capture" Enabled="true" />
  <query   Cmd="query"   Enabled="true" />
  <record  Cmd="record"  Enabled="true" />
  <drag    Cmd="drag"    Enabled="true" />
  <launch  Cmd="launch"  Enabled="true" />
  <click   Cmd="click"   Enabled="true" />
  <sendinput Cmd="key_s"   Vk="s" Enabled="true" />
  <sendinput Cmd="key_a"   Vk="a" Enabled="true" />
  <sendinput Cmd="key_esc" Vk="VK_ESCAPE" Enabled="true" />
</Commands>
</MCEController>
'@

$ctrl = $null
$script:id = 0
function Rpc([string]$method, $prms) {
  $script:id++
  $body = @{ jsonrpc = '2.0'; id = $script:id; method = $method; params = $prms } | ConvertTo-Json -Depth 8 -Compress
  return Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 30
}
# One call per agent tool: name + arguments. Returns the raw JSON-RPC response.
function Tool([string]$name, $toolArgs) { Rpc 'tools/call' @{ name = $name; arguments = $toolArgs } }
# Click a named element (menu bar item, tab) at its centre; $target is the window selector
# (@{ handle = ... } or @{ window = ... }). No pixel coordinates, no UIA-tree walking.
function ClickName([hashtable]$target, [string]$name) {
  $clickArgs = @{ at = @{ by = 'name'; value = $name } }
  foreach ($k in $target.Keys) { $clickArgs[$k] = $target[$k] }
  Tool 'click' $clickArgs | Out-Null
}
# Press a menu mnemonic on the open dropdown (the one thing name-based targeting can't reach).
function Key([string]$cmd) { Tool 'send_command' @{ command = $cmd } | Out-Null }
# The agent envelope { ok, sessionId, result } is JSON inside the MCP text content block.
function ResultOf($resp) {
  foreach ($b in $resp.result.content) {
    if ($b.type -eq 'text') { try { return ($b.text | ConvertFrom-Json).result } catch { return $null } }
  }
  return $null
}

try {
  # Controller first: launch the GUI, wait for its HTTP floor, then clear the desktop. The overlay is an
  # independent top-most window and survives MinimizeAll, so the controller's own window stays hidden
  # (HideOnStartup) while its overlay keeps narrating.
  $ctrl = Start-Process -FilePath $exe -WorkingDirectory $ctrlDir -PassThru
  $up = $false
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 600
    try { if ((Rpc 'initialize' @{}).result.serverInfo.name -eq 'MCEC') { $up = $true; break } } catch {}
  }
  if (-not $up) { throw 'controller HTTP did not come up' }
  Write-Host 'controller up'
  (New-Object -ComObject Shell.Application).MinimizeAll()
  Start-Sleep -Milliseconds 800

  # Subject: launch the isolated copy with the gated `launch` tool; it returns the window handle we then
  # drive by. (Dogfoods the robust launch primitive rather than a raw Start-Process / Win+R dance.)
  $lr = ResultOf (Tool 'launch' @{ path = $subjectExe; timeout = 8000 })
  $hsub = if ($lr) { [long]($lr.handle) } else { 0 }
  if (-not $hsub) { throw 'launch did not return a subject window handle' }
  Write-Host "subject up (handle=0x$('{0:X}' -f $hsub))"
  Start-Sleep -Milliseconds 1500   # let the window settle and the menu bar build

  # Observe where the window actually landed (nothing is pinned; it opens at MCEC's default size and
  # location). The record region and every drag point below are derived from these real bounds.
  $w = (ResultOf (Tool 'query' @{ handle = $hsub; maxDepth = 1 })).window
  if (-not $w) { throw 'could not read the subject window bounds' }
  $wx = [int]$w.x; $wy = [int]$w.y; $ww = [int]$w.width; $wh = [int]$w.height
  Write-Host "subject bounds: $wx,$wy ${ww}x${wh}"

  # Record the left band of the screen: full height (so the top-most overlay's narration column is in
  # frame wherever the window sits) and out to the window's right edge. fps 4 + downscale keep the hero
  # compact (frame count x width drive file size).
  $rx = 0; $ry = 0; $rw = [Math]::Min($sw, $wx + $ww + 12); $rh = $sh
  Tool 'record' @{ action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 560 } | Out-Null
  Write-Host 'record start'
  Tool 'capture' @{ handle = $hsub } | Out-Null; Start-Sleep -Milliseconds 600

  # --- File > Settings, then tour every tab (click BY NAME; no coordinates, no tree walking) ---
  ClickName @{ handle = $hsub } 'File'; Start-Sleep -Milliseconds 700   # click opens AND focuses the menu
  Key 'key_s'; Start-Sleep -Milliseconds 1200                          # 'S'ettings mnemonic on the open File menu
  foreach ($tab in @('General', 'Client', 'Server', 'Serial Server', 'Activity Monitor')) {
    ClickName @{ window = 'Settings' } $tab                            # tabs are dialog controls; name-resolvable
    Write-Host "  tab: $tab"; Start-Sleep -Milliseconds 600
  }
  Key 'key_esc'; Start-Sleep -Milliseconds 600                         # close the modal Settings dialog

  # --- Resize ~25% smaller by dragging the bottom-right sizing border inward (a real pixel gesture). ---
  $newW = [int]($ww * 0.75); $newH = [int]($wh * 0.75)
  Tool 'drag' @{
    handle = $hsub
    from   = @{ x = $wx + $ww - 2; y = $wy + $wh - 2 }
    to     = @{ x = $wx + $newW;   y = $wy + $newH }
  } | Out-Null
  Start-Sleep -Milliseconds 500

  # --- Move the window by dragging its title bar in a small circle (drag tool with path waypoints). ---
  $grabX = $wx + [int]($newW / 2); $grabY = $wy + 12
  $ccx = $grabX; $ccy = $grabY + 55; $r = 55   # circle just below the grab point so the window wiggles in place
  $circle = @()
  for ($a = 0; $a -le 720; $a += 50) {
    $rad = [math]::PI * $a / 180.0
    $circle += @{ x = [int]($ccx + $r * [math]::Cos($rad)); y = [int]($ccy + $r * [math]::Sin($rad)) }
  }
  Tool 'drag' @{
    handle = $hsub
    from   = @{ x = $grabX; y = $grabY }
    path   = $circle[0..($circle.Count - 2)]
    to     = $circle[-1]
  } | Out-Null
  Start-Sleep -Milliseconds 500

  # --- Help > About (click resolves the menu bar item by name, so the window having moved is fine). ---
  ClickName @{ handle = $hsub } 'Help'; Start-Sleep -Milliseconds 700
  Key 'key_a'; Start-Sleep -Milliseconds 1000                          # 'A'bout mnemonic on the open Help menu
  Tool 'capture' @{ window = 'About' } | Out-Null
  Start-Sleep -Milliseconds 1100     # pause on the About box

  $stop = Tool 'record' @{ action = 'stop'; file = $outGif }
  Write-Host "record stop: $(($stop.result.content | Where-Object type -eq text).text)"
}
finally {
  if ($ctrl -and -not $ctrl.HasExited) { try { $ctrl.Kill($true) } catch {} }
  foreach ($p in Get-Process -Name mcec -ErrorAction SilentlyContinue) { try { $p.Kill($true) } catch {} }
  if ($null -ne $savedSettings) { Set-Content -Path $ctrlSettings -Value $savedSettings -Encoding UTF8 } else { Remove-Item $ctrlSettings -ErrorAction SilentlyContinue }
  if ($null -ne $savedCommands) { Set-Content -Path $ctrlCommands -Value $savedCommands -Encoding UTF8 } else { Remove-Item $ctrlCommands -ErrorAction SilentlyContinue }
  Remove-Item $subjectDir -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Wrote $outGif"
