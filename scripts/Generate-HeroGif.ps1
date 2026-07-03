#requires -version 7
<#
.SYNOPSIS
  Minimal support for the agent-driven hero recording (see docs/hero-gif.md): builds MCEC and stands up
  an authorized, MCP-serving CONTROLLER from a disposable copy for an agent to drive. It does NOT drive
  the tour or record anything; the agent does that over MCP, following docs/hero-gif.md. This is the only
  script in the flow, and it exists solely because an agent cannot bootstrap the first controller over
  MCP (there is nothing to connect to yet).

.DESCRIPTION
  Default (stand up the controller):
    - builds MCEC,
    - copies the build to a throwaway dir (so src\bin is NEVER mutated),
    - writes that copy's agent-ready config (MCP on 127.0.0.1:<Port>, agent commands + session
      provisioning authorized, overlay on and docked Left, window hidden),
    - launches it and waits for the MCP endpoint,
    - prints how to register the endpoint and hands off to the agent.
  The controller keeps running so the agent can drive it. Tear it down with -Stop when finished.

  -Stop: kills MCEC and deletes the throwaway controller copy(ies). Provisioned session dirs the agent
  did not end are reaped automatically on the next MCEC launch.

.PARAMETER Config  Build configuration (Debug or Release). Default: Debug.
.PARAMETER Port    Localhost MCP port for the controller. Default: 5151.
.PARAMETER Stop    Tear down: kill MCEC and remove the throwaway controller copy.
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')][string]$Config = 'Debug',
  [int]$Port = 0,
  [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$copyGlob = 'mcec-hero-controller-*'

# Stop ONLY the hero controller(s): mcec processes whose exe lives under a mcec-hero-controller-* temp
# dir. Never a blanket `Get-Process mcec | kill` (that would take the operator's real MCEC or another
# agent's instance). Then remove the throwaway copies.
function Stop-HeroControllers {
  foreach ($p in Get-Process -Name mcec -ErrorAction SilentlyContinue) {
    $path = try { $p.Path } catch { $null }
    if ($path -and $path -like "*\$($copyGlob.TrimEnd('*'))*") { try { $p.Kill($true) } catch {} }
  }
  Get-ChildItem $env:TEMP -Directory -Filter $copyGlob -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($Stop) {
  Stop-HeroControllers
  Write-Host 'Stopped the hero controller(s) and removed the throwaway copies. Other MCEC instances are left alone; provisioned session dirs are reaped on the next MCEC launch.'
  return
}

# A fresh bootstrap reclaims any prior hero controller first (a re-run should not leave orphans or fight
# over a port), then picks a FREE localhost port so concurrent/leftover instances never collide.
Stop-HeroControllers
if ($Port -le 0) {
  $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $l.Start(); $Port = ([System.Net.IPEndPoint]$l.LocalEndpoint).Port; $l.Stop()
}

$buildDir = Join-Path $repoRoot "src\bin\$Config\net10.0-windows"
Write-Host "Building ($Config)..."
dotnet build (Join-Path $repoRoot 'src\MCEControl.csproj') -c $Config | Out-Null
if (-not (Test-Path (Join-Path $buildDir 'mcec.exe'))) { throw "mcec.exe not found in $buildDir" }

# Run the controller from a fresh disposable COPY so the build tree is never mutated; the subject the
# agent provisions is a fresh copy too. The whole copy is removed by -Stop.
$ctrlDir = Join-Path $env:TEMP ("mcec-hero-controller-" + [System.IO.Path]::GetRandomFileName())
Copy-Item $buildDir $ctrlDir -Recurse -Force

Set-Content -Encoding UTF8 -Path (Join-Path $ctrlDir 'mcec.settings') -Value @"
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>true</McpServerEnabled>
  <McpBindAddress>127.0.0.1</McpBindAddress>
  <McpHttpPort>$Port</McpHttpPort>
  <AllowSessionProvisioning>true</AllowSessionProvisioning>
  <CommandOverlayEnabled>true</CommandOverlayEnabled>
  <CommandOverlayPosition>Left</CommandOverlayPosition>
  <HideOnStartup>true</HideOnStartup>
  <ActAsServer>false</ActAsServer>
</AppSettings>
"@

# Two security gates apply to every agent tool: the global AgentCommandsEnabled (above) AND the
# per-command Enabled flag in this table (built-ins ship disabled). So the tour's tools must each be
# enabled here. provision-session/end-session are the exception: they are dispatched before the
# per-command gate and need no entry. Keystrokes go through the built-in keyboard PRIMITIVES rather than
# named commands: `chars:` sends any single character, and `shiftdown:`/`shiftup:` hold/release modifiers
# (letters as chars; Win+D via shiftdown:lwin + d + shiftup:lwin); dialog buttons are reached with click.
Set-Content -Encoding UTF8 -Path (Join-Path $ctrlDir 'mcec.commands') -Value @'
<?xml version="1.0" encoding="utf-8"?>
<MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="3.0.0">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <displays  Cmd="displays"   Enabled="true" />
  <query     Cmd="query"      Enabled="true" />
  <capture   Cmd="capture"    Enabled="true" />
  <launch    Cmd="launch"     Enabled="true" />
  <click     Cmd="click"      Enabled="true" />
  <drag      Cmd="drag"       Enabled="true" />
  <record    Cmd="record"     Enabled="true" />
  <chars     Cmd="chars:"     Enabled="true" />
  <sendinput Cmd="shiftdown:" Enabled="true" />
  <sendinput Cmd="shiftup:"   Enabled="true" />
</Commands>
</MCEController>
'@

# Launch DETACHED so the controller outlives THIS script. Start-Process leaves the child in this
# process's Windows Job Object, so an agent runner that kills the job when the command returns takes the
# controller down with it (observed: "the process dies when the bootstrap script exits"). Creating it via
# WMI parents the new process to the WMI host, OUTSIDE our job, so it survives the launcher's exit.
$exe = Join-Path $ctrlDir 'mcec.exe'
$create = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
  CommandLine      = "`"$exe`""
  CurrentDirectory = $ctrlDir
}
if ($create.ReturnValue -ne 0) {
  throw "Failed to launch the controller detached (Win32_Process.Create returned $($create.ReturnValue))."
}
$ctrlPid = $create.ProcessId
$url = "http://127.0.0.1:$Port/mcp"
$up = $false
for ($i = 0; $i -lt 40; $i++) {
  Start-Sleep -Milliseconds 600
  try {
    $body = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{} } | ConvertTo-Json -Compress
    if ((Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 5).result.serverInfo.name -eq 'MCEC') {
      $up = $true; break
    }
  }
  catch {}
}
if (-not $up) { throw "controller MCP endpoint did not come up at $url" }

Write-Host ''
Write-Host "Controller is up (pid=$ctrlPid, detached; survives this script exiting); agent commands and session provisioning are authorized."
Write-Host "  MCP endpoint : $url"
Write-Host "  Register it  : claude mcp add --transport http mcec $url"
Write-Host '  Then ask your agent to recreate the hero per docs/hero-gif.md (it drives entirely via MCP tools).'
Write-Host '  When finished: pwsh -NoProfile -File scripts/Generate-HeroGif.ps1 -Stop'
