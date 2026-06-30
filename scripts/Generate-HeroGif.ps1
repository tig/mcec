#requires -version 7
<#
.SYNOPSIS
  Regenerates the MCEC hero GIF (docs/hero.gif): one MCEC drives a SECOND MCEC through launch ->
  Help > About -> File > Settings -> File > Exit, while the on-screen command overlay (#119) narrates
  every command, and records it all with the agent `record` tool (#80).

.DESCRIPTION
  Dogfoods the agent stack end to end. The controller is a GUI MCEC (not headless `--mcp`, so it renders
  the overlay) with the localhost MCP HTTP floor on (McpServerEnabled), the agent commands enabled
  (AgentCommandsEnabled), and the overlay on and docked Left (CommandOverlayEnabled /
  CommandOverlayPosition). The *controlled* subject is a SEPARATE COPY of the build in its own directory
  so it reads a co-located config (Program.ConfigPath == the exe's own folder); its config sets
  ActAsServer=false (so it never binds IPAddress.Any:5150 and triggers the Windows Firewall prompt that
  would steal focus), turns its own overlay OFF (only the controller narrates), and pins the window so
  the recorded region is deterministic.

  The driver (this script) connects to the controller over HTTP and uses the agent tools to drive the
  subject: it `query`s the subject's UIA tree to locate the Help/File menu items, clicks them with real
  mouse moves, sends the menu hot-keys, and `capture`s the dialogs. As each tool runs, the controller's
  overlay paints a terse, burnt-orange, alpha-blended line over the LEFT of the (wide, left-docked)
  subject window — so the recorded region is just the window (compact, no wallpaper) yet still contains
  the narration. The two oranges match: the overlay item background IS the About box's brand orange.

  This drives the REAL desktop (mouse, keystrokes, launching an app) for ~20s. Run it on an interactive
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
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int n);
'@
[Native.U32]::SetProcessDPIAware() | Out-Null
$sw = [Native.U32]::GetSystemMetrics(0); $sh = [Native.U32]::GetSystemMetrics(1)

# Pinned subject window geometry. The window is wide and left-docked; the controller's overlay is docked
# LEFT over it, and the recorded region is just the window -> compact, no wallpaper, narration included.
# The About/Settings dialogs are CenterParent, so they sit to the right of the left-hugging overlay.
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

# ---- controller: GUI MCEC with the agent surface + overlay, driven over HTTP ----
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
  <sendinput Cmd="key_x" Vk="x" Enabled="true" />
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
function ClickAbs([int]$cx, [int]$cy) {
  Cmd ("mouse:mt,{0},{1}" -f [int][math]::Round($cx * 65535.0 / ($sw - 1)), [int][math]::Round($cy * 65535.0 / ($sh - 1)))
  Cmd 'mouse:lbc'
}
function Find($node, [scriptblock]$pred) {
  if (& $pred $node) { return $node }
  if ($node.children) { foreach ($c in $node.children) { $r = Find $c $pred; if ($r) { return $r } } }
  return $null
}
# Query the subject by HANDLE (the controller now also has an "MCEC" window, so a title match is
# ambiguous) and return its UIA tree, or null.
function QueryTree([long]$handle, [int]$depth = 5) {
  foreach ($b in (Tool 'query' @{ handle = $handle; maxDepth = $depth }).result.content) {
    if ($b.type -eq 'text') { try { return ($b.text | ConvertFrom-Json).result.tree } catch { return $null } }
  }
  return $null
}

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
  for ($i = 0; $i -lt 25; $i++) { Start-Sleep -Milliseconds 500; $tree = QueryTree $hsub 5; if ($tree) { break } }
  if (-not $tree) { throw 'subject UIA tree never came up' }
  Write-Host 'subject window up'
  Start-Sleep -Milliseconds 1000
  $tree = QueryTree $hsub 5   # re-query so the menu bar is fully built before we locate it

  $isMenu = { param($n) ($n.controlType -match 'MenuItem') }
  $help = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'Help' }
  $file = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'File' }
  if (-not $help -or -not $file) { throw 'File/Help menu items not found' }

  # fps 4 + downscale to 680 keeps the hero compact (the encoder writes full frames, so frame count x
  # width drive file size).
  $rec = Tool 'record' @{ action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 680 }
  Write-Host "record start ok=$(-not $rec.result.isError)"
  Start-Sleep -Milliseconds 400

  # Observe (a couple of tool calls so the overlay shows query/capture lines), then drive the menus.
  Tool 'query'   @{ handle = $hsub } | Out-Null;  Start-Sleep -Milliseconds 700
  Tool 'capture' @{ handle = $hsub } | Out-Null;  Start-Sleep -Milliseconds 700

  # Help > About (the burnt-orange About box matches the overlay)
  ClickAbs ([int]($help.x + $help.width / 2)) ([int]($help.y + $help.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_a'; Start-Sleep -Milliseconds 1300
  Tool 'capture' @{ window = 'About' } | Out-Null; Start-Sleep -Milliseconds 1500
  Cmd 'key_esc'; Start-Sleep -Milliseconds 800

  # File > Settings
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_s'; Start-Sleep -Milliseconds 1700
  Cmd 'key_esc'; Start-Sleep -Milliseconds 800

  # File > Exit
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_x'; Start-Sleep -Milliseconds 1200
  if (-not $subject.HasExited) { Cmd 'enter'; Start-Sleep -Milliseconds 900 }

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
