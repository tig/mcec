#requires -version 7
<#
.SYNOPSIS
  Regenerates the MCEC hero GIF (docs/hero.gif): one MCEC instance drives another through
  launch -> File > Settings (tour every tab) -> mouse-resize the window ~25% smaller ->
  drag the title bar in small circles -> Help > About -> pause, recording it with the agent
  `record` tool.

.DESCRIPTION
  Dogfoods the #80 `record` feature and exercises the mouse-drag input path (button-down,
  a stream of absolute moves, button-up) for both a sizing-border resize and a title-bar move.
  A headless controller (`mcec.exe --mcp`) runs from the repo build dir; the *controlled*
  subject is a SEPARATE COPY of the build in its own directory so it reads a co-located config
  (Program.ConfigPath == the exe's own folder). The subject's config sets ActAsServer=false (so
  it never binds IPAddress.Any:5150 and never triggers the Windows Firewall prompt that would
  steal focus) and pins the window location/size so the recorded region is deterministic. All
  desktop windows are minimized first for a clean backdrop.

  This drives the REAL desktop (mouse, keystrokes, launching an app) for ~30s. Run it on an
  interactive session you can leave alone.

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

# Pinned subject window geometry. The recorded region is exactly the window, so the hero is just the
# app (the About/Settings dialogs center within it) on no wallpaper -> clean and compact.
$winX = 130; $winY = 80; $winW = 900; $winH = 560
$rx = $winX; $ry = $winY; $rw = $winW; $rh = $winH

# ---- subject: a separate copy of the build with its own co-located config ----
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
  <WindowLocation><X>$winX</X><Y>$winY</Y></WindowLocation>
  <WindowSize><Width>$winW</Width><Height>$winH</Height></WindowSize>
</AppSettings>
"@

# ---- controller: agent commands + the actions used to drive the subject ----
$ctrlSettings = Join-Path $ctrlDir 'mcec.settings'
$ctrlCommands = Join-Path $ctrlDir 'mcec.commands'
$savedSettings = if (Test-Path $ctrlSettings) { Get-Content -Raw $ctrlSettings } else { $null }
$savedCommands = if (Test-Path $ctrlCommands) { Get-Content -Raw $ctrlCommands } else { $null }

Set-Content -Encoding UTF8 -Path $ctrlSettings -Value @'
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>false</McpServerEnabled>
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

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $exe; $psi.Arguments = '--mcp'; $psi.WorkingDirectory = $ctrlDir
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
$psi.UseShellExecute = $false; $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
$driver = [System.Diagnostics.Process]::Start($psi)
$driver.BeginErrorReadLine()

$script:id = 0
function Rpc([string]$method, $prms) {
  $script:id++
  $req = @{ jsonrpc = '2.0'; id = $script:id; method = $method; params = $prms } | ConvertTo-Json -Depth 8 -Compress
  $driver.StandardInput.WriteLine($req); $driver.StandardInput.Flush()
  $line = $driver.StandardOutput.ReadLine()
  if ($null -eq $line) { throw "no response for $method" }
  return $line | ConvertFrom-Json
}
function Tool([string]$name, $toolArgs) { Rpc 'tools/call' @{ name = $name; arguments = $toolArgs } }
function Cmd([string]$command) { Tool 'send_command' @{ command = $command } | Out-Null }
function MoveAbs([int]$cx, [int]$cy) {
  Cmd ("mouse:mt,{0},{1}" -f [int][math]::Round($cx * 65535.0 / ($sw - 1)), [int][math]::Round($cy * 65535.0 / ($sh - 1)))
}
function ClickAbs([int]$cx, [int]$cy) { MoveAbs $cx $cy; Cmd 'mouse:lbc' }
# Drag with the left button held down through a path of absolute screen points: button-down on
# the first point, a move to each subsequent point (with a dwell so the 4fps recorder catches it),
# button-up at the end. Used for both the sizing-border resize and the title-bar move.
function Drag($points) {
  MoveAbs $points[0][0] $points[0][1]; Start-Sleep -Milliseconds 150
  Cmd 'mouse:lbd'; Start-Sleep -Milliseconds 200
  foreach ($p in $points[1..($points.Count - 1)]) { MoveAbs $p[0] $p[1]; Start-Sleep -Milliseconds 90 }
  Start-Sleep -Milliseconds 150; Cmd 'mouse:lbu'
}
function Find($node, [scriptblock]$pred) {
  if (& $pred $node) { return $node }
  if ($node.children) { foreach ($c in $node.children) { $r = Find $c $pred; if ($r) { return $r } } }
  return $null
}
function QueryTree([string]$window, [int]$depth = 5) {
  # The MCP text block holds the agent envelope { ok, sessionId, result:{ window, tree, ... } }
  # (AgentToolResult.ToJsonObject). The UIA snapshot is at result.tree.
  foreach ($b in (Tool 'query' @{ window = $window; maxDepth = $depth }).result.content) {
    if ($b.type -eq 'text') { try { return ($b.text | ConvertFrom-Json).result.tree } catch { return $null } }
  }
  return $null
}

try {
  $init = Rpc 'initialize' @{}
  Write-Host "controller: $($init.result.serverInfo.name) $($init.result.serverInfo.version)"

  # Clean backdrop: minimize every window (controller console + this terminal included).
  (New-Object -ComObject Shell.Application).MinimizeAll()
  Start-Sleep -Milliseconds 800

  # fps 4 + downscale to 440 keeps the hero compact (the encoder writes full frames, so frame
  # count x width drive file size; the per-step dwells below are tuned to keep the tour short).
  $rec = Tool 'record' @{ action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 440 }
  Write-Host "record start: $($rec.result.content[0].text)"
  Start-Sleep -Milliseconds 500

  $subject = Start-Process -FilePath $subjectExe -WorkingDirectory $subjectDir -PassThru
  Write-Host "subject pid $($subject.Id)"

  $tree = $null
  for ($i = 0; $i -lt 25; $i++) { Start-Sleep -Milliseconds 600; $tree = QueryTree 'MCEC' 4; if ($tree) { break } }
  if (-not $tree) { throw 'subject MCEC window never appeared' }
  Write-Host 'subject window up'
  Start-Sleep -Milliseconds 600    # dwell on the freshly-started window
  $tree = QueryTree 'MCEC' 5        # re-query so the menu bar is fully built before we locate it

  $isMenu = { param($n) ($n.controlType -match 'MenuItem') }
  $file = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'File' }
  if (-not $file) { throw 'File menu item not found' }

  # --- File > Settings, then tour every tab (click each header in order) ---
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_s'      # 'S'ettings mnemonic on the open File menu

  $stree = $null
  for ($i = 0; $i -lt 20; $i++) { Start-Sleep -Milliseconds 300; $stree = QueryTree 'Settings' 8; if ($stree) { break } }
  if (-not $stree) { throw 'Settings dialog never appeared' }
  Write-Host 'settings dialog up'
  foreach ($tn in @('General', 'Client', 'Server', 'Serial Server', 'Activity Monitor')) {
    $tab = Find $stree { param($n) $n.name -eq $tn -and $n.controlType -match 'TabItem' }
    if ($tab) {
      ClickAbs ([int]($tab.x + $tab.width / 2)) ([int]($tab.y + $tab.height / 2))
      Write-Host "  tab: $tn"; Start-Sleep -Milliseconds 600
    }
    else { Write-Host "  (tab '$tn' not found in tree)" }
  }
  Cmd 'key_esc'; Start-Sleep -Milliseconds 500    # close Settings (modal) before touching the main window

  # --- Resize the main window ~25% smaller by dragging the bottom-right sizing border inward. ---
  # The window is pinned at ($winX,$winY,$winW,$winH); the recorded region is that same rect, so the
  # window shrinks toward its top-left corner and stays fully in frame.
  $newW = [int]($winW * 0.75); $newH = [int]($winH * 0.75)
  $corner0X = $winX + $winW - 2; $corner0Y = $winY + $winH - 2
  $corner1X = $winX + $newW;     $corner1Y = $winY + $newH
  Drag @(
    , @($corner0X, $corner0Y)
    , @([int](($corner0X + $corner1X) / 2), [int](($corner0Y + $corner1Y) / 2))
    , @($corner1X, $corner1Y)
  )
  Start-Sleep -Milliseconds 500

  # --- Move the window by dragging its title bar in small circles. ---
  # Grab the title bar (offset from the now-resized window's top-left), then walk the cursor around two
  # small circles. The window follows by (cursor - grab-offset); the circle is sized/centered so the
  # whole window stays inside the recorded region.
  $grabX = $winX + [int]($newW / 2); $grabY = $winY + 12     # middle of the title bar
  $offX = $grabX - $winX; $offY = $grabY - $winY             # cursor-to-window-top-left offset
  $ccx = $winX + 90 + $offX; $ccy = $winY + 70 + $offY; $r = 55   # circle centre, in cursor space
  $path = @(, @($grabX, $grabY))                            # button-down on the title bar
  for ($a = 0; $a -le 720; $a += 50) {
    $rad = [math]::PI * $a / 180.0
    $path += , @([int]($ccx + $r * [math]::Cos($rad)), [int]($ccy + $r * [math]::Sin($rad)))
  }
  Drag $path
  Start-Sleep -Milliseconds 500

  # --- Help > About (re-query: the window moved, so the menu bar is at fresh coordinates). ---
  $tree = QueryTree 'MCEC' 5
  $help = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'Help' }
  if (-not $help) { throw 'Help menu item not found after move' }
  ClickAbs ([int]($help.x + $help.width / 2)) ([int]($help.y + $help.height / 2))
  Start-Sleep -Milliseconds 500; Cmd 'key_a'; Start-Sleep -Milliseconds 1000

  # --- Pause on the About box, then stop. ---
  Start-Sleep -Milliseconds 1000

  $stop = Tool 'record' @{ action = 'stop'; file = $outGif }
  Write-Host "record stop: $($stop.result.content[0].text)"
}
finally {
  try { $driver.StandardInput.Close() } catch {}
  try { if (-not $driver.WaitForExit(3000)) { $driver.Kill($true) } } catch {}
  $driver.Dispose()
  foreach ($p in Get-Process -Name mcec -ErrorAction SilentlyContinue) { try { $p.Kill($true) } catch {} }
  if ($null -ne $savedSettings) { Set-Content -Path $ctrlSettings -Value $savedSettings -Encoding UTF8 } else { Remove-Item $ctrlSettings -ErrorAction SilentlyContinue }
  if ($null -ne $savedCommands) { Set-Content -Path $ctrlCommands -Value $savedCommands -Encoding UTF8 } else { Remove-Item $ctrlCommands -ErrorAction SilentlyContinue }
  Remove-Item $subjectDir -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Wrote $outGif"
