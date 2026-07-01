#requires -version 7
<#
.SYNOPSIS
  Regenerates the MCEC hero GIF (docs/hero.gif): one MCEC drives a SECOND MCEC through a guided tour —
  launch -> File > Settings (visit every tab) -> mouse-resize the window ~25% smaller -> drag the title
  bar in small circles -> Help > About -> pause — while the on-screen command overlay (#119) narrates
  every command, and records it all with the agent `record` tool (#80).

.DESCRIPTION
  Dogfoods the agent stack end to end and exercises the atomic `mouse:drag` input path (#123 — one
  command does press, the whole move path, and release) for both a sizing-border resize and a title-bar
  move. The controller is a
  GUI MCEC (not headless `--mcp`, so it renders the overlay) with the localhost MCP HTTP floor on
  (McpServerEnabled), the agent commands enabled (AgentCommandsEnabled), and the overlay on and docked
  Left (CommandOverlayEnabled / CommandOverlayPosition). The *controlled* subject is a SEPARATE COPY of
  the build in its own directory so it reads a co-located config (Program.ConfigPath == the exe's own
  folder); its config sets ActAsServer=false (so it never binds IPAddress.Any:5150 and triggers the
  Windows Firewall prompt that would steal focus), turns its own overlay OFF (only the controller
  narrates), and pins the window so the recorded region is deterministic.

  The driver (this script) connects to the controller over HTTP and uses the agent tools to drive the
  subject by HANDLE (the controller now also has an "MCEC" window, so a title match is ambiguous): it
  `query`s the UIA tree to locate menu items/tabs, clicks/drags with real mouse input, and `capture`s
  the dialogs. As each tool runs, the controller's overlay paints a terse, burnt-orange, alpha-blended
  line over the LEFT of the (wide, left-docked) subject window — so the recorded region is just the
  window (compact, no wallpaper) yet still contains the narration. The two oranges match: the overlay
  item background IS the About box's brand orange.

  This drives the REAL desktop (mouse, keystrokes, launching an app) for ~30s. Run it on an interactive
  session you can leave alone.

.PARAMETER Config
  Build configuration to use (Debug or Release). Default: Debug.
#>
[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Config = 'Debug')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$ctrlDir = Join-Path $repoRoot "src\bin\$Config\net10.0-windows"
$exe = Join-Path $ctrlDir 'mcec.exe'
$outGif = Join-Path $repoRoot 'docs\hero.gif'
$subjectDir = Join-Path $env:TEMP 'mcec-hero-subject'
$url = 'http://127.0.0.1:5151/mcp'

if (-not (Test-Path $exe)) {
  Write-Host "Building ($Config)..."
  dotnet build (Join-Path $repoRoot 'src\MCEControl.csproj') -c $Config | Out-Null
}
if (-not (Test-Path $exe)) { throw "mcec.exe not found at $exe" }

Add-Type -Namespace Native -Name U32 -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
[Native.U32]::SetProcessDPIAware() | Out-Null
$sw = [Native.U32]::GetSystemMetrics(0); $sh = [Native.U32]::GetSystemMetrics(1)

# Pinned subject window geometry: wide and left-docked so the controller's left overlay sits over it and
# the recorded region is just the window (compact, no wallpaper) yet contains the narration. The resize
# shrinks the window toward its top-left corner and the title-bar move keeps it inside the recorded rect.
$winX = 12; $winY = 66; $winW = 1040; $winH = 560
$rx = $winX - 6; $ry = $winY - 8; $rw = $winW + 12; $rh = $winH + 30

# ---- subject: a separate copy of the build with its own co-located config (overlay OFF) ----
if (Test-Path $subjectDir) { Remove-Item $subjectDir -Recurse -Force }
Copy-Item $ctrlDir $subjectDir -Recurse -Force
$subjectExe = Join-Path $subjectDir 'mcec.exe'

Set-Content -Encoding UTF8 -Path (Join-Path $subjectDir 'mcec.settings') -Value @"
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <ActAsServer>false</ActAsServer>
  <ActAsClient>false</ActAsClient>
  <ActAsSerialServer>false</ActAsSerialServer>
  <DisableUpdatePopup>true</DisableUpdatePopup>
  <CommandOverlayEnabled>false</CommandOverlayEnabled>
  <WindowLocation><X>$winX</X><Y>$winY</Y></WindowLocation>
  <WindowSize><Width>$winW</Width><Height>$winH</Height></WindowSize>
</AppSettings>
"@

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

Set-Content -Encoding UTF8 -Path $ctrlCommands -Value @'
<?xml version="1.0" encoding="utf-8"?>
<MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="3.0.0">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <capture Cmd="capture" Enabled="true" />
  <query   Cmd="query"   Enabled="true" />
  <find    Cmd="find"    Enabled="true" />
  <record  Cmd="record"  Enabled="true" />
  <mouse   Cmd="mouse:"  Enabled="true" />
  <sendinput Cmd="key_a" Vk="a" Enabled="true" />
  <sendinput Cmd="key_s" Vk="s" Enabled="true" />
  <sendinput Cmd="key_esc" Vk="VK_ESCAPE" Enabled="true" />
  <sendinput Cmd="enter" Vk="VK_RETURN" Enabled="true" />
</Commands>
</MCEController>
'@

$ctrl = $null; $subject = $null
$script:id = 0
function Rpc([string]$method, $prms) {
  $script:id++
  $body = @{ jsonrpc = '2.0'; id = $script:id; method = $method; params = $prms } | ConvertTo-Json -Depth 8 -Compress
  return Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 30
}
function Tool([string]$name, $toolArgs) { Rpc 'tools/call' @{ name = $name; arguments = $toolArgs } }
function Cmd([string]$command) { Tool 'send_command' @{ command = $command } | Out-Null }
function MoveAbs([int]$cx, [int]$cy) {
  Cmd ("mouse:mt,{0},{1}" -f [int][math]::Round($cx * 65535.0 / ($sw - 1)), [int][math]::Round($cy * 65535.0 / ($sh - 1)))
}
function ClickAbs([int]$cx, [int]$cy) { MoveAbs $cx $cy; Cmd 'mouse:lbc' }
# Drag with the left button held down through a path of absolute screen points, as ONE atomic MCEC
# command (issue #123). `mouse:drag` takes pixels and normalizes across the virtual desktop itself and
# smooths the motion between waypoints, so a single `send_command` replaces the old button-down /
# stream-of-moves / button-up choreography — and, being atomic, it can't interleave with anything else.
# Used for both the sizing-border resize and the title-bar move.
function Drag($points) {
  $coords = ($points | ForEach-Object { '{0},{1}' -f [int]$_[0], [int]$_[1] }) -join ','
  Cmd "mouse:drag,$coords"
}
function Find($node, [scriptblock]$pred) {
  if (& $pred $node) { return $node }
  if ($node.children) { foreach ($c in $node.children) { $r = Find $c $pred; if ($r) { return $r } } }
  return $null
}
# The MCP text block holds the agent envelope { ok, sessionId, result:{ window, tree, ... } }; the UIA
# snapshot is at result.tree. Query the subject by HANDLE (its "MCEC" title is now ambiguous with the
# controller); dialogs (Settings/About) are unambiguous by title.
function TreeOf($resp) {
  foreach ($b in $resp.result.content) { if ($b.type -eq 'text') { try { return ($b.text | ConvertFrom-Json).result.tree } catch { return $null } } }
  return $null
}
function QueryH([long]$handle, [int]$depth = 5) { TreeOf (Tool 'query' @{ handle = $handle; maxDepth = $depth }) }
function QueryW([string]$window, [int]$depth = 5) { TreeOf (Tool 'query' @{ window = $window; maxDepth = $depth }) }

try {
  # Controller first: launch the GUI, wait for its HTTP floor, then minimize every window for a clean
  # backdrop. The overlay is an independent top-most window and survives MinimizeAll, so the controller's
  # main window stays minimized (out of frame) while its overlay keeps narrating.
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

  # Subject: launch the isolated copy and wait for its window handle, then for its UIA menu bar.
  $subject = Start-Process -FilePath $subjectExe -WorkingDirectory $subjectDir -PassThru
  $hsub = 0
  for ($i = 0; $i -lt 30; $i++) { Start-Sleep -Milliseconds 500; $subject.Refresh(); if ($subject.MainWindowHandle -ne 0) { $hsub = $subject.MainWindowHandle.ToInt64(); break } }
  if ($hsub -eq 0) { throw 'subject MCEC window never appeared' }
  $tree = $null
  for ($i = 0; $i -lt 25; $i++) { Start-Sleep -Milliseconds 500; $tree = QueryH $hsub 5; if ($tree) { break } }
  if (-not $tree) { throw 'subject UIA tree never came up' }
  Write-Host 'subject window up'
  Start-Sleep -Milliseconds 700
  $tree = QueryH $hsub 5    # re-query so the menu bar is fully built before we locate it

  $isMenu = { param($n) ($n.controlType -match 'MenuItem') }
  $file = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'File' }
  if (-not $file) { throw 'File menu item not found' }

  # fps 4 + downscale keeps the hero compact (the encoder writes full frames, so frame count x width
  # drive file size; the per-step dwells below are tuned to keep the tour short).
  $rec = Tool 'record' @{ action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 560 }
  Write-Host "record start ok=$(-not $rec.result.isError)"
  Start-Sleep -Milliseconds 400

  # A couple of observation calls so the overlay opens with query/capture lines.
  Tool 'query'   @{ handle = $hsub } | Out-Null;  Start-Sleep -Milliseconds 600
  Tool 'capture' @{ handle = $hsub } | Out-Null;  Start-Sleep -Milliseconds 600

  # --- File > Settings, then tour every tab (click each header in order) ---
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_s'      # 'S'ettings mnemonic on the open File menu

  $stree = $null
  for ($i = 0; $i -lt 20; $i++) { Start-Sleep -Milliseconds 300; $stree = QueryW 'Settings' 8; if ($stree) { break } }
  if (-not $stree) { throw 'Settings dialog never appeared' }
  Write-Host 'settings dialog up'
  foreach ($tn in @('General', 'Client', 'Server', 'Serial Server', 'Activity Monitor')) {
    $tab = Find $stree { param($n) $n.name -eq $tn -and $n.controlType -match 'TabItem' }
    if ($tab) {
      ClickAbs ([int]($tab.x + $tab.width / 2)) ([int]($tab.y + $tab.height / 2))
      Write-Host "  tab: $tn"; Start-Sleep -Milliseconds 550
    }
    else { Write-Host "  (tab '$tn' not found in tree)" }
  }
  Cmd 'key_esc'; Start-Sleep -Milliseconds 500    # close Settings (modal) before touching the main window

  # --- Resize the main window ~25% smaller by dragging the bottom-right sizing border inward. ---
  $newW = [int]($winW * 0.75); $newH = [int]($winH * 0.75)
  $corner0X = $winX + $winW - 2; $corner0Y = $winY + $winH - 2
  $corner1X = $winX + $newW;     $corner1Y = $winY + $newH
  Drag @(
    , @($corner0X, $corner0Y)
    , @([int](($corner0X + $corner1X) / 2), [int](($corner0Y + $corner1Y) / 2))
    , @($corner1X, $corner1Y)
  )
  Start-Sleep -Milliseconds 500

  # --- Move the window by dragging its title bar in small circles (stays inside the recorded region). ---
  $grabX = $winX + [int]($newW / 2); $grabY = $winY + 12
  $offX = $grabX - $winX; $offY = $grabY - $winY
  $ccx = $winX + 120 + $offX; $ccy = $winY + 70 + $offY; $r = 55
  $path = @(, @($grabX, $grabY))
  for ($a = 0; $a -le 720; $a += 50) {
    $rad = [math]::PI * $a / 180.0
    $path += , @([int]($ccx + $r * [math]::Cos($rad)), [int]($ccy + $r * [math]::Sin($rad)))
  }
  Drag $path
  Start-Sleep -Milliseconds 500

  # --- Help > About (re-query by handle: the window moved, so the menu bar is at fresh coordinates). ---
  $tree = QueryH $hsub 5
  $help = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'Help' }
  if (-not $help) { throw 'Help menu item not found after move' }
  ClickAbs ([int]($help.x + $help.width / 2)) ([int]($help.y + $help.height / 2))
  Start-Sleep -Milliseconds 500; Cmd 'key_a'; Start-Sleep -Milliseconds 1000
  Tool 'capture' @{ window = 'About' } | Out-Null

  # --- Pause on the About box, then stop. ---
  Start-Sleep -Milliseconds 1100

  $stop = Tool 'record' @{ action = 'stop'; file = $outGif }
  Write-Host "record stop: $(($stop.result.content | Where-Object type -eq text).text)"
}
finally {
  foreach ($p in @($ctrl, $subject)) { if ($p -and -not $p.HasExited) { try { $p.Kill($true) } catch {} } }
  foreach ($p in Get-Process -Name mcec -ErrorAction SilentlyContinue) { try { $p.Kill($true) } catch {} }
  if ($null -ne $savedSettings) { Set-Content -Path $ctrlSettings -Value $savedSettings -Encoding UTF8 } else { Remove-Item $ctrlSettings -ErrorAction SilentlyContinue }
  if ($null -ne $savedCommands) { Set-Content -Path $ctrlCommands -Value $savedCommands -Encoding UTF8 } else { Remove-Item $ctrlCommands -ErrorAction SilentlyContinue }
  Remove-Item $subjectDir -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Wrote $outGif"
