#requires -version 7
<#
.SYNOPSIS
  Regenerates the MCEC hero GIF (docs/hero.gif): one MCEC instance drives another through
  launch -> Help > About -> File > Settings -> File > Exit, recording it with the agent `record` tool.

.DESCRIPTION
  Dogfoods the #80 `record` feature. A headless controller (`mcec.exe --mcp`) runs from the repo
  build dir; the *controlled* subject is a SEPARATE COPY of the build in its own directory so it reads
  a co-located config (Program.ConfigPath == the exe's own folder). The subject's config sets
  ActAsServer=false (so it never binds IPAddress.Any:5150 and never triggers the Windows Firewall
  prompt that would steal focus) and pins the window location/size so the recorded region is
  deterministic. All desktop windows are minimized first for a clean backdrop.

  This drives the REAL desktop (mouse, keystrokes, launching an app) for ~20s. Run it on an
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
function ClickAbs([int]$cx, [int]$cy) {
  Cmd ("mouse:mt,{0},{1}" -f [int][math]::Round($cx * 65535.0 / ($sw - 1)), [int][math]::Round($cy * 65535.0 / ($sh - 1)))
  Cmd 'mouse:lbc'
}
function Find($node, [scriptblock]$pred) {
  if (& $pred $node) { return $node }
  if ($node.children) { foreach ($c in $node.children) { $r = Find $c $pred; if ($r) { return $r } } }
  return $null
}
function QueryTree([string]$window, [int]$depth = 5) {
  foreach ($b in (Tool 'query' @{ window = $window; maxDepth = $depth }).result.content) {
    if ($b.type -eq 'text') { try { return ($b.text | ConvertFrom-Json).data.tree } catch { return $null } }
  }
  return $null
}

try {
  $init = Rpc 'initialize' @{}
  Write-Host "controller: $($init.result.serverInfo.name) $($init.result.serverInfo.version)"

  # Clean backdrop: minimize every window (controller console + this terminal included).
  (New-Object -ComObject Shell.Application).MinimizeAll()
  Start-Sleep -Milliseconds 800

  # fps 4 + downscale to 680 keeps the hero compact (the encoder writes full frames, so frame
  # count x width drive file size).
  $rec = Tool 'record' @{ action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 680 }
  Write-Host "record start: $($rec.result.content[0].text)"
  Start-Sleep -Milliseconds 500

  $subject = Start-Process -FilePath $subjectExe -WorkingDirectory $subjectDir -PassThru
  Write-Host "subject pid $($subject.Id)"

  $tree = $null
  for ($i = 0; $i -lt 25; $i++) { Start-Sleep -Milliseconds 600; $tree = QueryTree 'MCEC' 4; if ($tree) { break } }
  if (-not $tree) { throw 'subject MCEC window never appeared' }
  Write-Host 'subject window up'
  Start-Sleep -Milliseconds 1200   # dwell on the freshly-started window
  $tree = QueryTree 'MCEC' 5        # re-query so the menu bar is fully built before we locate it

  $isMenu = { param($n) ($n.controlType -match 'MenuItem') }
  $help = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'Help' }
  $file = Find $tree { param($n) (& $isMenu $n) -and $n.name -eq 'File' }
  if (-not $help -or -not $file) { throw 'File/Help menu items not found' }

  # Help > About
  ClickAbs ([int]($help.x + $help.width / 2)) ([int]($help.y + $help.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_a'; Start-Sleep -Milliseconds 1900
  Cmd 'key_esc'; Start-Sleep -Milliseconds 1000

  # File > Settings
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_s'; Start-Sleep -Milliseconds 2100
  Cmd 'key_esc'; Start-Sleep -Milliseconds 1000

  # File > Exit
  ClickAbs ([int]($file.x + $file.width / 2)) ([int]($file.y + $file.height / 2))
  Start-Sleep -Milliseconds 600; Cmd 'key_x'; Start-Sleep -Milliseconds 1300
  if (-not $subject.HasExited) { Cmd 'enter'; Start-Sleep -Milliseconds 1000 }

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
