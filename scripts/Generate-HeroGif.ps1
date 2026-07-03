#requires -version 7
<#
.SYNOPSIS
  Prepares a develop-stamped build and prints the natural-language brief for an agent to record the MCEC
  hero GIF (docs/hero.gif). The tour itself is now the AGENT's job, driven through MCEC's own tools with
  the isolated subject supplied by `provision-session`; this script no longer hand-manages a subject copy
  or flips the controller's gates.

.DESCRIPTION
  See docs/hero-gif.md for the full agent-driven flow. In brief:

    1. Operator opts in once: File > Settings > Agent > "Allow agents to provision disposable instances"
       on the controller (a NON-installed build; the Program Files install refuses the MCP front door).
    2. An agent connected to that controller over MCP provisions a disposable subject, launches it, tours
       Settings (every tab) / resize / move / About, records the region, and calls end-session.

  This script keeps only the one concern worth automating deterministically: the recording must come from
  a develop-stamped build, because GitVersion bakes the branch name into the version string that appears
  IN the hero (the subject's log window, status bar, and About box) -- and `provision-session` copies the
  controller's binaries into the subject, so the controller's stamp is what lands in frame. It builds,
  verifies the stamp, then prints the brief.

.PARAMETER Config
  Build configuration to use (Debug or Release). Default: Debug.

.PARAMETER AllowNonDevelopBuild
  Skip the develop-branch/develop-stamp guard. Pass this only when a feature-branch hero is deliberate.
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

# GUARD: the recording must come from a develop-stamped build. GitVersion bakes the branch name into the
# version string, which shows in the hero; a timestamp-fresh binary from whatever branch was built last
# passes a naive "build if needed" check.
$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'develop' -and -not $AllowNonDevelopBuild) {
  throw "Refusing to prepare the hero from branch '$branch' (the GitVersion stamp lands in the GIF). " +
        'Switch to develop (git checkout develop && git pull), or pass -AllowNonDevelopBuild if a ' +
        'branch build is deliberate.'
}

Write-Host "Building ($Config)..."
dotnet build (Join-Path $repoRoot 'src\MCEControl.csproj') -c $Config | Out-Null
if (-not (Test-Path $exe)) { throw "mcec.exe not found at $exe" }

$stamp = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $AllowNonDevelopBuild -and $stamp -notlike '*Branch.develop.*') {
  throw "mcec.exe is stamped '$stamp' after rebuilding; expected a Branch.develop stamp. " +
        'Is the working tree in a detached/unexpected state?'
}
Write-Host "Controller build ready: $stamp"
Write-Host "  $exe"
Write-Host ''
Write-Host 'Next (see docs/hero-gif.md):'
Write-Host '  1. Launch the controller above and opt in: File > Settings > Agent >'
Write-Host '     "Allow agents to provision disposable instances".'
Write-Host '  2. Connect an agent to it over MCP and give it this brief:'
Write-Host ''
Write-Host '     Record the MCEC hero. Provision a disposable MCEC instance for the subject, launch it,'
Write-Host '     and record a short tour of its own window: open File > Settings and visit every tab,'
Write-Host '     close it, mouse-resize the window about 25% smaller by dragging its bottom-right sizing'
Write-Host '     border, drag the title bar in a small circle, then open Help > About and pause on it.'
Write-Host '     Keep the overlay narrating on the left. Write the result to docs/hero.gif, then end the'
Write-Host '     session.'
Write-Host ''
Write-Host 'The agent uses provision-session for the isolated subject and end-session to tear it down;'
Write-Host 'no hand-managed copy, no config XML. The exact tool sequence is in docs/hero-gif.md.'
